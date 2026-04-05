using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FFmpeg.AutoGen;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Logging;
using osu.Framework.Platform.Linux.Native;

namespace osu.Framework.Graphics.Video
{
    /// <summary>
    /// Represents a video encoder that can be used to convert textures and frames into a video file. Check out <see cref="osu.Framework.Graphics.Video.VideoDecoder" /> for more details.
    /// </summary>
    public unsafe class VideoEncoder : IDisposable
    {
        /// <summary>
        /// True if the encoder currently is encoding frames, false otherwise.
        /// </summary>
        public bool IsRunning => State == EncoderState.Running;

        /// <summary>
        /// True if the encoder has faulted after starting to encode.
        /// </summary>
        public bool IsFaulted => State == EncoderState.Faulted;

        /// <summary>
        /// The current state of the encoding process.
        /// </summary>
        public EncoderState State { get; private set; }

        /// <summary>
        /// Determines which hardware acceleration device(s) should be used.
        /// </summary>
        public readonly Bindable<HardwareVideoEncoderType> TargetHardwareVideoEncoders = new();

        private Task encodingTask;
        private CancellationTokenSource encodingTaskCancellationTokenSource;

        private AVFormatContext* formatContext;
        private AVStream* stream;
        private AVCodecContext* codecContext;

        private readonly ConcurrentQueue<Action> encoderCommands = new();
        private readonly ConcurrentQueue<IntPtr> frameQueue = new();

        private ObjectHandle<VideoEncoder> handle;
        private readonly FFmpegEncodeFuncs ffmpeg;

        private bool isDisposed;

        private readonly string outputPath;
        private readonly int width;
        private readonly int height;
        private readonly AVRational frameRate;

        static VideoEncoder()
        {
            if (RuntimeInfo.OS == RuntimeInfo.Platform.Linux)
            {
                void loadVersionedLibraryGlobally(string name)
                {
                    int version = FFmpeg.AutoGen.ffmpeg.LibraryVersionMap[name];
                    Library.Load($"lib{name}.so.{version}", Library.LoadFlags.RTLD_LAZY | Library.LoadFlags.RTLD_GLOBAL);
                }

                // FFmpeg.AutoGen doesn't load libraries as RTLD_GLOBAL, so we must load them ourselves to fix inter-library dependencies
                // otherwise they would fallback to the system-installed libraries that can differ in version installed.
                loadVersionedLibraryGlobally("avutil");
                loadVersionedLibraryGlobally("avcodec");
                loadVersionedLibraryGlobally("avformat");
                loadVersionedLibraryGlobally("swscale");
            }
        }

        /// <summary>
        /// Creates a new video encoder that encodes frames into the given output file path.
        /// </summary>
        /// <param name="path">The path to the file that should be encoded to.</param>
        /// <param name="width">The width of the video.</param>
        /// <param name="height">The height of the video.</param>
        /// <param name="fps">The frames per second of the video.</param>
        public VideoEncoder(string path, int width, int height, int fps = 60)
        {
            ffmpeg = CreateFuncs();
            outputPath = path;
            this.width = width;
            this.height = height;
            this.frameRate = new AVRational { num = fps, den = 1 };

            State = EncoderState.Ready;
            handle = new ObjectHandle<VideoEncoder>(this, GCHandleType.Normal);

            TargetHardwareVideoEncoders.BindValueChanged(_ =>
            {
                // ignore if encoding wasn't initialized yet.
                if (formatContext != null)
                    encoderCommands.Enqueue(recreateCodecContext);
            });
        }

        /// <summary>
        /// Enqueues a frame to be encoded.
        /// </summary>
        /// <param name="frame">The frame to encode.</param>
        public void SendFrame(AVFrame* frame)
        {
            if (State != EncoderState.Running) return;

            var clonedFrame = ffmpeg.av_frame_alloc();
            ffmpeg.av_frame_move_ref(clonedFrame, frame);

            // Cast to IntPtr to store in the generic queue
            frameQueue.Enqueue((IntPtr)clonedFrame);
        }

        /// <summary>
        /// Starts the encoding process. The encoding will happen asynchronously in a separate thread.
        /// </summary>
        /// <param name="codecId">The target codec to encode with.</param>
        public void StartEncoding(AVCodecID codecId = AVCodecID.AV_CODEC_ID_H264)
        {
            if (encodingTask != null)
                throw new InvalidOperationException("Cannot start encoding once already started.");

            try
            {
                logAvailableFormats();
                prepareEncoding(codecId);
                recreateCodecContext();
            }
            catch (Exception e)
            {
                Logger.Log($"Encoder faulted during init: {e}");
                State = EncoderState.Faulted;
                return;
            }

            encodingTaskCancellationTokenSource = new CancellationTokenSource();
            encodingTask = Task.Factory.StartNew(() => encodingLoop(encodingTaskCancellationTokenSource.Token),
                encodingTaskCancellationTokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);

            State = EncoderState.Running;
        }

