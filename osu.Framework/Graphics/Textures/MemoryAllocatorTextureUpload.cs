// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Buffers;
using osu.Framework.Graphics.Primitives;
using osuTK.Graphics.ES30;

namespace osu.Framework.Graphics.Textures
{
    public class MemoryAllocatorTextureUpload : ITextureUpload
    {
        public Span<byte> RawData => memoryOwner.Memory.Span[..dataLength];

        public ReadOnlySpan<byte> Data => RawData;

        private readonly IMemoryOwner<byte> memoryOwner;
        private readonly int dataLength;

        public int Level { get; set; }

        public virtual PixelFormat Format => PixelFormat.Rgba;

        public RectangleI Bounds { get; set; }

        /// <summary>
        /// Create an empty raw texture with an efficient shared memory backing.
        /// </summary>
        /// <param name="width">The width of the texture.</param>
        /// <param name="height">The height of the texture.</param>
        /// <param name="memoryAllocator">The source to retrieve memory from. Shared default is used if null.</param>
        public MemoryAllocatorTextureUpload(int width, int height, MemoryPool<byte> memoryPool = null)
        {
            dataLength = width * height * 4;
            memoryOwner = (memoryPool ?? MemoryPool<byte>.Shared).Rent(dataLength);
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

            memoryOwner?.Dispose();

            disposed = true;
        }

        #endregion
    }
}
