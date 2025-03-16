using Conesoft.Hosting;
using Conesoft.Notifications;
using Conesoft.Services.PollFeedSources;

var builder = Host.CreateApplicationBuilder(args);

builder
    .AddHostConfigurationFiles()
    .AddHostEnvironmentInfo()
    .AddLoggingService()
    .AddNotificationService()
    ;

builder.Services
    .AddHttpClient()
    .AddHostedService<Service>()
    ;

builder.Services.AddHttpClient("rss", client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "RSS Reader");
});

var host = builder.Build();
await host.RunAsync();