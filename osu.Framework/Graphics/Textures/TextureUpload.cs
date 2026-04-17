// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Buffers;
using System.IO;
using osu.Framework.Extensions.ImageExtensions;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Logging;
using osuTK.Graphics.ES30;
using NetVips;
using SixLabors.ImageSharp.PixelFormats;
using StbiSharp;
using Image = NetVips.Image;

namespace osu.Framework.Graphics.Textures
{
    /// <summary>
    /// Low level class for queueing texture uploads to the GPU.
    /// Should be manually disposed if not queued for upload via <see cref="Texture.SetData(ITextureUpload)"/>.
    /// </summary>
    public class TextureUpload : ITextureUpload
    {
        /// <summary>
        /// The target mipmap level to upload into.
        /// </summary>
        public int Level { get; set; }

        /// <summary>
        /// The texture format for this upload.
        /// </summary>
        public PixelFormat Format => PixelFormat.Rgba;

        /// <summary>
        /// The target bounds for this upload. If not specified, will assume to be (0, 0, width, height).
        /// </summary>
        public RectangleI Bounds { get; set; }

        public ReadOnlySpan<byte> Data => rawData ?? pixelMemory.Span;

        public int Width { get; private set; }
        public int Height { get; private set; }

        /// <summary>
        /// The backing texture. A handle is kept to avoid early GC.
        /// </summary>
        private readonly Image image;

        private ReadOnlyPixelMemory pixelMemory;

        // For the raw bytes contructor
        private readonly byte[] rawData;
        private readonly ArrayPool<byte> arrayPool;

        public TextureUpload(byte[] data, ArrayPool<byte> pool = null)
        {
            rawData = data;
            arrayPool = pool;
        }

        /// <summary>
        /// Create an upload from a <see cref="TextureUpload"/>. This is the preferred method.
        /// </summary>
        /// <param name="image">The texture to upload.</param>
        public TextureUpload(Image image)
        {
            Image current = image;

            try
            {
                if (current.Interpretation != Enums.Interpretation.Srgb)
                {
                    var next = current.Colourspace(Enums.Interpretation.Srgb);
                    if (!current.Equals(image)) current.Dispose();
                    current = next;
                }

                if (!current.HasAlpha() || current.Bands < 4)
                {
                    var next = current.AddAlpha();
                    if (!current.Equals(image)) current.Dispose();
                    current = next;
                }

                if (current.Format != Enums.BandFormat.Uchar)
                {
                    var next = current.Cast(Enums.BandFormat.Uchar);
                    if (!current.Equals(image)) current.Dispose();
                    current = next;
                }

                this.image = current;
                Width = this.image.Width;
                Height = this.image.Height;
                pixelMemory = this.image.CreateReadOnlyPixelMemory();
            }
            catch
            {
                if (!current.Equals(image)) current?.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Create an upload from an arbitrary image stream.
        /// Note that this bypasses per-platform image loading optimisations.
        /// Use <see cref="TextureLoaderStore"/> as provided from GameHost where possible.
        /// </summary>
        /// <param name="stream">The image content.</param>
        public TextureUpload(Stream stream)
            : this(LoadFromStream(stream))
        {
        }

        private static bool stbiNotFound;

        internal static Image LoadFromStream(Stream stream)
        {
            long initialPos = stream.Position;
            bool isWebP = TextureUpload.isWebP(stream);
            Image result = null;

            try
            {
                result = Image.NewFromStream(stream, access: Enums.Access.Random);
            }
            catch (Exception e)
            {
                Logger.Log($"Texture could not be loaded via NetVips; trying STB: {e.Message}");
                stream.Position = initialPos;
            }

            if (result == null && !stbiNotFound)
            {
                try
                {
                    using (var buffer = SixLabors.ImageSharp.Configuration.Default.MemoryAllocator.Allocate<byte>((int)stream.Length))
                    {
                        stream.ReadExactly(buffer.Memory.Span);

                        using (var stbiImage = Stbi.LoadFromMemory(buffer.Memory.Span, 4))
                        {
                            result = Image.NewFromMemoryCopy(
                                stbiImage.Data,
                                stbiImage.Width,
                                stbiImage.Height,
                                4,
                                Enums.BandFormat.Uchar
                            ).Copy(interpretation: Enums.Interpretation.Srgb);
                        }
                    }
                }
                catch (Exception e)
                {
                    if (e is DllNotFoundException) stbiNotFound = true;
                    Logger.Log($"Texture could not be loaded via STB; falling back to ImageSharp: {e.Message}");
                    stream.Position = initialPos;
                }
            }

            if (result == null)
            {
                try
                {
                    using (var img = SixLabors.ImageSharp.Image.Load<Rgba32>(stream))
                        result = img.ToVips();
                }
                catch (Exception e)
                {
                    Logger.Log($"Texture could not be loaded via ImageSharp: {e.Message}");
                    stream.Position = initialPos;
                }
            }

            if (isWebP)
            {
                // a stupid fix for heavily compressed webp images with visible artifacts but it's efficient and works
                // Note: NetVips does not have a built-in BoxBlur method, so we'll make do of gauss blur
                // TODO: implement BoxBlur later for this- or don't if custom BoxBlur impl is slower than small sigma gauss.
                var fixedImage = result?.Gaussblur(0.01);
                result?.Dispose();
                result = fixedImage;
            }

            return result;
        }

        private static bool isWebP(Stream stream)
        {
            long initialPos = stream.Position;

            if (stream.Length < 12)
                return false;

            Span<byte> header = stackalloc byte[12];
            stream.ReadExactly(header);
            stream.Position = initialPos;

            return header[0] == 'R' && header[1] == 'I' &&
                   header[2] == 'F' && header[3] == 'F' &&
                   header[8] == 'W' && header[9] == 'E' &&
                   header[10] == 'B' && header[11] == 'P';
        }

        /// <summary>
        /// Create an empty upload. Used by <see cref="IFrameBuffer"/> for initialisation.
        /// </summary>
        internal TextureUpload()
        {
        }

        #region IDisposable Support

        private bool disposed;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool isDisposing)
        {
            if (disposed)
                return;

            image?.Dispose();
            if (image != null)
                pixelMemory.Dispose();

            if (rawData != null && arrayPool != null)
                arrayPool.Return(rawData);

            disposed = true;
        }

        #endregion
    }
}
