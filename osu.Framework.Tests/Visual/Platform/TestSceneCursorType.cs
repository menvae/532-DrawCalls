// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Platform;
using osuTK.Graphics;

namespace osu.Framework.Tests.Visual.Platform
{
    public partial class TestSceneCursorType : FrameworkTestScene
    {
        [Resolved]
        private GameHost host { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            var all = Enum.GetValues<CursorType>();

            int half = all.Length / 2;
            var top = all.Take(all.Length / 2);
            var bottom = all.Skip(half);

            Child = new CursorTypeContainer
            {
                RelativeSizeAxes = Axes.Both,
                Child = new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Content = new[]
                    {
                        top.Select(x => new CursorBox(x)).ToArray(),
                        bottom.Select(x => new CursorBox(x)).ToArray(),
                    }
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            host.Window.CursorState = CursorState.Default;
        }

        protected override void Dispose(bool isDisposing)
        {
            host.Window.CursorState = CursorState.Hidden;
            base.Dispose(isDisposing);
        }

        private partial class CursorBox : CompositeDrawable, IHasCursorType
        {
            public CursorType Cursor { get; }

            public CursorBox(CursorType type)
            {
                RelativeSizeAxes = Axes.Both;
                Cursor = type;

                InternalChildren = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = (int)type % 2 == 0 ? Color4.Red : Color4.Green,
                        Alpha = 0.2f
                    },
                    new SpriteText
                    {
                        Text = type.ToString(),
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre
                    }
                };
            }

            protected override bool OnHover(HoverEvent e) => true;
        }
    }
}
