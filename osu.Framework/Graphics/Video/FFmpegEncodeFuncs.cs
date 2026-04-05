// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System.Runtime.InteropServices;
using FFmpeg.AutoGen;
using JetBrains.Annotations;

// ReSharper disable InconsistentNaming
// ReSharper disable MissingBlankLines
#pragma warning disable IDE1006 // Naming style

namespace osu.Framework.Graphics.Video
{
    public unsafe class FFmpegEncodeFuncs
    {
        #region Delegates

        public delegate int AvDictSetDelegate(AVDictionary** pm, [MarshalAs(UnmanagedType.LPUTF8Str)] string key, [MarshalAs(UnmanagedType.LPUTF8Str)] string value, int flags);

        public delegate void AvDictFreeDelegate(AVDictionary** m);

        public delegate AVFrame* AvFrameAllocDelegate();

        public delegate void AvFrameFreeDelegate(AVFrame** frame);

        public delegate void AvFrameUnrefDelegate(AVFrame* frame);

        public delegate void AvFrameMoveRefDelegate(AVFrame* dst, AVFrame* src);

        public delegate int AvFrameGetBufferDelegate(AVFrame* frame, int align);

        public delegate byte* AvStrDupDelegate(string s);

        public delegate int AvStrErrorDelegate(int errnum, byte* buffer, ulong bufSize);

        public delegate void* AvMallocDelegate(ulong size);

        public delegate void AvFreepDelegate(void* ptr);

        public delegate AVPacket* AvPacketAllocDelegate();

        public delegate void AvPacketUnrefDelegate(AVPacket* pkt);

        public delegate void AvPacketFreeDelegate(AVPacket** pkt);

        public delegate int AvcodecSendFrameDelegate(AVCodecContext* avctx, AVFrame* frame);

        public delegate int AvcodecReceivePacketDelegate(AVCodecContext* avctx, AVPacket* avpkt);

        public delegate int AvInterleavedWriteFrameDelegate(AVFormatContext* s, AVPacket* pkt);

        public delegate int AvWriteTrailerDelegate(AVFormatContext* s);

        public delegate int AvformatWriteHeaderDelegate(AVFormatContext* s, AVDictionary** options);

        public delegate AVStream* AvformatNewStreamDelegate(AVFormatContext* s, AVCodec* c);

        public delegate int AvHwdeviceCtxCreateDelegate(AVBufferRef** device_ctx, AVHWDeviceType type, [MarshalAs(UnmanagedType.LPUTF8Str)] string device, AVDictionary* opts, int flags);

        public delegate int AvformatAllocOutputContext2Delegate(AVFormatContext** ctx, AVOutputFormat* oformat, [MarshalAs(UnmanagedType.LPUTF8Str)] string format_name, [MarshalAs(UnmanagedType.LPUTF8Str)] string filename);

        public delegate void AvformatFreeContextDelegate(AVFormatContext* s);

        public delegate AVCodec* AvCodecIterateDelegate(void** opaque);

        public delegate int AvCodecIsEncoderDelegate(AVCodec* codec);

        public delegate AVCodecContext* AvcodecAllocContext3Delegate(AVCodec* codec);

        public delegate void AvcodecFreeContextDelegate(AVCodecContext** avctx);

        public delegate int AvcodecParametersFromContextDelegate(AVCodecParameters* par, AVCodecContext* codec);

        public delegate int AvcodecOpen2Delegate(AVCodecContext* avctx, AVCodec* codec, AVDictionary** options);

        public delegate void AvcodecFlushBuffersDelegate(AVCodecContext* avctx);

        public delegate int AvioOpenDelegate(AVIOContext** s, [MarshalAs(UnmanagedType.LPUTF8Str)] string url, int flags);

        public delegate int AvioClosepDelegate(AVIOContext** s);

        public delegate AVIOContext* AvioAllocContextDelegate(byte* buffer, int buffer_size, int write_flag, void* opaque, avio_alloc_context_read_packet_func read_packet, avio_alloc_context_write_packet_func write_packet, avio_alloc_context_seek_func seek);

        public delegate void AvioContextFreeDelegate(AVIOContext** s);

        public delegate void SwsFreeContextDelegate(SwsContext* swsContext);

        public delegate SwsContext* SwsGetCachedContextDelegate(SwsContext* context, int srcW, int srcH, AVPixelFormat srcFormat, int dstW, int dstH, AVPixelFormat dstFormat, int flags, SwsFilter* srcFilter, SwsFilter* dstFilter, double* param);

        public delegate int SwsScaleDelegate(SwsContext* c, byte*[] srcSlice, int[] srcStride, int srcSliceY, int srcSliceH, byte*[] dst, int[] dstStride);

        #endregion

        [CanBeNull]
        public AvDictSetDelegate av_dict_set;

        [CanBeNull]
        public AvDictFreeDelegate av_dict_free;

        public AvFrameAllocDelegate av_frame_alloc;
        public AvFrameFreeDelegate av_frame_free;
        public AvFrameUnrefDelegate av_frame_unref;
        public AvFrameMoveRefDelegate av_frame_move_ref;
        public AvFrameGetBufferDelegate av_frame_get_buffer;
        public AvStrDupDelegate av_strdup;
        public AvStrErrorDelegate av_strerror;
        public AvMallocDelegate av_malloc;
        public AvFreepDelegate av_freep;
        public AvPacketAllocDelegate av_packet_alloc;
        public AvPacketUnrefDelegate av_packet_unref;
        public AvPacketFreeDelegate av_packet_free;
        public AvcodecSendFrameDelegate avcodec_send_frame;
        public AvcodecReceivePacketDelegate avcodec_receive_packet;
        public AvInterleavedWriteFrameDelegate av_interleaved_write_frame;
        public AvWriteTrailerDelegate av_write_trailer;
        public AvformatWriteHeaderDelegate av_write_header;
        public AvHwdeviceCtxCreateDelegate av_hwdevice_ctx_create;
        public AvformatNewStreamDelegate avformat_new_stream;
        public AvformatAllocOutputContext2Delegate avformat_alloc_output_context2;
        public AvformatFreeContextDelegate avformat_free_context;
        public AvCodecIterateDelegate av_codec_iterate;
        public AvCodecIsEncoderDelegate av_codec_is_encoder;
        public AvcodecAllocContext3Delegate avcodec_alloc_context3;
        public AvcodecFreeContextDelegate avcodec_free_context;
        public AvcodecParametersFromContextDelegate avcodec_parameters_from_context;
        public AvcodecOpen2Delegate avcodec_open2;
        public AvcodecFlushBuffersDelegate avcodec_flush_buffers;
        public AvioOpenDelegate avio_open;
        public AvioClosepDelegate avio_closep;
        public AvioAllocContextDelegate avio_alloc_context;
        public AvioContextFreeDelegate avio_context_free;
        public SwsFreeContextDelegate sws_freeContext;
        public SwsGetCachedContextDelegate sws_getCachedContext;
        public SwsScaleDelegate sws_scale;

        // Touching AutoGen.ffmpeg or its LibraryLoader in any way on non-Desktop platforms
        // will cause it to throw in static constructor, which can't be bypassed.
        // Define our own constants to avoid touching the class.

        public const int AVIO_FLAG_WRITE = 2;
        public const int AV_TIME_BASE = 1000000;
        public static readonly int EAGAIN = RuntimeInfo.IsApple ? 35 : 11;
        public const int AVERROR_EOF = -('E' + ('O' << 8) + ('F' << 16) + (' ' << 24));
        public const long AV_NOPTS_VALUE = unchecked((long)0x8000000000000000);
    }
}
