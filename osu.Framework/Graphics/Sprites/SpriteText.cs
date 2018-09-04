// Copyright (c) 2007-2018 ppy Pty Ltd <contact@ppy.sh>.
// Licensed under the MIT Licence - https://raw.githubusercontent.com/ppy/osu-framework/master/LICENCE

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Caching;
using osu.Framework.Configuration;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Graphics.Textures;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.IO.Stores;
using osu.Framework.MathUtils;
using OpenTK;
using OpenTK.Graphics;

namespace osu.Framework.Graphics.Sprites
{
    public class SpriteText : Drawable, IHasCurrentValue<string>, IHasLineBaseHeight, IHasText, IHasFilterTerms, IFillFlowContainer
    {
        private const float default_text_size = 20;
        private static readonly Vector2 shadow_offset = new Vector2(0, 0.06f);
        private static readonly char[] default_fixed_width_exceptions = { '.', ':', ',' };

        private string text;

        /// <summary>
        /// Gets or sets the text to be displayed.
        /// </summary>
        public string Text
        {
            get => text;
            set
            {
                if (text == value)
                    return;
                text = value;

                charactersCache.Invalidate();
            }
        }

        private float textSize = default_text_size;

        /// <summary>
        /// The size of the text in local space. This means that if TextSize is set to 16, a single line will have a height of 16.
        /// </summary>
        public float TextSize
        {
            get => textSize;
            set
            {
                if (textSize == value)
                    return;
                textSize = value;

                charactersCache.Invalidate();
                shadowOffsetCache.Invalidate();
            }
        }

        private string font;

        /// <summary>
        /// The name of the font to use when looking up textures for the individual characters.
        /// </summary>
        public string Font
        {
            get => font;
            set
            {
                if (font == value)
                    return;
                font = value;

                charactersCache.Invalidate();
            }
        }

        private bool allowMultiline = true;

        /// <summary>
        /// True if the text should be wrapped if it gets too wide. Note that \n does NOT cause a line break. If you need explicit line breaks, use <see cref="TextFlowContainer"/> instead.
        /// </summary>
        public bool AllowMultiline
        {
            get => allowMultiline;
            set
            {
                if (allowMultiline == value)
                    return;
                allowMultiline = value;

                charactersCache.Invalidate();
            }
        }

        private bool shadow;

        /// <summary>
        /// True if a shadow should be displayed around the text.
        /// </summary>
        public bool Shadow
        {
            get => shadow;
            set
            {
                if (shadow == value)
                    return;
                shadow = value;

                Invalidate(Invalidation.DrawNode);
            }
        }

        private Color4 shadowColour = new Color4(0, 0, 0, 0.2f);

        /// <summary>
        /// The colour of the shadow displayed around the text. A shadow will only be displayed if the <see cref="Shadow"/> property is set to true.
        /// </summary>
        public Color4 ShadowColour
        {
            get => shadowColour;
            set
            {
                if (shadowColour == value)
                    return;
                shadowColour = value;

                Invalidate(Invalidation.DrawNode);
            }
        }

        private bool useFullGlyphHeight = true;

        /// <summary>
        /// True if the <see cref="SpriteText"/>'s vertical size should be equal to <see cref="TextSize"/> (the full height) or precisely the size of used characters.
        /// Set to false to allow better centering of individual characters/numerals/etc.
        /// </summary>
        public bool UseFullGlyphHeight
        {
            get => useFullGlyphHeight;
            set
            {
                if (useFullGlyphHeight == value)
                    return;
                useFullGlyphHeight = value;

                charactersCache.Invalidate();
            }
        }

        private bool fixedWidth;

        /// <summary>
        /// True if all characters should be spaced apart the same distance.
        /// </summary>
        public bool FixedWidth
        {
            get => fixedWidth;
            set
            {
                if (fixedWidth == value)
                    return;
                fixedWidth = value;

                charactersCache.Invalidate();
            }
        }

        /// <summary>
        /// An array of characters which should not get a fixed width in a <see cref="FixedWidth"/> instance.
        /// </summary>
        protected virtual char[] FixedWidthExceptionCharacters => default_fixed_width_exceptions;

        [Resolved]
        private FontStore store { get; set; }

        private float spaceWidth;

