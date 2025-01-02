using CodeHollow.FeedReader;
using CodeHollow.FeedReader.Feeds;
using Conesoft.Files;
using Conesoft.Hosting;
using Conesoft.Notifications;
using Conesoft.Services.PollFeedSources.Helpers;
using Helpers;
using Serilog;
using System.Web;
using Entry = Conesoft.Services.PollFeedSources.Entry;

var builder = Host.CreateApplicationBuilder(args);

builder
    .AddHostConfigurationFiles()
    .AddHostEnvironmentInfo()
    .AddLoggingService()
    .AddNotificationService()
    ;

var host = builder.Build();

using var lifetime = await host.StartConsoleAsync();

var configuration = builder.Configuration;
var environment = host.Services.GetRequiredService<HostEnvironment>();
var notification = host.Services.GetRequiredService<Notifier>();
var timer = new PeriodicTimer(TimeSpan.FromMinutes(15));

var storage = environment.Global.Storage / "FromSources" / "Feeds";
var feedstorage = storage / "Feeds";
var entrystorage = storage / "Entries";

do
{
    try
    {
        var feeds = configuration.GetRequiredSection("rss-feed-sources").Get<Feed[]>() ?? [];

        Log.Information($"polling {feeds.Length} feeds...");

        await Task.WhenAll(feeds.Select(async f =>
        {
            try
            {
                var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "RSS Reader");

                Log.Information("reading feed {feed}", f.Name);

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
                            Feed: f.Siteurl.CleanUrl().Replace("https://", "").Replace("http://", "").Replace("www.", "")
                        );

                        var entryfile = entrystorage / Filename.From(entry.Filename, "json");
                        var newentry = entryfile.Exists == false;
                        await entryfile.WriteAsJson(entry);

                        if (newentry)
                        {
                            var image = await ImageSaver.SaveImage(link, entrystorage, entry.Filename);
                            await notification.Notify(entry.Name, $"from: {entry.Feed}", entry.Url, image);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Error: {exception} for {page}: {link}", ex.Message, f.Name, e.Link);
                    }
                }));
            }
            catch (Exception ex)
            {
                Log.Error("Error: {exception} for {page}: {url}", ex.Message, f.Name, f.Siteurl);
            }
        }));
        Log.Information("done");
    }
    catch (Exception ex)
    {
        Log.Error("Error: {exception}", ex.Message);
    }
}
while (await timer.WaitForNextTickAsync(lifetime.CancellationToken).ReturnFalseWhenCancelled());


