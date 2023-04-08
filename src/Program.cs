using CodeHollow.FeedReader;
using Conesoft.Files;
using Helpers;
using System.Text.RegularExpressions;

var timer = new PeriodicTimer(TimeSpan.FromMinutes(15));

var settings = Conesoft.Hosting.Host.LocalSettings;
var storage = Conesoft.Hosting.Host.GlobalStorage / "FromSources" / "Feeds";
var feedstorage = storage / "Feeds";
var entrystorage = storage / "Entries";

var feeds = await settings.ReadFromJson<Feed[]>() ?? Array.Empty<Feed>();

do
{
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
                var link = e.Link.StartsWith("http") ? e.Link : feed.Link + e.Link;

                var entry = new Entry(
                    Name: e.Title,
                    Url: link,
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

    var match = MyRegex().Match(html);

    if(match.Success && match.Groups.Count > 1)
    {
        var image = match.Groups[1].Value.UrlWithoutQueryString();
        var bytes = await client.GetByteArrayAsync(image);

        var file = storage / Filename.From(filename, Path.GetExtension(image));

        await file.WriteBytes(bytes);
    }
}

record Feed(string Name, string Siteurl, string Feedurl, string Category)
{
    public string Filename => Name.SafeFilename();
}

record Entry(string Name, string Url, string Description, string Category)
{
    public string Filename => Url.CleanUrl().SafeFilename();
}

partial class Program
{
    [GeneratedRegex("<meta property=\"og:image\" content=\"(.+?)\"")]
    private static partial Regex MyRegex();
}