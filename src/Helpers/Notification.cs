using Microsoft.AspNetCore.Http.Extensions;

namespace Conesoft.Services.PollFeedSources.Helpers
{
    public class Notification(string conesoftSecret)
    {
        public async Task Notify(Entry entry, Files.File? image)
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
    }
}
