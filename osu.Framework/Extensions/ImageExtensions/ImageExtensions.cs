// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Buffers;
using System.IO;
using NetVips;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using Image = NetVips.Image;

namespace osu.Framework.Extensions.ImageExtensions
{
    public static class ImageExtensions
    {
        /// <summary>
        /// Creates a contiguous and read-only span from the pixels of an <see cref="Image{TPixel}"/>.
        /// Useful for retrieving unmanaged pointers to the entire pixel data of the <see cref="Image{TPixel}"/> for marshalling.
        /// </summary>
        /// <remarks>
        /// The returned <see cref="ReadOnlyPixelSpan{TPixel}"/> must be disposed when usage is finished.
        /// </remarks>
        /// <param name="image">The <see cref="Image{TPixel}"/>.</param>
        /// <typeparam name="TPixel">The type of pixels in <paramref name="image"/>.</typeparam>
        /// <returns>The <see cref="ReadOnlyPixelSpan{TPixel}"/>.</returns>
        public static ReadOnlyPixelSpan CreateReadOnlyPixelSpan(this Image image)
            => new ReadOnlyPixelSpan(image);

        /// <summary>
        /// Creates a contiguous and read-only memory from the pixels of an <see cref="Image{TPixel}"/>.
        /// Useful for retrieving unmanaged pointers to the entire pixel data of the <see cref="Image{TPixel}"/> for marshalling.
        /// </summary>
        /// <remarks>
        /// The returned <see cref="ReadOnlyPixelMemory{TPixel}"/> must be disposed when usage is finished.
        /// </remarks>
        /// <param name="image">The <see cref="Image{TPixel}"/>.</param>
        /// <typeparam name="TPixel">The type of pixels in <paramref name="image"/>.</typeparam>
        /// <returns>The <see cref="ReadOnlyPixelMemory{TPixel}"/>.</returns>
        public static ReadOnlyPixelMemory CreateReadOnlyPixelMemory(this Image image)
            => new ReadOnlyPixelMemory(image);

        /// <summary>
        /// Creates a new contiguous memory buffer from the pixels in an <see cref="Image{TPixel}"/>.
        /// </summary>
        /// <remarks>
        /// The returned <see cref="IMemoryOwner{T}"/> must be disposed when usage is finished.
        /// </remarks>
        /// <param name="image">The <see cref="Image{TPixel}"/>.</param>
        /// <typeparam name="TPixel">The type of pixels in <paramref name="image"/>.</typeparam>
        /// <returns>The <see cref="IMemoryOwner{T}"/>, containing the contiguous pixel memory.</returns>
        internal static IMemoryOwner<TPixel> CreateContiguousMemory<TPixel>(this SixLabors.ImageSharp.Image<TPixel> image)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            var allocatedOwner = SixLabors.ImageSharp.Configuration.Default.MemoryAllocator.Allocate<TPixel>(image.Width * image.Height);
            var allocatedMemory = allocatedOwner.Memory;

            for (int r = 0; r < image.Height; r++)
                image.DangerousGetPixelRowMemory(r).CopyTo(allocatedMemory.Slice(r * image.Width));

            return allocatedOwner;
        }

        /// <summary>
        /// Convert an ImageSharp image to a NetVips Image.
        /// </summary>
        public static NetVips.Image ToVips(this SixLabors.ImageSharp.Image source)
        {
            if (source is SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32> rgbaImage)
            {
                byte[] pixels = new byte[rgbaImage.Width * rgbaImage.Height * 4];
                rgbaImage.CopyPixelDataTo(pixels);

                return Image.NewFromMemory(
                    pixels,
                    rgbaImage.Width,
                    rgbaImage.Height,
                    4,
                    Enums.BandFormat.Uchar
                ).Copy(interpretation: Enums.Interpretation.Srgb);
            }

            using var ms = new MemoryStream();
            source.SaveAsPng(ms);
            return Image.NewFromBuffer(ms.ToArray()).Copy(interpretation: Enums.Interpretation.Srgb);
        }

        /// <summary>
        /// Convert a NetVips Image to an ImageSharp image.
        /// </summary>
        public static SixLabors.ImageSharp.Image ToImageSharp(this NetVips.Image source)
        {
            byte[] buffer = source.TiffsaveBuffer();

            return SixLabors.ImageSharp.Image.Load(buffer);
        }

        /// <summary>
        /// Convert a NetVips Image directly to a specific ImageSharp pixel type.
        /// </summary>
        public static SixLabors.ImageSharp.Image<TPixel> ToImageSharp<TPixel>(this NetVips.Image source)
            where TPixel : unmanaged, SixLabors.ImageSharp.PixelFormats.IPixel<TPixel>
        {
            byte[] buffer = source.TiffsaveBuffer();
            return SixLabors.ImageSharp.Image.Load<TPixel>(buffer);
        }
    }
}
