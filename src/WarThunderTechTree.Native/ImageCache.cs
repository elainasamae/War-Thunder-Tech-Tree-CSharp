using System.Collections.Concurrent;
using System.Drawing.Drawing2D;

namespace WarThunderTechTree.Native;

internal sealed class ImageCache : IDisposable
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(12) };
    private readonly ConcurrentDictionary<string, Bitmap> _images = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> _loading = new(StringComparer.OrdinalIgnoreCase);

    public Bitmap? GetOrQueue(string? url, Action loaded)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            return null;
        if (_images.TryGetValue(url, out var image)) return image;
        if (_loading.TryAdd(url, 0)) _ = LoadAsync(url, loaded);
        return null;
    }

    private async Task LoadAsync(string url, Action loaded)
    {
        try
        {
            var bytes = await Client.GetByteArrayAsync(url);
            using var stream = new MemoryStream(bytes);
            using var source = Image.FromStream(stream);
            var bitmap = new Bitmap(source.Width, source.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.CompositingQuality = CompositingQuality.HighSpeed;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.DrawImage(source, 0, 0, source.Width, source.Height);
            }
            _images[url] = bitmap;
            loaded();
        }
        catch
        {
            // Offline and individual image failures do not block the native tree.
        }
        finally
        {
            _loading.TryRemove(url, out _);
        }
    }

    public void Dispose()
    {
        foreach (var image in _images.Values) image.Dispose();
        _images.Clear();
    }
}