        [BackgroundDependencyLoader]
        private void load(ShaderManager shaders)
        {
            spaceWidth = GetTextureForCharacter('.')?.DisplayWidth * 2 ?? default_text_size;
            sharedData.TextureShader = shaders?.Load(VertexShaderDescriptor.TEXTURE_2, FragmentShaderDescriptor.TEXTURE);
            sharedData.RoundedTextureShader = shaders?.Load(VertexShaderDescriptor.TEXTURE_2, FragmentShaderDescriptor.TEXTURE_ROUNDED);
        }

        #region Sizing

        private float? explicitWidth;

        /// <summary>
        /// Gets or sets the width of this <see cref="SpriteText"/>. The <see cref="SpriteText"/> will maintain this width when set.
        /// </summary>
        public override float Width
        {
            get => base.Width;
            set
            {
                if (explicitWidth == value)
                    return;

                base.Width = value;
                explicitWidth = value;

                charactersCache.Invalidate();
            }
        }

        private float? explicitHeight;

        /// <summary>
        /// Gets or sets the height of this <see cref="SpriteText"/>. The <see cref="SpriteText"/> will maintain this height when set.
        /// </summary>
        public override float Height
        {
            get => base.Height;
            set
            {
                if (explicitHeight == value)
                    return;

                base.Height = value;
                explicitHeight = value;

                charactersCache.Invalidate();
            }
        }

        /// <summary>
        /// Gets or sets the size of this <see cref="SpriteText"/>. The <see cref="SpriteText"/> will maintain this size when set.
        /// </summary>
        public override Vector2 Size
        {
            get => new Vector2(Width, Height);
            set
            {
                Width = value.X;
                Height = value.Y;
            }
        }

        private Vector2 spacing;

        /// <summary>
        /// Gets or sets the spacing between characters of this <see cref="SpriteText"/>.
        /// </summary>
        public Vector2 Spacing
        {
            get => spacing;
            set
            {
                if (spacing == value)
                    return;
                spacing = value;

                charactersCache.Invalidate();
            }
        }

        private MarginPadding padding;

        /// <summary>
        /// Shrinks the space which may be occupied by characters of this <see cref="SpriteText"/> by the specified amount on each side.
        /// </summary>
        public MarginPadding Padding
        {
            get => padding;
            set
            {
                if (padding.Equals(value))
                    return;

                if (!Validation.IsFinite(value)) throw new ArgumentException($@"{nameof(Padding)} must be finite, but is {value}.");

                padding = value;

                charactersCache.Invalidate();
            }
        }

        #endregion

        #region Characters

        private Cached charactersCache = new Cached();
        private readonly List<CharacterPart> characters = new List<CharacterPart>();

        protected override void Update()
        {
            base.Update();

            if (!charactersCache.IsValid)
            {
                computeCharacters();
                charactersCache.Validate();
            }
        }

        private void computeCharacters()
        {
            characters.Clear();

            Vector2 currentPos = new Vector2(Padding.Left, Padding.Top);

            try
            {
                if (string.IsNullOrEmpty(Text))
                    return;

                float maxWidth = float.PositiveInfinity;
                if ((RelativeSizeAxes & Axes.X) > 0 || explicitWidth != null)
                    maxWidth = DrawWidth - Padding.Right;

                float currentRowHeight = 0;

                foreach (var character in Text)
                {
                    // Unscaled size (i.e. not multiplied by TextSize)
                    Vector2 textureSize;
                    Texture texture = null;

                    // Retrieve the texture + size
                    if (char.IsWhiteSpace(character))
                    {
                        float size = FixedWidth ? constantWidth : spaceWidth;

                        if (character == 0x3000)
                        {
                            // Double-width space
                            size *= 2;
                        }

                        textureSize = new Vector2(size);
                    }
                    else
                    {
                        texture = GetTextureForCharacter(character);
                        textureSize = new Vector2(texture.DisplayWidth, texture.DisplayHeight);
                    }

                    bool useFixedWidth = FixedWidth && !FixedWidthExceptionCharacters.Contains(character);

                    // Scaled glyph size to be used for positioning
                    Vector2 glyphSize = new Vector2(useFixedWidth ? constantWidth : textureSize.X, UseFullGlyphHeight ? 1 : textureSize.Y) * TextSize;

                    // Texture size scaled by TextSize
                    Vector2 scaledTextureSize = textureSize * TextSize;

                    // Check if we need to go onto the next line
                    if (AllowMultiline && currentPos.X + glyphSize.X >= maxWidth)
                    {
                        currentPos.X = Padding.Left;
                        currentPos.Y += currentRowHeight + spacing.Y;
                        currentRowHeight = 0;
                    }

                    // The height of the row depends on whether we want to use the full glyph height or not
                    currentRowHeight = Math.Max(currentRowHeight, glyphSize.Y);

                    if (char.IsWhiteSpace(character))
                        currentPos.X += glyphSize.X + spacing.X;
                    else
                    {
                        float offset = (glyphSize.X - scaledTextureSize.X) / 2;
                        var drawQuad = ToScreenSpace(new RectangleF(new Vector2(currentPos.X + offset, currentPos.Y), scaledTextureSize));

                        characters.Add(new CharacterPart
                        {
                            Texture = texture,
                            DrawQuad = drawQuad
                        });

                        currentPos.X += glyphSize.X + spacing.X;
                    }
                }

                // When we added the last character, we also added the spacing, but we should remove it to get the correct size
                currentPos.X -= spacing.X;

                // The last row needs to be included in the height
                currentPos.Y += currentRowHeight;
            }
            finally
            {
                if (explicitWidth == null && (RelativeSizeAxes & Axes.X) == 0)
                    base.Width = characters.Count == 0 ? 0 : currentPos.X + Padding.Right;
                if (explicitHeight == null && (RelativeSizeAxes & Axes.Y) == 0)
                    base.Height = characters.Count == 0 ? 0 : currentPos.Y + Padding.Bottom;

                Invalidate(Invalidation.DrawNode, shallPropagate: false);
            }
        }

