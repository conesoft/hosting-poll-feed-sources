using CodeHollow.FeedReader;
using CodeHollow.FeedReader.Feeds;
using Conesoft.Files;
using Helpers;
using Microsoft.AspNetCore.Http.Extensions;
using System.Text.RegularExpressions;
using System.Web;

var configuration = new ConfigurationBuilder().AddJsonFile(Conesoft.Hosting.Host.GlobalSettings.Path).Build();
var conesoftSecret = configuration["conesoft:secret"] ?? throw new Exception("Conesoft Secret not found in Configuration");

var timer = new PeriodicTimer(TimeSpan.FromMinutes(15));

var settings = Conesoft.Hosting.Host.LocalSettings;
var storage = Conesoft.Hosting.Host.GlobalStorage / "FromSources" / "Feeds";
var feedstorage = storage / "Feeds";
var entrystorage = storage / "Entries";

var newestEntriesFile = storage / Filename.From("Newest Entries", "txt");

var newestEntries = newestEntriesFile.Exists ? (await newestEntriesFile.ReadLines() ?? []).ToList() : [];

do
{
    try
    {
        var feeds = await settings.ReadFromJson<Feed[]>() ?? [];

        Console.WriteLine($"polling {feeds.Length} feeds...");

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
                    try
                    {
                        var link = e.SpecificItem switch
                        {
                            AtomFeedItem atom => atom.Links.FirstOrDefault(l => l.Relation == "alternate")?.Href ?? null,
                            _ => null
                        };

                        link ??= e.Link.StartsWith("http") ? e.Link : feed.Link + e.Link;

                        var entry = new Entry(
                            Name: HttpUtility.UrlDecode(e.Title),
                            Url: link,
                            Published: e.PublishingDate ?? DateTime.MinValue,
                            Description: e.Description,
                            Category: f.Category,
                            Feed: f.Siteurl.CleanUrl().Replace("https://", "").Replace("http://", "").Replace("/", "").Replace("www.", "")
                        );

                        var entryfile = entrystorage / Filename.From(entry.Filename, "json");
                        var newentry = entryfile.Exists == false;
                        await entryfile.WriteAsJson(entry);

                        if (newentry)
                        {
                            var image = await SaveImage(link, entrystorage, entry.Filename);
                            if (newestEntries.Count > 0)
                            {
                                newestEntries.Insert(0, entry.Filename);
                            }
                            else
                            {
                                newestEntries.Add(entry.Filename);
                            }
                            await Notify(entry, image);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        Console.WriteLine();
                        Console.WriteLine(e.Link);
                    }
                }));
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine();
                Console.WriteLine(f.Siteurl);
            }
        }));

        await newestEntriesFile.WriteLines(newestEntries.Take(10));

        Console.WriteLine("...done");
        Console.WriteLine();
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex.Message);
        Console.WriteLine();
    }
}
while (await timer.WaitForNextTickAsync());

async Task Notify(Entry entry, Conesoft.Files.File? image)
{
    var title = entry.Name;
    var message = $"from: {entry.Feed}";
    var url = entry.Url;
    var imageUrl = image != null ? $"https://kontrol.conesoft.net/content/feeds/thumbnail/{image.Name}" : "";

    var query = new QueryBuilder
    {
        { "token", conesoftSecret },
        { "title", title },
        { "message", message },
        { "url", url }
    };
    if (image != null)
    {
        query.Add("imageUrl", imageUrl);
    }

    await new HttpClient().GetAsync($@"https://conesoft.net/notify" + query.ToQueryString());
}

static async Task<Conesoft.Files.File?> SaveImage(string link, Conesoft.Files.Directory storage, string filename)
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

record Feed(string Name, string Siteurl, string Feedurl, string Category)
{
    public string Filename => Name.SafeFilename();
}

record Entry(string Name, string Url, DateTime Published, string Description, string Category, string Feed)
{
    public string Filename => Url.CleanUrl().SafeFilename();
}

partial class Program
{
    //[GeneratedRegex("property=[\"']og:image[\"'] content=[\"'](.+?)[\"']")]
    //private static partial Regex FindOgImageContent();


    //[GeneratedRegex("content=[\"'](.+?)[\"'] property=[\"']og:image[\"']")]
    //private static partial Regex FindContentOgImage();

    //[GeneratedRegex("img.*?src=[\"\"'](.*?)[\"\"']", RegexOptions.IgnorePatternWhitespace | RegexOptions.IgnoreCase)]
    //private static partial Regex FindFirstImage();

    [GeneratedRegex(
        "property=[\"']og:image[\"'] content=[\"'](?<imageurl>.+?)[\"']|" +
        "content=[\"'](?<imageurl>.+?)[\"'] property=[\"']og:image[\"']|" +
        "img(?!>)src=[\"'](?<imageurl>.*?)[\"']",
        RegexOptions.IgnoreCase
    )]
    private static partial Regex FindOgImageContentOrFirstImage();
}