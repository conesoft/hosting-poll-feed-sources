using CodeHollow.FeedReader;
using CodeHollow.FeedReader.Feeds;
using Conesoft.Files;
using Conesoft.Hosting;
using Conesoft.Services.PollFeedSources.Helpers;
using Helpers;
using Serilog;
using System.Web;

var configuration = new ConfigurationBuilder().AddJsonFile(Conesoft.Hosting.Host.GlobalSettings.Path).Build();
var conesoftSecret = configuration["conesoft:secret"] ?? throw new Exception("Conesoft Secret not found in Configuration");

var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(args);
builder.Services
    .AddLoggingToHost()
    .AddPeriodicGarbageCollection(TimeSpan.FromMinutes(5))
    .AddSingleton(new Notification(conesoftSecret));

var host = builder.Build();

await host.StartAsync();

var notification = host.Services.GetRequiredService<Notification>();

var timer = new PeriodicTimer(TimeSpan.FromMinutes(15));

var settings = Conesoft.Hosting.Host.LocalSettings;
var storage = Conesoft.Hosting.Host.GlobalStorage / "FromSources" / "Feeds";
var feedstorage = storage / "Feeds";
var entrystorage = storage / "Entries";

Log.Information("trying to read from {settings}", settings);

do
{
    try
    {
        var feeds = await settings.ReadFromJson<Feed[]>() ?? [];

        Log.Information($"polling {feeds.Length} feeds...");

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
                            var image = await ImageSaver.SaveImage(link, entrystorage, entry.Filename);
                            await notification.Notify(entry, image);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Error: {exception} for {link}", ex.Message, e.Link);
                    }
                }));
            }
            catch (Exception ex)
            {
                Log.Error("Error: {exception} for {url}", ex.Message, f.Siteurl);
            }
        }));
        Log.Information("done");
    }
    catch (Exception ex)
    {
        Log.Error("Error: {exception}", ex.Message);
    }
}
while (await timer.WaitForNextTickAsync());


