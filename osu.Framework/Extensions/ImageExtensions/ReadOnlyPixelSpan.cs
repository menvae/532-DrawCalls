// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NetVips;

namespace osu.Framework.Extensions.ImageExtensions
{
    public readonly ref struct ReadOnlyPixelSpan
    {
        /// <summary>
        /// The span of pixels.
        /// </summary>
        public readonly ReadOnlySpan<byte> Span;

        internal ReadOnlyPixelSpan(Image image)
        {
            Span = image.WriteToMemory<byte>();
        }

        public void Dispose()
        {
            // The byte array is (hopefully) managed by GC.
            // This is only left for code compatibility
        }
    }
}
