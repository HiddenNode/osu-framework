// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using osu.Framework.Caching;
using osu.Framework.Graphics.Sprites;
using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.EnumExtensions;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Localisation;
using osuTK;

namespace osu.Framework.Graphics.Containers
{
    /// <summary>
    /// A drawable text object that supports more advanced text formatting.
    /// </summary>
    public class TextFlowContainer : Container
    {
        protected override Container<Drawable> Content => content;
        private readonly TextFillFlowContainer content;

        public virtual IEnumerable<Drawable> FlowingChildren => content.FlowingChildren;

        private readonly Action<SpriteText> defaultCreationParameters;

        private readonly List<ITextPart> parts = new List<ITextPart>();

        private readonly Cached partsCache = new Cached();

        /// <summary>
        /// An indent value for the first (header) line of a paragraph.
        /// </summary>
        public float FirstLineIndent
        {
            get => content.FirstLineIndent;
            set
            {
                if (value == content.FirstLineIndent) return;

                content.FirstLineIndent = value;

                content.Layout.Invalidate();
            }
        }

        /// <summary>
        /// An indent value for all lines proceeding the first line in a paragraph.
        /// </summary>
        public float ContentIndent
        {
            get => content.ContentIndent;
            set
            {
                if (value == content.ContentIndent) return;

                content.ContentIndent = value;

                content.Layout.Invalidate();
            }
        }

        /// <summary>
        /// Vertical space between paragraphs (i.e. text separated by '\n') in multiples of the text size.
        /// The default value is 0.5.
        /// </summary>
        public float ParagraphSpacing
        {
            get => content.ParagraphSpacing;
            set
            {
                if (value == content.ParagraphSpacing) return;

                content.ParagraphSpacing = value;

                content.Layout.Invalidate();
            }
        }

        /// <summary>
        /// Vertical space between lines both when a new paragraph begins and when line wrapping occurs.
        /// Additive with <see cref="ParagraphSpacing"/> on new paragraph. Default value is 0.
        /// </summary>
        public float LineSpacing
        {
            get => content.LineSpacing;
            set
            {
                if (value == content.LineSpacing) return;

                content.LineSpacing = value;

                content.Layout.Invalidate();
            }
        }

        /// <summary>
        /// The <see cref="Anchor"/> which text should flow from.
        /// </summary>
        public Anchor TextAnchor
        {
            get => content.TextAnchor;
            set
            {
                if (content.TextAnchor == value)
                    return;

                content.TextAnchor = value;

                content.Anchor = value;
                content.Origin = value;

                content.Layout.Invalidate();
            }
        }

        /// <summary>
        /// An easy way to set the full text of a text flow in one go.
        /// This will overwrite any existing text added using this method of <see cref="AddText(LocalisableString, Action{SpriteText})"/>
        /// </summary>
        public LocalisableString Text
        {
            set
            {
                Clear();
                parts.Clear();

                AddText(value);
            }
        }

        [Resolved]
        internal LocalisationManager Localisation { get; private set; }

        private readonly Bindable<LocalisationParameters> localisationParameters = new Bindable<LocalisationParameters>();

        public TextFlowContainer(Action<SpriteText> defaultCreationParameters = null)
        {
            content = new TextFillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
            };
            AddInternal(content);

            this.defaultCreationParameters = defaultCreationParameters;
        }

        protected override void LoadAsyncComplete()
        {
            base.LoadAsyncComplete();

            localisationParameters.Value = Localisation.CurrentParameters.Value;
            RecreateAllParts();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            localisationParameters.BindValueChanged(_ => partsCache.Invalidate());
            ((IBindable<LocalisationParameters>)localisationParameters).BindTo(Localisation.CurrentParameters);
        }

        protected void InvalidateLayout()
        {
            content.InvalidateLayout();
            content.Layout.Invalidate();
        }

        protected override void Update()
        {
            base.Update();

            Vector2 maxSize = ChildSize;

            if ((AutoSizeAxes & Axes.X) != 0)
                maxSize.X = 0;

            if ((AutoSizeAxes & Axes.Y) != 0)
                maxSize.Y = 0;

            content.MaximumSize = maxSize;

            if (!partsCache.IsValid)
                RecreateAllParts();
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();

            if (!content.Layout.IsValid)
            {
                content.ComputeLayout();
                content.Layout.Validate();
            }
        }

        protected override int Compare(Drawable x, Drawable y)
        {
            // FillFlowContainer will reverse the ordering of right-anchored words such that the (previously) first word would be
            // the right-most word, whereas it should still be flowed left-to-right. This is achieved by reversing the comparator.
            if (TextAnchor.HasFlagFast(Anchor.x2))
                return base.Compare(y, x);

            return base.Compare(x, y);
        }

