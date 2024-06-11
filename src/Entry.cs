using Helpers;

namespace Conesoft.Services.PollFeedSources;

public record Entry(string Name, string Url, DateTime Published, string Description, string Category, string Feed)
{
    public string Filename => Url.CleanUrl().SafeFilename();
}