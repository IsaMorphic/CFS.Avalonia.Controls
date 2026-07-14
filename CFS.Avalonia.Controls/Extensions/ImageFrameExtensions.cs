using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;

namespace CFS.Avalonia.Controls.Extensions
{
    public static class ImageFrameExtensions
    {
        public static TimeSpan GetFrameDelay(this ImageFrame frame)
        {
            var frameMetadata = frame.Metadata;

            if (frameMetadata.GetGifMetadata() is GifFrameMetadata gifFrameMetadata && gifFrameMetadata.FrameDelay > 0)
            {
                return TimeSpan.FromMilliseconds(gifFrameMetadata.FrameDelay * 10);
            }
            else if (frameMetadata.GetPngMetadata() is PngFrameMetadata pngFrameMetadata && pngFrameMetadata.FrameDelay.Denominator > 0)
            {
                return TimeSpan.FromSeconds((double)pngFrameMetadata.FrameDelay.Numerator / pngFrameMetadata.FrameDelay.Denominator);
            }
            else if (frameMetadata.GetWebpMetadata() is WebpFrameMetadata webpFrameMetadata && webpFrameMetadata.FrameDelay > 0)
            {
                return TimeSpan.FromMilliseconds(webpFrameMetadata.FrameDelay);
            }
            else
            {
                return TimeSpan.Zero;
            }
        }
    }
}
