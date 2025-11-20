/* Based on Image.cs from the Avalonia project.
 * 
 * The MIT License (MIT)
 * Copyright (c) AvaloniaUI OÜ All Rights Reserved, 
 * portions copyright (c) Chosen Few Software
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy of 
 * this software and associated documentation files (the "Software"), to deal in the 
 * Software without restriction, including without limitation the rights to use, copy, 
 * modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and 
 * to permit persons to whom the Software is furnished to do so, subject to the 
 * following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all copies or 
 * substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, 
 * INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR 
 * PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE 
 * LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, 
 * TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE 
 * USE OR OTHER DEALINGS IN THE SOFTWARE.
 */

using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Automation.Peers;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Metadata;
using Avalonia.Platform;
using SixLabors.ImageSharp.PixelFormats;
using System.Buffers;

using CFS.Avalonia.Controls.Extensions;

namespace CFS.Avalonia.Controls
{
    /// <summary>
    /// Displays a <see cref="SixLabors.ImageSharp.Image"/> image.
    /// </summary>
    public class Image : Control
    {
        private static readonly SixLabors.ImageSharp.Configuration configuration;

        /// <summary>
        /// Defines the <see cref="Source"/> property.
        /// </summary>
        public static readonly StyledProperty<SixLabors.ImageSharp.Image?> SourceProperty =
            AvaloniaProperty.Register<Image, SixLabors.ImageSharp.Image?>(nameof(Source));

        /// <summary>
        /// Defines the <see cref="BlendMode"/> property.
        /// </summary>
        public static readonly StyledProperty<BitmapBlendingMode> BlendModeProperty =
            AvaloniaProperty.Register<Image, BitmapBlendingMode>(nameof(BlendMode));

        /// <summary>
        /// Defines the <see cref="Stretch"/> property.
        /// </summary>
        public static readonly StyledProperty<Stretch> StretchProperty =
            AvaloniaProperty.Register<Image, Stretch>(nameof(Stretch), Stretch.Uniform);

        /// <summary>
        /// Defines the <see cref="StretchDirection"/> property.
        /// </summary>
        public static readonly StyledProperty<StretchDirection> StretchDirectionProperty =
            AvaloniaProperty.Register<Image, StretchDirection>(
                nameof(StretchDirection),
                StretchDirection.Both);

        /// <summary>
        /// Defines the <see cref="IterationCount"/> property.
        /// </summary>
        public static readonly StyledProperty<IterationCount> IterationCountProperty =
            AvaloniaProperty.Register<Image, IterationCount>(nameof(IterationCount), IterationCount.Infinite);

        static Image()
        {
            configuration = SixLabors.ImageSharp.Configuration.Default.Clone();
            configuration.PreferContiguousImageBuffers = true;

            AffectsRender<Image>(SourceProperty, IterationCountProperty, StretchProperty, StretchDirectionProperty, BlendModeProperty);
            AffectsMeasure<Image>(SourceProperty, StretchProperty, StretchDirectionProperty);
            AutomationProperties.ControlTypeOverrideProperty.OverrideDefaultValue<Image>(AutomationControlType.Image);
        }

        // Essential bitmap state

        private SixLabors.ImageSharp.Image<Rgba32>? sourceImage;

        private WriteableBitmap? targetBitmap;

        // Animated bitmap state

        private SixLabors.ImageSharp.ImageFrame<Rgba32>? currentFrame;

        private TimeSpan? animationStart;

