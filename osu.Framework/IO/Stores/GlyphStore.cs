// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using NetVips;
using osu.Framework.Extensions;
using osu.Framework.Graphics.Textures;
using osu.Framework.Logging;
using osu.Framework.Text;
using SharpFNT;
using Image = NetVips.Image;

namespace osu.Framework.IO.Stores
{
    /// <summary>
    /// A basic glyph store that will load font sprite sheets every character retrieval.
    /// </summary>
    public class GlyphStore : IResourceStore<TextureUpload>, IGlyphStore
    {
        protected readonly string AssetName;

        protected readonly IResourceStore<TextureUpload> TextureLoader;

        public string FontName { get; }

        public float? Baseline => Font?.Common.Base;

        protected readonly ResourceStore<byte[]> Store;

        [CanBeNull]
        protected BitmapFont Font => completionSource.Task.GetResultSafely();

        private readonly TaskCompletionSource<BitmapFont> completionSource = new TaskCompletionSource<BitmapFont>();

        /// <summary>
        /// This is a rare usage of a static framework-wide cache.
        /// In normal execution font instances are held locally by font stores and this will add no overhead or improvement.
        /// It exists specifically to avoid overheads of parsing fonts repeatedly in unit tests.
        /// </summary>
        private static readonly ConcurrentDictionary<string, BitmapFont> font_cache = new ConcurrentDictionary<string, BitmapFont>();

        /// <summary>
        /// Create a new glyph store.
        /// </summary>
        /// <param name="store">The store to provide font resources.</param>
        /// <param name="assetName">The base name of the font.</param>
        /// <param name="textureLoader">An optional platform-specific store for loading textures. Should load for the store provided in <param ref="param"/>.</param>
        public GlyphStore(ResourceStore<byte[]> store, string assetName = null, IResourceStore<TextureUpload> textureLoader = null)
        {
            Store = new ResourceStore<byte[]>(store);

            Store.AddExtension("fnt");
            Store.AddExtension("bin");

            AssetName = assetName;
            TextureLoader = textureLoader;

            FontName = assetName?.Split('/').Last() ?? string.Empty;
        }

        private Task fontLoadTask;

        public Task LoadFontAsync() => fontLoadTask ??= Task.Factory.StartNew(() =>
        {
            try
            {
                BitmapFont font;

                using (var s = Store.GetStream($@"{AssetName}"))
                {
                    string hash = s.ComputeMD5Hash();

                    if (font_cache.TryGetValue(hash, out font))
                    {
                        Logger.Log($"Cached font load for {AssetName}");
                    }
                    else
                    {
                        font_cache.TryAdd(hash, font = BitmapFont.FromStream(s, FormatHint.Binary, false));
                    }
                }

                completionSource.SetResult(font);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Couldn't load font asset from {AssetName}.");
                completionSource.SetResult(null);
                throw;
            }
        }, TaskCreationOptions.PreferFairness);

        public bool HasGlyph(char c) => Font?.Characters.ContainsKey(c) == true;

        protected virtual TextureUpload GetPageImage(int page)
        {
            if (TextureLoader != null)
                return TextureLoader.Get(GetFilenameForPage(page));

            using (var stream = Store.GetStream(GetFilenameForPage(page)))
                return new TextureUpload(stream);
        }

        protected string GetFilenameForPage(int page)
        {
            Debug.Assert(Font != null);
            return $@"{AssetName}_{page.ToString().PadLeft((Font.Pages.Count - 1).ToString().Length, '0')}.png";
        }

        public CharacterGlyph Get(char character)
        {
            if (Font == null)
                return null;

            Debug.Assert(Baseline != null);

            var bmCharacter = Font.GetCharacter(character);

            return new CharacterGlyph(character, bmCharacter.XOffset, bmCharacter.YOffset, bmCharacter.XAdvance, Baseline.Value, this);
        }

        public int GetKerning(char left, char right) => Font?.GetKerningAmount(left, right) ?? 0;

        Task<CharacterGlyph> IResourceStore<CharacterGlyph>.GetAsync(string name, CancellationToken cancellationToken) =>
            Task.Run(() => ((IGlyphStore)this).Get(name[0]), cancellationToken);

        CharacterGlyph IResourceStore<CharacterGlyph>.Get(string name) => Get(name[0]);

        public TextureUpload Get(string name)
        {
            if (Font == null) return null;

            if (name.Length > 1 && !name.StartsWith($@"{FontName}/", StringComparison.Ordinal))
                return null;

            return Font.Characters.TryGetValue(name.Last(), out Character c) ? LoadCharacter(c) : null;
        }

        public virtual async Task<TextureUpload> GetAsync(string name, CancellationToken cancellationToken = default)
        {
            if (name.Length > 1 && !name.StartsWith($@"{FontName}/", StringComparison.Ordinal))
                return null;

            var bmFont = await completionSource.Task.ConfigureAwait(false);

            return bmFont.Characters.TryGetValue(name.Last(), out Character c)
                ? LoadCharacter(c)
                : null;
        }

        protected int LoadedGlyphCount;

        protected virtual TextureUpload LoadCharacter(Character character)
        {
            var pageUpload = GetPageImage(character.Page);
            LoadedGlyphCount++;

            using var pageImage = Image.NewFromMemoryCopy(pageUpload.Data, pageUpload.Width, pageUpload.Height, 4, Enums.BandFormat.Uchar);

            if (pageImage == null)
                return new TextureUpload();

            // the spritesheet may have unused pixels trimmed
            int readableWidth = Math.Max(0, Math.Min(character.Width, pageImage.Width - character.X));
            int readableHeight = Math.Max(0, Math.Min(character.Height, pageImage.Height - character.Y));

            Image glyph;

            if (readableWidth > 0 && readableHeight > 0)
            {
                glyph = pageImage.Crop(character.X, character.Y, readableWidth, readableHeight);

                if (readableWidth < character.Width || readableHeight < character.Height)
                {
                    var background = new double[] { 255, 255, 255, 0 };

                    var padded = glyph.Embed(0, 0, character.Width, character.Height,
                        extend: Enums.Extend.Background,
                        background: background);

                    glyph.Dispose();
                    glyph = padded;
                }
            }
            else
            {
                glyph = Image.Black(1, 1)
                             .Linear(new[] { 0.0 }, new double[] { 255, 255, 255, 0 })
                             .Embed(0, 0, character.Width, character.Height, extend: Enums.Extend.Copy);
            }

            return new TextureUpload(glyph);
        }

        public Stream GetStream(string name) => throw new NotSupportedException();

        public IEnumerable<string> GetAvailableResources() => Font?.Characters.Keys.Select(k => $"{FontName}/{(char)k}") ?? Enumerable.Empty<string>();

        #region IDisposable Support

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
        }

        #endregion
    }
}
