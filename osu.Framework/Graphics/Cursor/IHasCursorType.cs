// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Platform;

namespace osu.Framework.Graphics.Cursor
{
    public interface IHasCursorType : IDrawable
    {
        CursorType Cursor { get; }
    }
}