        private void logAvailableFormats()
        {
            void* iterator = null;
            var names = new List<string>();

            while (true)
            {
                var fmt = ffmpeg.av_codec_iterate(&iterator);
                if (fmt == null) break;
                names.Add(Marshal.PtrToStringAnsi((IntPtr)fmt->name) ?? "?");
            }

            Logger.Log($"Available codecs: {string.Join(", ", names)}");
        }

        // sets up libavformat state: creates the AVFormatContext, the frames, etc. to start encoding, but does not actually start encoding
        private void prepareEncoding(AVCodecID codecId)
        {
            AVFormatContext* fc = null;
            // TODO: properly do this later
#nullable enable
            string? format = Path.GetExtension(outputPath).TrimStart('.').ToLowerInvariant() switch
            {
                "mp4" => "mp4",
                "mkv" => "matroska",
                "webm" => "webm",
                "mov" => "mov",
                _ => null
            };
#nullable restore

            int result = ffmpeg.avformat_alloc_output_context2(&fc, null, format, outputPath);
            if (result < 0) throw new InvalidOperationException($"Could not allocate output context: {getErrorMessage(result)}");

            formatContext = fc;

            stream = ffmpeg.avformat_new_stream(formatContext, null);
            if (stream == null) throw new InvalidOperationException("Could not create new stream.");

            result = ffmpeg.avio_open(&formatContext->pb, outputPath, FFmpegEncodeFuncs.AVIO_FLAG_WRITE);
            if (result < 0) throw new InvalidOperationException($"Could not open output file: {getErrorMessage(result)}");
        }

        private void recreateCodecContext()
        {
            var targetHw = TargetHardwareVideoEncoders.Value;
            bool openSuccessful = false;

            foreach (var (codecWrapper, hwDeviceType) in GetAvailableEncoders(stream->codecpar->codec_id, targetHw))
            {
                AVCodec* encoder = codecWrapper.Pointer;

                if (codecContext != null)
                {
                    fixed (AVCodecContext** ptr = &codecContext)
                        ffmpeg.avcodec_free_context(ptr);
                }

                codecContext = ffmpeg.avcodec_alloc_context3(encoder);
                codecContext->width = width;
                codecContext->height = height;

                codecContext->time_base = new AVRational { num = frameRate.den, den = frameRate.num };
                codecContext->pix_fmt = AVPixelFormat.AV_PIX_FMT_YUV420P;

                if ((formatContext->oformat->flags & FFmpeg.AutoGen.ffmpeg.AVFMT_GLOBALHEADER) != 0)
                    codecContext->flags |= FFmpeg.AutoGen.ffmpeg.AV_CODEC_FLAG_GLOBAL_HEADER;

                if (hwDeviceType != AVHWDeviceType.AV_HWDEVICE_TYPE_NONE)
                {
                    ffmpeg.av_hwdevice_ctx_create(&codecContext->hw_device_ctx, hwDeviceType, null, null, 0);
                }

                int openResult = ffmpeg.avcodec_open2(codecContext, encoder, null);
                if (openResult < 0) continue;

                ffmpeg.avcodec_parameters_from_context(stream->codecpar, codecContext);
                openSuccessful = true;
                break;
            }

            if (!openSuccessful) throw new InvalidOperationException("No usable encoder found.");

            ffmpeg.av_write_header(formatContext, null);
        }

        private string getErrorMessage(int errorCode)
        {
            const ulong buffer_size = 256;
            byte[] buffer = new byte[buffer_size];

            int strErrorCode;

            fixed (byte* bufPtr = buffer)
            {
                strErrorCode = ffmpeg.av_strerror(errorCode, bufPtr, buffer_size);
            }

            if (strErrorCode < 0)
                return $"{errorCode} (av_strerror failed with code {strErrorCode})";

            int messageLength = Math.Max(0, Array.IndexOf(buffer, (byte)0));
            return $"{Encoding.ASCII.GetString(buffer[..messageLength])} ({errorCode})";
        }

