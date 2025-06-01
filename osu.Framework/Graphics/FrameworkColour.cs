// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osuTK.Graphics;

namespace osu.Framework.Graphics
{
    public static class FrameworkColour
    {
        public static Colour4 Background1 => getColor(.2f, .12f);
        public static Colour4 Background2 => getColor(.2f, .18f);
        public static Colour4 Background3 => getColor(.2f, .24f);

        public static Colour4 Primary => getColor(1f, .7f);
        public static Colour4 Secondary => getColor(.6f, .7f);
        public static Colour4 Text => getColor(.1f, .98f);

        private static Colour4 getColor(float saturation, float lightness) => Colour4.FromHSL(220 / 360f, saturation, lightness);

        public static Color4 GreenDarker => Background1;

        public static Color4 BlueGreenDark => Background2;
        public static Color4 GreenDark => Background2;

        public static Color4 Blue => Background3;
        public static Color4 BlueDark => Background3;
        public static Color4 BlueGreen => Background3;
        public static Color4 Green => Background3;

        public static Color4 Yellow => Primary;
        public static Color4 YellowGreen => Secondary;
    }
}
