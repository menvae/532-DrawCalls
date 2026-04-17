// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Runtime.CompilerServices;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osuTK.Graphics;

namespace osu.Framework.Graphics.Textures
{
    public partial class TextureAtlas
    {
        /// <summary>
        /// A texture which is acting as the backing for an atlas.
        /// </summary>
        private class BackingAtlasTexture : Texture
        {
            /// <summary>
            /// The amount of padding around each texture in the atlas.
            /// </summary>
            private readonly int padding;

            private readonly RectangleI atlasBounds;

#pragma warning disable IDE0052
            // Keep a reference to the parent for the texture visualiser.
            // ReSharper disable once NotAccessedField.Local
            private readonly Texture parent;
#pragma warning restore IDE0052

            private const byte init_r = 0;
            private const byte init_g = 0;
            private const byte init_b = 0;

            private const int bytes_per_pixel = 4;

            public BackingAtlasTexture(IRenderer renderer, int width, int height, bool manualMipmaps, TextureFilteringMode filteringMode = TextureFilteringMode.Linear, int padding = 0)
                : this(renderer.CreateTexture(width, height, manualMipmaps, filteringMode, initialisationColour: new Color4(init_r, init_g, init_b, 0)))
            {
                this.padding = padding;
                atlasBounds = new RectangleI(0, 0, Width, Height);
            }

            private BackingAtlasTexture(Texture parent)
                : base(parent)
            {
                this.parent = parent;
                IsAtlasTexture = parent.IsAtlasTexture = true;
            }

            internal override void SetData(ITextureUpload upload, WrapMode wrapModeS, WrapMode wrapModeT, Opacity? opacity)
            {
                // Can only perform padding when the bounds are a sub-part of the texture
                RectangleI middleBounds = upload.Bounds;

                if (middleBounds.IsEmpty || middleBounds.Width * middleBounds.Height * bytes_per_pixel > upload.Data.Length)
                {
                    // For a texture atlas, we don't care about opacity, so we avoid
                    // any computations related to it by assuming it to be mixed.
                    base.SetData(upload, wrapModeS, wrapModeT, Opacity.Mixed);
                    return;
                }

                int actualPadding = padding / (1 << upload.Level);

                var data = upload.Data;

                uploadCornerPadding(data, middleBounds, actualPadding, wrapModeS != WrapMode.None && wrapModeT != WrapMode.None);
                uploadHorizontalPadding(data, middleBounds, actualPadding, wrapModeS != WrapMode.None);
                uploadVerticalPadding(data, middleBounds, actualPadding, wrapModeT != WrapMode.None);

                // Upload the middle part of the texture
                // For a texture atlas, we don't care about opacity, so we avoid
                // any computations related to it by assuming it to be mixed.
                base.SetData(upload, wrapModeS, wrapModeT, Opacity.Mixed);
            }

            private void uploadVerticalPadding(ReadOnlySpan<byte> upload, RectangleI middleBounds, int actualPadding, bool fillOpaque)
            {
                RectangleI[] sideBoundsArray =
                {
                    new RectangleI(middleBounds.X, middleBounds.Y - actualPadding, middleBounds.Width, actualPadding).Intersect(atlasBounds), // Top
                    new RectangleI(middleBounds.X, middleBounds.Y + middleBounds.Height, middleBounds.Width, actualPadding).Intersect(atlasBounds), // Bottom
                };

                int[] sideIndices =
                {
                    0, // Top
                    (middleBounds.Height - 1) * middleBounds.Width, // Bottom
                };

                for (int i = 0; i < 2; ++i)
                {
                    RectangleI sideBounds = sideBoundsArray[i];

                    if (!sideBounds.IsEmpty)
                    {
                        bool allTransparent = true;
                        int index = sideIndices[i];

                        var sideUpload = new MemoryAllocatorTextureUpload(sideBounds.Width, sideBounds.Height) { Bounds = sideBounds };
                        var data = sideUpload.RawData;

                        for (int y = 0; y < sideBounds.Height; ++y)
                        {
                            for (int x = 0; x < sideBounds.Width; ++x)
                            {
                                int pixel = index + x;
                                allTransparent &= checkEdgeRGB(upload, pixel);

                                transferBorderPixel(data, y * sideBounds.Width + x, upload, pixel, fillOpaque);
                            }
                        }

                        // Only upload padding if the border isn't completely transparent.
                        if (!allTransparent)
                        {
                            // For a texture atlas, we don't care about opacity, so we avoid
                            // any computations related to it by assuming it to be mixed.
                            base.SetData(sideUpload, WrapMode.None, WrapMode.None, Opacity.Mixed);
                        }
                    }
                }
            }