        private void processFrame(AVFrame* frame, AVPacket* pkt)
        {
            int result = ffmpeg.avcodec_send_frame(codecContext, frame);
            if (result < 0) return;

            while (result >= 0)
            {
                result = ffmpeg.avcodec_receive_packet(codecContext, pkt);
                if (result == -FFmpegEncodeFuncs.EAGAIN || result == FFmpegEncodeFuncs.AVERROR_EOF) break;

                pkt->stream_index = stream->index;
                ffmpeg.av_interleaved_write_frame(formatContext, pkt);
                ffmpeg.av_packet_unref(pkt);
            }
        }

        private void encodingLoop(CancellationToken token)
        {
            var packet = ffmpeg.av_packet_alloc();

            try
            {
                while (!token.IsCancellationRequested || !frameQueue.IsEmpty)
                {
                    while (encoderCommands.TryDequeue(out var cmd)) cmd();

                    if (frameQueue.TryDequeue(out var framePtr))
                    {
                        var frame = (AVFrame*)framePtr;
                        processFrame(frame, packet);
                        ffmpeg.av_frame_free(&frame);
                    }
                    else
                    {
                        Thread.Sleep(1);
                    }
                }

                // Flush encoder
                processFrame(null, packet);
                ffmpeg.av_write_trailer(formatContext);
                State = EncoderState.EndOfStream;
            }
            catch (Exception e)
            {
                Logger.Log($"Encoding loop faulted: {e}");
                State = EncoderState.Faulted;
            }
            finally
            {
                ffmpeg.av_packet_free(&packet);

                if (State != EncoderState.Faulted)
                    State = EncoderState.Stopped;
            }
        }

        protected class AvCodecWrapper
        {
            public AVCodec* Pointer { get; }

            public AvCodecWrapper(AVCodec* pointer)
            {
                Pointer = pointer;
            }
        }

        /// <remarks>
        /// Returned HW devices are not guaranteed to be available on the current machine, they only represent what the loaded FFmpeg libraries support.
        /// </remarks>
        protected virtual IEnumerable<(AvCodecWrapper codec, AVHWDeviceType hwDeviceType)> GetAvailableEncoders(AVCodecID codecId, HardwareVideoEncoderType targetHw)
        {
            var encoders = new List<(AvCodecWrapper, AVHWDeviceType)>();
            void* iterator = null;

            while (true)
            {
                var avCodec = ffmpeg.av_codec_iterate(&iterator);
                if (avCodec == null) break;

                if (avCodec->id != codecId || ffmpeg.av_codec_is_encoder(avCodec) == 0) continue;

                encoders.Add((new AvCodecWrapper(avCodec), AVHWDeviceType.AV_HWDEVICE_TYPE_NONE));
            }

            return encoders;
        }

        #region Disposal

        ~VideoEncoder()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (isDisposed)
                return;

            isDisposed = true;

            if (disposing)
            {
                encodingTaskCancellationTokenSource?.Cancel();
                encodingTask?.WaitSafely();
                encodingTaskCancellationTokenSource?.Dispose();

                handle.Dispose();
            }

            if (codecContext != null)
            {
                fixed (AVCodecContext** ptr = &codecContext)
                    ffmpeg.avcodec_free_context(ptr);
            }

            if (formatContext != null)
            {
                if (formatContext->pb != null)
                    ffmpeg.avio_closep(&formatContext->pb);

                ffmpeg.avformat_free_context(formatContext);
                formatContext = null;
            }
        }

        #endregion

        protected virtual FFmpegEncodeFuncs CreateFuncs()
        {
            // other frameworks should handle native libraries themselves
            FFmpeg.AutoGen.ffmpeg.GetOrLoadLibrary = name =>
            {
                int version = FFmpeg.AutoGen.ffmpeg.LibraryVersionMap[name];

                // "lib" prefix and extensions are resolved by .net core
                string libraryName;

                switch (RuntimeInfo.OS)
                {
                    case RuntimeInfo.Platform.macOS:
                        libraryName = $"{name}.{version}";
                        break;

                    case RuntimeInfo.Platform.Windows:
                        libraryName = $"{name}-{version}";
                        break;

                    // To handle versioning in Linux, we have to specify the entire file name
                    // because Linux uses a version suffix after the file extension (e.g. libavutil.so.56)
                    // More info: https://learn.microsoft.com/en-us/dotnet/standard/native-interop/native-library-loading?view=net-6.0
                    case RuntimeInfo.Platform.Linux:
                        libraryName = $"lib{name}.so.{version}";
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(RuntimeInfo.OS), RuntimeInfo.OS, null);
                }

                return NativeLibrary.Load(libraryName, RuntimeInfo.EntryAssembly, DllImportSearchPath.UseDllDirectoryForDependencies | DllImportSearchPath.SafeDirectories);
            };

