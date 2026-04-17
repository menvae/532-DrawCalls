// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Image = NetVips.Image;

namespace osu.Framework.Platform
{
    /// <summary>
    /// Virtual clipboard for use in headless execution.
    /// </summary>
    /// <remarks>
    /// Stores all data in-memory, so the host OS clipboard is not affected.
    /// </remarks>
    public class HeadlessClipboard : Clipboard
    {
        private string? clipboardText;
        private Image? clipboardImage;

        public override string? GetText() => clipboardText;

        public override void SetText(string text)
        {
            clipboardImage = null;
            clipboardText = text;
        }

        public override Image? GetImage() => clipboardImage?.Copy();

        public override bool SetImage(Image image)
        {
            clipboardText = null;
            clipboardImage = image;
            return true;
        }
    }
}
