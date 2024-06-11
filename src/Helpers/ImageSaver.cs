using Conesoft.Files;
using Conesoft.Tools;
using System.Text.RegularExpressions;

namespace Conesoft.Services.PollFeedSources.Helpers;

public partial class ImageSaver
{
    public static async Task<Files.File?> SaveImage(string link, Files.Directory storage, string filename)
    {
        var client = new HttpClient();

        var html = await client.GetStringAsync(link);

        var match = FindOgImageContentOrFirstImage().Match(html);

        var image = match.Success ? match.Groups["imageurl"].Value.UrlWithoutQueryString() : null;

        if (image != null)
        {
            var extension = Path.GetExtension(image);
            extension = string.IsNullOrEmpty(extension) ? "jpg" : extension;

            var file = storage / Filename.From(filename, extension);

            using var stream = await client.GetStreamAsync(image);
            using var filestream = System.IO.File.OpenWrite(file.Path);
            await stream.CopyToAsync(filestream);

            return file;
        }

        return null;
    }


    [GeneratedRegex(
        "property=[\"']og:image[\"'] content=[\"'](?<imageurl>.+?)[\"']|" +
        "content=[\"'](?<imageurl>.+?)[\"'] property=[\"']og:image[\"']|" +
        "img(?!>)src=[\"'](?<imageurl>.*?)[\"']",
        RegexOptions.IgnoreCase
    )]
    private static partial Regex FindOgImageContentOrFirstImage();
}