            return new FFmpegEncodeFuncs
            {
                av_dict_set = FFmpeg.AutoGen.ffmpeg.av_dict_set,
                av_dict_free = FFmpeg.AutoGen.ffmpeg.av_dict_free,
                av_strdup = FFmpeg.AutoGen.ffmpeg.av_strdup,
                av_strerror = FFmpeg.AutoGen.ffmpeg.av_strerror,
                av_malloc = FFmpeg.AutoGen.ffmpeg.av_malloc,
                av_freep = FFmpeg.AutoGen.ffmpeg.av_freep,
                av_frame_alloc = FFmpeg.AutoGen.ffmpeg.av_frame_alloc,
                av_frame_free = FFmpeg.AutoGen.ffmpeg.av_frame_free,
                av_frame_unref = FFmpeg.AutoGen.ffmpeg.av_frame_unref,
                av_frame_move_ref = FFmpeg.AutoGen.ffmpeg.av_frame_move_ref,
                av_frame_get_buffer = FFmpeg.AutoGen.ffmpeg.av_frame_get_buffer,
                av_packet_alloc = FFmpeg.AutoGen.ffmpeg.av_packet_alloc,
                av_packet_unref = FFmpeg.AutoGen.ffmpeg.av_packet_unref,
                av_packet_free = FFmpeg.AutoGen.ffmpeg.av_packet_free,
                av_codec_iterate = FFmpeg.AutoGen.ffmpeg.av_codec_iterate,
                av_codec_is_encoder = FFmpeg.AutoGen.ffmpeg.av_codec_is_encoder,
                avcodec_alloc_context3 = FFmpeg.AutoGen.ffmpeg.avcodec_alloc_context3,
                avcodec_free_context = FFmpeg.AutoGen.ffmpeg.avcodec_free_context,
                avcodec_parameters_from_context = FFmpeg.AutoGen.ffmpeg.avcodec_parameters_from_context,
                avcodec_open2 = FFmpeg.AutoGen.ffmpeg.avcodec_open2,
                avcodec_send_frame = FFmpeg.AutoGen.ffmpeg.avcodec_send_frame,
                avcodec_receive_packet = FFmpeg.AutoGen.ffmpeg.avcodec_receive_packet,
                avcodec_flush_buffers = FFmpeg.AutoGen.ffmpeg.avcodec_flush_buffers,
                avformat_alloc_output_context2 = FFmpeg.AutoGen.ffmpeg.avformat_alloc_output_context2,
                avformat_free_context = FFmpeg.AutoGen.ffmpeg.avformat_free_context,
                av_hwdevice_ctx_create = FFmpeg.AutoGen.ffmpeg.av_hwdevice_ctx_create,
                avformat_new_stream = FFmpeg.AutoGen.ffmpeg.avformat_new_stream,
                av_write_header = FFmpeg.AutoGen.ffmpeg.avformat_write_header,
                av_interleaved_write_frame = FFmpeg.AutoGen.ffmpeg.av_interleaved_write_frame,
                av_write_trailer = FFmpeg.AutoGen.ffmpeg.av_write_trailer,
                avio_open = FFmpeg.AutoGen.ffmpeg.avio_open,
                avio_closep = FFmpeg.AutoGen.ffmpeg.avio_closep,
                sws_freeContext = FFmpeg.AutoGen.ffmpeg.sws_freeContext,
                sws_getCachedContext = FFmpeg.AutoGen.ffmpeg.sws_getCachedContext,
                sws_scale = FFmpeg.AutoGen.ffmpeg.sws_scale
            };
        }

        /// <summary>
        /// Represents the possible states the encoder can be in.
        /// </summary>
        public enum EncoderState
        {
            /// <summary>
            /// The encoder is ready to begin encoding. This is the default state before the encoder starts operations.
            /// </summary>
            Ready = 0,

            /// <summary>
            /// The encoder is currently running and encoding frames.
            /// </summary>
            Running = 1,

            /// <summary>
            /// The encoder has faulted with an exception.
            /// </summary>
            Faulted = 2,

            /// <summary>
            /// The encoder has reached the end of the video data.
            /// </summary>
            EndOfStream = 3,

            /// <summary>
            /// The encoder has been completely stopped and cannot be resumed.
            /// </summary>
            Stopped = 4,
        }
    }
}
