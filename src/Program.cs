using CodeHollow.FeedReader;
using CodeHollow.FeedReader.Feeds;
using Conesoft.Files;
using Helpers;
using System.Text.RegularExpressions;

var timer = new PeriodicTimer(TimeSpan.FromMinutes(15));

var settings = Conesoft.Hosting.Host.LocalSettings;
var storage = Conesoft.Hosting.Host.GlobalStorage / "FromSources" / "Feeds";
var feedstorage = storage / "Feeds";
var entrystorage = storage / "Entries";

do
{
    var feeds = await settings.ReadFromJson<Feed[]>() ?? Array.Empty<Feed>();

    await Task.WhenAll(feeds.Select(async f =>
    {
        try
        {
            var client = new HttpClient();

            var file = feedstorage / Filename.From(f.Filename, "xml");

            var xml = await client.GetStringAsync(f.Feedurl);
            await file.WriteText(xml);

            var feed = FeedReader.ReadFromString(xml);
            var entries = feed.Items;

            await Task.WhenAll(entries.Select(async e =>
            {
                var link = e.SpecificItem switch
                {
                    AtomFeedItem atom => atom.Links.FirstOrDefault(l => l.Relation == "alternate")?.Href ?? null,
                    _ => null
                };

                link ??= e.Link.StartsWith("http") ? e.Link : feed.Link + e.Link;

                var entry = new Entry(
                    Name: e.Title,
                    Url: link,
                    Published: e.PublishingDate ?? DateTime.MinValue,
                    Description: e.Description,
                    Category: f.Category
                );

                var entryfile = entrystorage / Filename.From(entry.Filename, "json");
                await entryfile.WriteAsJson(entry);

                await SaveImage(link, entrystorage, entry.Filename);
            }));
        }
        catch (Exception)
        {
        }
    }));
}
while (await timer.WaitForNextTickAsync());

static async Task SaveImage(string link, Conesoft.Files.Directory storage, string filename)
{
    var client = new HttpClient();

    var html = await client.GetStringAsync(link);

    {
        var match = FindOgImageContent().Match(html);

        if (match.Success && match.Groups.Count > 1)
        {
            var image = match.Groups[1].Value.UrlWithoutQueryString();
            var bytes = await client.GetByteArrayAsync(image);

            var extension = Path.GetExtension(image);
            extension = string.IsNullOrEmpty(extension) ? "jpg" : extension;

            var file = storage / Filename.From(filename, extension);

            await file.WriteBytes(bytes);

            return;
        }
    }

    {
        var match = FindContentOgImage().Match(html);

        if (match.Success && match.Groups.Count > 1)
        {
            var image = match.Groups[1].Value.UrlWithoutQueryString();
            var bytes = await client.GetByteArrayAsync(image);

            var extension = Path.GetExtension(image);
            extension = string.IsNullOrEmpty(extension) ? "jpg" : extension;

            var file = storage / Filename.From(filename, extension);

            await file.WriteBytes(bytes);

            return;
        }
    }

    {
        var match = FindFirstImage().Match(html);

        if (match.Success && match.Groups.Count > 1)
        {
            var image = match.Groups[1].Value.UrlWithoutQueryString();
            var bytes = await client.GetByteArrayAsync(image);

            var extension = Path.GetExtension(image);
            extension = string.IsNullOrEmpty(extension) ? "jpg" : extension;

            var file = storage / Filename.From(filename, extension);

            await file.WriteBytes(bytes);

            return;
        }
    }
}

record Feed(string Name, string Siteurl, string Feedurl, string Category)
{
    public string Filename => Name.SafeFilename();
}

record Entry(string Name, string Url, DateTime Published, string Description, string Category)
{
    public string Filename => Url.CleanUrl().SafeFilename();
}

partial class Program
{
    [GeneratedRegex("property=[\"']og:image[\"'] content=[\"'](.+?)[\"']")]
    private static partial Regex FindOgImageContent();


    [GeneratedRegex("content=[\"'](.+?)[\"'] property=[\"']og:image[\"']")]
    private static partial Regex FindContentOgImage();

    [GeneratedRegex("img.*?src=[\"\"'](.*?)[\"\"']", RegexOptions.IgnorePatternWhitespace | RegexOptions.IgnoreCase)]
    private static partial Regex FindFirstImage();
}