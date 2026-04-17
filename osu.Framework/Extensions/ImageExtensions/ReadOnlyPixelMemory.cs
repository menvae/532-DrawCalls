// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using NetVips;

namespace osu.Framework.Extensions.ImageExtensions
{
    public struct ReadOnlyPixelMemory : IDisposable
    {
        private Image? image;
        private byte[]? memory;

        internal ReadOnlyPixelMemory(Image image)
        {
            this.image = image;
            this.memory = image.WriteToMemory<byte>();
        }

        /// <summary>
        /// The span of pixels.
        /// </summary>
        public ReadOnlySpan<byte> Span
        {
            get
            {
                // Occurs when this struct has been default-initialised (the struct itself doesn't accept a nullable image).
                if (image == null || memory == null)
                    return ReadOnlySpan<byte>.Empty;

                Debug.Assert(memory != null);
                return memory;
            }
        }

        public void Dispose()
        {
            image = null;
            memory = null;
        }
    }
}
