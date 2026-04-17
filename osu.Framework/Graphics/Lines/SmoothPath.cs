// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NetVips;
using osu.Framework.Allocation;
using osu.Framework.Caching;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Textures;
using osuTK.Graphics;
using Image = NetVips.Image;

namespace osu.Framework.Graphics.Lines
{
    public partial class SmoothPath : Path
    {
        [Resolved]
        private IRenderer renderer { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            validateTexture();
        }

        public override float PathRadius
        {
            get => base.PathRadius;
            set
            {
                if (base.PathRadius == value)
                    return;

                base.PathRadius = value;

                InvalidateTexture();
            }
        }

        private readonly Cached textureCache = new Cached();

        protected void InvalidateTexture()
        {
            textureCache.Invalidate();
            Invalidate(Invalidation.DrawNode);
        }

        private void validateTexture()
        {
            if (textureCache.IsValid)
                return;

            int textureWidth = (int)Math.Max(PathRadius, 1) * 2;
            const double aa_portion = 0.02;

            using var ramp = Image.Identity(textureWidth);
            using var progress = ramp.Linear(new[] { 1.0 / (textureWidth - 1) }, new[] { 0.0 });

            using var lut = createColorLut(textureWidth);
            using var lutIndex = progress.Linear(new[] { (double)textureWidth - 1 }, new[] { 0.0 });
            var raw = lutIndex.Maplut(lut);

            using var aaMask = progress.Linear(new[] { 1.0 / aa_portion }, new[] { 0.0 });

            using var one = progress.NewFromImage(1.0);
            using var condition = aaMask.Relational(one, Enums.OperationRelational.More);

            using var clampedMask = condition.Ifthenelse(1.0, aaMask);

            if (raw.Bands == 4)
            {
                using var alpha = raw[3].Multiply(clampedMask);
                using var colorBands = raw.ExtractBand(0, 3);
                var merged = colorBands.Bandjoin(alpha);
                raw.Dispose();
                raw = merged;
            }

            if (Texture?.Width == textureWidth)
            {
                Texture.SetData(new TextureUpload(raw));
            }
            else
            {
                var texture = new DisposableTexture(renderer.CreateTexture(textureWidth, 1, true));
                texture.SetData(new TextureUpload(raw));
                Texture = texture;
            }

            textureCache.Validate();
        }

        private Image createColorLut(int width)
        {
            var pixels = new byte[width * 4];

            for (int i = 0; i < width; i++)
            {
                float progress = (float)i / (width - 1);
                var c = ColourAt(progress);

                pixels[i * 4 + 0] = (byte)c.R;
                pixels[i * 4 + 1] = (byte)c.G;
                pixels[i * 4 + 2] = (byte)c.B;
                pixels[i * 4 + 3] = (byte)c.A;
            }

            return Image.NewFromMemory(pixels, width, 1, 4, Enums.BandFormat.Uchar);
        }

        internal override DrawNode GenerateDrawNodeSubtree(ulong frame, int treeIndex, bool forceNewDrawNode)
        {
            validateTexture();
            return base.GenerateDrawNodeSubtree(frame, treeIndex, forceNewDrawNode);
        }

        /// <summary>
        /// Retrieves the colour from a position in the texture of the <see cref="Path"/>.
        /// </summary>
        /// <param name="position">The position within the texture. 0 indicates the outermost-point of the path, 1 indicates the centre of the path.</param>
        protected virtual Color4 ColourAt(float position) => Color4.White;
    }
}
