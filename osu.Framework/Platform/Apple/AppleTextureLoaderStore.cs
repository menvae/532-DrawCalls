// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using NetVips;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Platform.Apple.Native;
using osu.Framework.Platform.Apple.Native.Accelerate;
using Image = NetVips.Image;

namespace osu.Framework.Platform.Apple
{
    internal abstract class AppleTextureLoaderStore : TextureLoaderStore
    {
        protected AppleTextureLoaderStore(IResourceStore<byte[]> store)
            : base(store)
        {
        }

        protected unsafe Image ImageFromCGImage(CGImage cgImage)
        {
            int width = (int)cgImage.Width;
            int height = (int)cgImage.Height;

            var format = new vImage_CGImageFormat
            {
                BitsPerComponent = 8,
                BitsPerPixel = 32,
                ColorSpace = CGColorSpace.CreateDeviceRGB(),
                // notably, macOS & iOS generally use premultiplied alpha when rendering image to pixels via CGBitmapContext or otherwise,
                // but vImage offers rendering as straight alpha by specifying Last instead of PremultipliedLast.
                BitmapInfo = (CGBitmapInfo)CGImageAlphaInfo.Last,
                Decode = null,
                RenderingIntent = CGColorRenderingIntent.Default,
            };

            vImage_Buffer accImage = default;

            // perform initial call to retrieve preferred alignment and bytes-per-row values for the given image dimensions.// allocate aligned memory region to contain image pixel data.
            nuint alignment = (nuint)vImage.Init(&accImage, (uint)height, (uint)width, 32, vImage_Flags.NoAllocate);

            // allocate aligned memory region to contain image pixel data.
            nuint bytesCount = accImage.BytesPerRow * accImage.Height;
            byte* dataPtr = (byte*)NativeMemory.AlignedAlloc(bytesCount, alignment);
            accImage.Data = dataPtr;

            var result = vImage.InitWithCGImage(&accImage, &format, null, cgImage.Handle, vImage_Flags.NoAllocate);
            Debug.Assert(result == vImage_Error.NoError);

            Image finalImage;

            using (var rawImage = Image.NewFromMemory((IntPtr)dataPtr, bytesCount, width, height, 4, Enums.BandFormat.Uchar))
            {
                finalImage = rawImage.Copy(interpretation: Enums.Interpretation.Srgb);
            }

            NativeMemory.AlignedFree(dataPtr);
            return finalImage;
        }
    }
}
