using ColumnPadStudio.Services;
using ColumnPadStudio.ViewModels;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColumnPadStudio.SmokeTests;

internal static class ImageSafetySmokeTests
{
    public static void Run(SmokeTestContext tests)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(
            1,
            3000,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            new byte[3000 * 4],
            4)));

        byte[] content;
        using (var stream = new MemoryStream())
        {
            encoder.Save(stream);
            content = stream.ToArray();
        }

        var image = new ColumnImageViewModel(
            string.Empty,
            "tall.png",
            pixelWidth: 1,
            pixelHeight: 1,
            imageContent: content);

        tests.Check(
            image.PixelWidth == 1 && image.PixelHeight == 3000,
            "Loaded pictures should derive their dimensions from actual bytes instead of trusting saved metadata.");
        tests.Check(
            image.DisplaySource is BitmapSource { PixelHeight: <= 2000 },
            "Tall pictures should be downscaled by their longest dimension before display decoding.");
    }
}
