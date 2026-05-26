using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace OpenMono.Utils;

public static class ImageUtils
{
    private const long MaxPixels = 1280L * 32 * 32;

    public static readonly HashSet<string> Extensions =
        new(StringComparer.OrdinalIgnoreCase) { "png", "jpg", "jpeg", "gif", "webp" };

    public static bool IsImage(string path) =>
        Extensions.Contains(Path.GetExtension(path).TrimStart('.'));

    public static string MimeFromExt(string ext) =>
        ext is "jpg" or "jpeg" ? "image/jpeg" : $"image/{ext}";

    public static (byte[] bytes, string mime) SmartResize(byte[] raw, string origMime)
    {
        using var img = Image.Load(raw);
        long pixels = (long)img.Width * img.Height;
        if (pixels <= MaxPixels)
            return (raw, origMime);

        double scale = Math.Sqrt((double)MaxPixels / pixels);
        int newW = Math.Max(32, (int)(Math.Round(img.Width  * scale / 32.0) * 32));
        int newH = Math.Max(32, (int)(Math.Round(img.Height * scale / 32.0) * 32));

        img.Mutate(x => x.Resize(newW, newH));
        using var ms = new MemoryStream();
        img.SaveAsJpeg(ms, new JpegEncoder { Quality = 90 });
        return (ms.ToArray(), "image/jpeg");
    }
}