        /// <summary>
        /// Add new text to this text flow. The \n character will create a new paragraph, not just a line break.
        /// If you need \n to be a line break, use <see cref="AddParagraph{TSpriteText}(LocalisableString, Action{TSpriteText})"/> instead.
        /// </summary>
        /// <returns>A collection of <see cref="Drawable" /> objects for each <see cref="SpriteText"/> word and <see cref="NewLineContainer"/> created from the given text.</returns>
        /// <param name="text">The text to add.</param>
        /// <param name="creationParameters">A callback providing any <see cref="SpriteText" /> instances created for this new text.</param>
        public ITextPart AddText<TSpriteText>(LocalisableString text, Action<TSpriteText> creationParameters = null)
            where TSpriteText : SpriteText, new()
            => AddPart(CreateChunkFor(text, true, () => new TSpriteText(), creationParameters));

        /// <inheritdoc cref="AddText{TSpriteText}(LocalisableString,Action{TSpriteText})"/>
        public ITextPart AddText(LocalisableString text, Action<SpriteText> creationParameters = null)
            => AddPart(CreateChunkFor(text, true, CreateSpriteText, creationParameters));

        /// <summary>
        /// Add an arbitrary <see cref="SpriteText"/> to this <see cref="TextFlowContainer"/>.
        /// While default creation parameters are applied automatically, word wrapping is unavailable for contained words.
        /// This should only be used when a specialised <see cref="SpriteText"/> type is required.
        /// </summary>
        /// <param name="text">The text to add.</param>
        /// <param name="creationParameters">A callback providing any <see cref="SpriteText" /> instances created for this new text.</param>
        public void AddText<TSpriteText>(TSpriteText text, Action<TSpriteText> creationParameters = null)
            where TSpriteText : SpriteText
        {
            defaultCreationParameters?.Invoke(text);
            creationParameters?.Invoke(text);
            AddPart(new TextPartManual(text.Yield()));
        }

        /// <summary>
        /// Add a new paragraph to this text flow. The \n character will create a line break
        /// If you need \n to be a new paragraph, not just a line break, use <see cref="AddText{TSpriteText}(LocalisableString, Action{TSpriteText})"/> instead.
        /// </summary>
        /// <returns>A collection of <see cref="Drawable" /> objects for each <see cref="SpriteText"/> word and <see cref="NewLineContainer"/> created from the given text.</returns>
        /// <param name="paragraph">The paragraph to add.</param>
        /// <param name="creationParameters">A callback providing any <see cref="SpriteText" /> instances created for this new paragraph.</param>
        public ITextPart AddParagraph<TSpriteText>(LocalisableString paragraph, Action<TSpriteText> creationParameters = null)
            where TSpriteText : SpriteText, new()
            => AddPart(CreateChunkFor(paragraph, false, () => new TSpriteText(), creationParameters));

        /// <inheritdoc cref="AddParagraph{TSpriteText}(LocalisableString,Action{TSpriteText})"/>
        public ITextPart AddParagraph(LocalisableString paragraph, Action<SpriteText> creationParameters = null)
            => AddPart(CreateChunkFor(paragraph, false, CreateSpriteText, creationParameters));

        /// <summary>
        /// Creates an appropriate implementation of <see cref="TextChunk{TSpriteText}"/> for this text flow container type.
        /// </summary>
        protected internal virtual TextChunk<TSpriteText> CreateChunkFor<TSpriteText>(LocalisableString text, bool newLineIsParagraph, Func<TSpriteText> creationFunc, Action<TSpriteText> creationParameters = null)
            where TSpriteText : SpriteText, new()
            => new TextChunk<TSpriteText>(text, newLineIsParagraph, creationFunc, creationParameters);

        /// <summary>
        /// End current line and start a new one.
        /// </summary>
        public void NewLine() => AddPart(new TextNewLine(false));

        /// <summary>
        /// End current paragraph and start a new one.
        /// </summary>
        public void NewParagraph() => AddPart(new TextNewLine(true));

        protected internal virtual SpriteText CreateSpriteText() => new SpriteText();

        internal void ApplyDefaultCreationParamters(SpriteText spriteText) => defaultCreationParameters?.Invoke(spriteText);

        public override void Add(Drawable drawable)
        {
            throw new InvalidOperationException($"Use {nameof(AddText)} to add text to a {nameof(TextFlowContainer)}.");
        }

        public override bool Remove(Drawable drawable, bool disposeImmediately)
        {
            InvalidateLayout();

            return base.Remove(drawable, disposeImmediately);
        }

        public override void Clear(bool disposeChildren)
        {
            InvalidateLayout();

            base.Clear(disposeChildren);
            parts.Clear();
        }

        /// <summary>
        /// Adds an <see cref="ITextPart"/> and its associated drawables to this <see cref="TextFlowContainer"/>.
        /// </summary>
        protected internal ITextPart AddPart(ITextPart part)
        {
            parts.Add(part);

            // if the parts cached is already invalid, there's no need to recreate the new addition. it will be created as part of the next validation.
            if (partsCache.IsValid)
                recreatePart(part);

            return part;
        }