            private void uploadHorizontalPadding(ReadOnlySpan<byte> upload, RectangleI middleBounds, int actualPadding, bool fillOpaque)
            {
                RectangleI[] sideBoundsArray =
                {
                    new RectangleI(middleBounds.X - actualPadding, middleBounds.Y, actualPadding, middleBounds.Height).Intersect(atlasBounds), // Left
                    new RectangleI(middleBounds.X + middleBounds.Width, middleBounds.Y, actualPadding, middleBounds.Height).Intersect(atlasBounds), // Right
                };

                int[] sideIndices =
                {
                    0, // Left
                    middleBounds.Width - 1, // Right
                };

                for (int i = 0; i < 2; ++i)
                {
                    RectangleI sideBounds = sideBoundsArray[i];

                    if (!sideBounds.IsEmpty)
                    {
                        bool allTransparent = true;
                        int index = sideIndices[i];

                        var sideUpload = new MemoryAllocatorTextureUpload(sideBounds.Width, sideBounds.Height) { Bounds = sideBounds };
                        var data = sideUpload.RawData;

                        int stride = middleBounds.Width;

                        for (int y = 0; y < sideBounds.Height; ++y)
                        {
                            for (int x = 0; x < sideBounds.Width; ++x)
                            {
                                int pixel = index + y * stride;

                                allTransparent &= checkEdgeRGB(upload, pixel);

                                transferBorderPixel(data, y * sideBounds.Width + x, upload, pixel, fillOpaque);
                            }
                        }

                        // Only upload padding if the border isn't completely transparent.
                        if (!allTransparent)
                        {
                            // For a texture atlas, we don't care about opacity, so we avoid
                            // any computations related to it by assuming it to be mixed.
                            base.SetData(sideUpload, WrapMode.None, WrapMode.None, Opacity.Mixed);
                        }
                    }
                }
            }

            private void uploadCornerPadding(ReadOnlySpan<byte> upload, RectangleI middleBounds, int actualPadding, bool fillOpaque)
            {
                RectangleI[] cornerBoundsArray =
                {
                    new RectangleI(middleBounds.X - actualPadding, middleBounds.Y - actualPadding, actualPadding, actualPadding).Intersect(atlasBounds), // TopLeft
                    new RectangleI(middleBounds.X + middleBounds.Width, middleBounds.Y - actualPadding, actualPadding, actualPadding).Intersect(atlasBounds), // TopRight
                    new RectangleI(middleBounds.X - actualPadding, middleBounds.Y + middleBounds.Height, actualPadding, actualPadding).Intersect(atlasBounds), // BottomLeft
                    new RectangleI(middleBounds.X + middleBounds.Width, middleBounds.Y + middleBounds.Height, actualPadding, actualPadding).Intersect(atlasBounds), // BottomRight
                };

                int[] cornerIndices =
                {
                    0, // TopLeft
                    middleBounds.Width - 1, // TopRight
                    (middleBounds.Height - 1) * middleBounds.Width, // BottomLeft
                    (middleBounds.Height - 1) * middleBounds.Width + middleBounds.Width - 1, // BottomRight
                };

                for (int i = 0; i < 4; ++i)
                {
                    RectangleI cornerBounds = cornerBoundsArray[i];
                    int nCornerPixels = cornerBounds.Width * cornerBounds.Height;
                    int pixel = cornerIndices[i];

                    // Only upload if we have a non-zero size and if the colour isn't already transparent white
                    if (nCornerPixels > 0 && !checkEdgeRGB(upload, pixel))
                    {
                        var cornerUpload = new MemoryAllocatorTextureUpload(cornerBounds.Width, cornerBounds.Height) { Bounds = cornerBounds };
                        var data = cornerUpload.RawData;

                        for (int j = 0; j < nCornerPixels; ++j)
                            transferBorderPixel(data, j, upload, pixel, fillOpaque);

                        // For a texture atlas, we don't care about opacity, so we avoid
                        // any computations related to it by assuming it to be mixed.
                        base.SetData(cornerUpload, WrapMode.None, WrapMode.None, Opacity.Mixed);
                    }
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void transferBorderPixel(Span<byte> dest, int destPixel, ReadOnlySpan<byte> source, int srcPixel, bool fillOpaque)
            {
                int d = destPixel * bytes_per_pixel;
                int s = srcPixel * bytes_per_pixel;

                dest[d] = source[s]; // R
                dest[d + 1] = source[s + 1]; // G
                dest[d + 2] = source[s + 2]; // B
                dest[d + 3] = fillOpaque ? source[s + 3] : (byte)0; // A
            }

            /// <summary>
            /// Check whether the provided upload edge pixel's RGB components match the initialisation colour.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool checkEdgeRGB(ReadOnlySpan<byte> data, int pixelIndex)
            {
                int offset = pixelIndex * bytes_per_pixel;
                return data[offset] == init_r
                       && data[offset + 1] == init_g
                       && data[offset + 2] == init_b;
            }
        }
    }
}