        private Cached<float> constantWidthCache;
        private float constantWidth => constantWidthCache.IsValid ? constantWidthCache.Value : (constantWidthCache.Value = GetTextureForCharacter('D')?.DisplayWidth ?? 0);

        private Cached<Vector2> shadowOffsetCache;
        private Vector2 shadowOffset => shadowOffsetCache.IsValid ? shadowOffsetCache.Value : (shadowOffsetCache.Value = ToScreenSpace(shadow_offset * TextSize) - ToScreenSpace(Vector2.Zero));

        #endregion

        #region Invalidation

        public override bool Invalidate(Invalidation invalidation = Invalidation.All, Drawable source = null, bool shallPropagate = true)
        {
            if (source == Parent && (invalidation & Invalidation.DrawInfo) > 0)
            {
                charactersCache.Invalidate();
                shadowOffsetCache.Invalidate();
            }

            return base.Invalidate(invalidation, source, shallPropagate);
        }

        #endregion

        #region DrawNode

        private readonly SpriteTextDrawNodeSharedData sharedData = new SpriteTextDrawNodeSharedData();

        protected override DrawNode CreateDrawNode() => new SpriteTextDrawNode();

        protected override void ApplyDrawNode(DrawNode node)
        {
            base.ApplyDrawNode(node);

            var n = (SpriteTextDrawNode)node;

            n.Shared = sharedData;

            n.Parts.Clear();
            n.Parts.AddRange(characters);

            n.Shadow = Shadow;
            n.ShadowColour = ShadowColour;

            if (Shadow)
                n.ShadowOffset = shadowOffset;
        }

        #endregion

        /// <summary>
        /// Gets the texture for the given character.
        /// </summary>
        /// <param name="c">The character to get the texture for.</param>
        /// <returns>The texture for the given character.</returns>
        protected Texture GetTextureForCharacter(char c)
        {
            if (store == null)
                return null;

            return store.Get(getTextureName(c)) ?? store.Get(getTextureName(c, false));
        }

        private string getTextureName(char c, bool useFont = true) => !useFont || string.IsNullOrEmpty(Font) ? c.ToString() : $@"{Font}/{c}";

        public override string ToString()
        {
            return $@"""{Text}"" " + base.ToString();
        }

        private Bindable<string> current;

        /// <summary>
        /// Implements the <see cref="IHasCurrentValue{T}"/> interface.
        /// </summary>
        public Bindable<string> Current
        {
            get => current;
            set
            {
                if (current != null)
                    current.ValueChanged -= setText;

                if (value != null)
                {
                    value.ValueChanged += setText;
                    value.TriggerChange();
                }

                current = value;

                void setText(string t) => Text = t;
            }
        }

        public float LineBaseHeight
        {
            get
            {
                var baseHeight = store.GetBaseHeight(Font);
                if (baseHeight.HasValue)
                    return baseHeight.Value * TextSize;

                if (string.IsNullOrEmpty(Text))
                    return 0;

                return store.GetBaseHeight(Text[0]).GetValueOrDefault() * TextSize;
            }
        }

        public IEnumerable<string> FilterTerms
        {
            get { yield return Text; }
        }
    }
}