        /// <summary>
        /// Gets or sets the image that will be displayed.
        /// </summary>
        [Content]
        public SixLabors.ImageSharp.Image? Source
        {
            get => GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        /// <summary>
        /// Gets or sets the blend mode for the image.
        /// </summary>
        public BitmapBlendingMode BlendMode
        {
            get => GetValue(BlendModeProperty);
            set => SetValue(BlendModeProperty, value);
        }

        /// <summary>
        /// Gets or sets a value controlling how the image will be stretched.
        /// </summary>
        public Stretch Stretch
        {
            get => GetValue(StretchProperty);
            set => SetValue(StretchProperty, value);
        }

        /// <summary>
        /// Gets or sets a value controlling in what direction the image will be stretched.
        /// </summary>
        public StretchDirection StretchDirection
        {
            get => GetValue(StretchDirectionProperty);
            set => SetValue(StretchDirectionProperty, value);
        }

        /// <summary>
        /// Determines how many times the animation (if any) repeats before stopping.
        /// </summary>
        public IterationCount IterationCount 
        {
            get => GetValue(IterationCountProperty);
            set => SetValue(IterationCountProperty, value);
        }

        /// <inheritdoc />
        protected override bool BypassFlowDirectionPolicies => true;

        private void AnimationFrameHandler(TimeSpan elapsed)
        {
            animationStart ??= elapsed;

            var nextFrame = GetCurrentFrame(elapsed);
            if (nextFrame is not null)
            {
                currentFrame = nextFrame;
                InvalidateVisual();
            }
        }

        private SixLabors.ImageSharp.ImageFrame<Rgba32>? GetCurrentFrame(TimeSpan elapsed)
        {
            IEnumerable<TimeSpan> frameTimes = Source?.Frames?.GetCumulativeFrameDelays() ?? [];
            TimeSpan duration = frameTimes.LastOrDefault();
            if (duration > TimeSpan.Zero && (IterationCount.IsInfinite || (elapsed - animationStart) / duration < IterationCount.Value))
            {
                return ((IEnumerable<SixLabors.ImageSharp.ImageFrame<Rgba32>>?)sourceImage?.Frames)?.Zip(frameTimes)
                    .FirstOrDefault(x => (elapsed - animationStart)?.Ticks % duration.Ticks < x.Second.Ticks)
                    .First;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Renders the control.
        /// </summary>
        /// <param name="context">The drawing context.</param>
        public unsafe sealed override void Render(DrawingContext context)
        {
            if (targetBitmap is not null && Bounds.Size is { Width: > 0, Height: > 0 })
            {
                Rect viewPort = new Rect(Bounds.Size);
                Size sourceSize = targetBitmap.Size;

                Vector scale = Stretch.CalculateScaling(Bounds.Size, sourceSize, StretchDirection);
                Size scaledSize = sourceSize * scale;
                Rect destRect = viewPort
                    .CenterRect(new Rect(scaledSize))
                    .Intersect(viewPort);
                Rect sourceRect = new Rect(sourceSize)
                    .CenterRect(new Rect(destRect.Size / scale));

                if (currentFrame?.DangerousTryGetSinglePixelMemory(out Memory<Rgba32> currentFrameBuffer) == true)
                {
                    using (MemoryHandle sourceBufferHandle = currentFrameBuffer.Pin())
                    using (ILockedFramebuffer targetBufferHandle = targetBitmap.Lock())
                    {
                        void* sourcePointer = sourceBufferHandle.Pointer;
                        long sourceLength = (long)currentFrameBuffer.Length * sizeof(Rgba32);

                        void* targetPointer = (void*)targetBufferHandle.Address;
                        long targetLength = (long)targetBufferHandle.RowBytes * targetBufferHandle.Size.Height;

                        Buffer.MemoryCopy(sourcePointer, targetPointer, targetLength, sourceLength);
                    }
                }

                using (context.PushRenderOptions(new RenderOptions { BitmapBlendingMode = BlendMode }))
                {
                    context.DrawImage(targetBitmap, sourceRect, destRect);
                }
            }

            TopLevel.GetTopLevel(this)?
                .RequestAnimationFrame(AnimationFrameHandler);
        }

        /// <summary>
        /// Measures the control.
        /// </summary>
        /// <param name="availableSize">The available size.</param>
        /// <returns>The desired size of the control.</returns>
        protected override Size MeasureOverride(Size availableSize)
        {
            var result = new Size();

            if (targetBitmap is not null)
            {
                Size sourceSize = targetBitmap.Size;
                result = Stretch.CalculateSize(availableSize, sourceSize, StretchDirection);
            }

            return result;
        }

        /// <inheritdoc/>
        protected override Size ArrangeOverride(Size finalSize)
        {
            if (targetBitmap is not null)
            {
                Size sourceSize = targetBitmap.Size;
                var result = Stretch.CalculateSize(finalSize, sourceSize);
                return result;
            }
            else
            {
                return new Size();
            }
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            SixLabors.ImageSharp.Image? oldValue, newValue;
            if (change.Property == SourceProperty)
            {
                (oldValue, newValue) = change.GetOldAndNewValue
                    <SixLabors.ImageSharp.Image?>();

                sourceImage?.Dispose();
                sourceImage = newValue?.CloneAs<Rgba32>(configuration);

                targetBitmap?.Dispose();
                targetBitmap = sourceImage is null ? null : new WriteableBitmap(
                    new PixelSize(sourceImage.Width, sourceImage.Height), 
                    new Vector(96, 96), 
                    PixelFormat.Rgba8888, 
                    AlphaFormat.Unpremul
                    );

                currentFrame = sourceImage?.Frames.RootFrame;
            }

            if (change.Property == SourceProperty || change.Property == IterationCountProperty)
            {
                animationStart = null;
            }
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new ImageAutomationPeer(this);
        }
    }
}