        /// <summary>
        /// Removes an <see cref="ITextPart"/> from this text flow.
        /// </summary>
        /// <returns>Whether <see cref="ITextPart"/> was successfully removed from the flow.</returns>
        public bool RemovePart(ITextPart partToRemove)
        {
            if (!parts.Remove(partToRemove))
                return false;

            partsCache.Invalidate();
            return true;
        }

        protected virtual void RecreateAllParts()
        {
            // manual parts need to be manually removed before clearing contents,
            // to avoid accidentally disposing of them in the process.
            foreach (var manualPart in parts.OfType<TextPartManual>())
                RemoveRange(manualPart.Drawables, false);

            // make sure not to clear the list of parts by accident.
            base.Clear(true);

            foreach (var part in parts)
                recreatePart(part);

            partsCache.Validate();
        }

        private void recreatePart(ITextPart part)
        {
            part.RecreateDrawablesFor(this);

            foreach (var drawable in part.Drawables)
            {
                base.Add(drawable);
            }

            InvalidateLayout();
        }

        public class NewLineContainer : Container
        {
            public readonly bool IndicatesNewParagraph;

            public NewLineContainer(bool newParagraph)
            {
                IndicatesNewParagraph = newParagraph;
            }
        }

        private class TextFillFlowContainer : FillFlowContainer
        {
            protected override bool ForceNewRow(Drawable child) => child is NewLineContainer;

            public readonly Cached Layout = new Cached();

            public new void InvalidateLayout() => base.InvalidateLayout();

            public Anchor TextAnchor = Anchor.TopLeft;

            public float ContentIndent;

            public float ParagraphSpacing = 0.5f;

            public float LineSpacing;

            public float FirstLineIndent;

            protected override void PerformLayout()
            {
                base.PerformLayout();

                ComputeLayout();
                Layout.Validate();
            }

            public void ComputeLayout()
            {
                var childrenByLine = new List<List<Drawable>>();
                var curLine = new List<Drawable>();

                foreach (var c in Children)
                {
                    if (c is NewLineContainer nlc)
                    {
                        curLine.Add(nlc);
                        childrenByLine.Add(curLine);
                        curLine = new List<Drawable>();
                    }
                    else
                    {
                        if (c.X == 0)
                        {
                            if (curLine.Count > 0)
                                childrenByLine.Add(curLine);
                            curLine = new List<Drawable>();
                        }

                        curLine.Add(c);
                    }
                }

                if (curLine.Count > 0)
                    childrenByLine.Add(curLine);

                bool isFirstLine = true;
                float lastLineHeight = 0f;

                foreach (var line in childrenByLine)
                {
                    bool isFirstChild = true;
                    IEnumerable<float> lineBaseHeightValues = line.OfType<IHasLineBaseHeight>().Select(l => l.LineBaseHeight);
                    float lineBaseHeight = lineBaseHeightValues.Any() ? lineBaseHeightValues.Max() : 0f;
                    float currentLineHeight = 0f;
                    float lineSpacingValue = lastLineHeight * LineSpacing;

                    // Compute the offset of this line from the right
                    Drawable lastTextPartInLine = (line[^1] is NewLineContainer && line.Count >= 2) ? line[^2] : line[^1];
                    float lineOffsetFromRight = ChildSize.X - (lastTextPartInLine.X + lastTextPartInLine.DrawWidth);

                    foreach (Drawable c in line)
                    {
                        if (c is NewLineContainer nlc)
                        {
                            nlc.Height = nlc.IndicatesNewParagraph ? (currentLineHeight == 0 ? lastLineHeight : currentLineHeight) * ParagraphSpacing : 0;
                            continue;
                        }

                        float childLineBaseHeight = (c as IHasLineBaseHeight)?.LineBaseHeight ?? 0f;
                        MarginPadding margin = new MarginPadding { Top = (childLineBaseHeight != 0f ? lineBaseHeight - childLineBaseHeight : 0f) + lineSpacingValue };
                        if (isFirstLine)
                            margin.Left = FirstLineIndent;
                        else if (isFirstChild)
                            margin.Left = ContentIndent;

                        c.Margin = margin;

                        if (c.Height > currentLineHeight)
                            currentLineHeight = c.Height;

                        if ((TextAnchor & Anchor.x1) != 0)
                            c.X += lineOffsetFromRight / 2;
                        else if ((TextAnchor & Anchor.x2) != 0)
                            c.X += lineOffsetFromRight;

                        isFirstChild = false;
                    }

                    if (currentLineHeight != 0f)
                        lastLineHeight = currentLineHeight;

                    isFirstLine = false;
                }
            }
        }
    }
}
