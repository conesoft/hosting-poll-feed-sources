using System.Web;

namespace Helpers;

static public class StringSanitationHelpers
{
    public static string SafeFilename(this string filename, char replacement = '-')
    {
        return string.Join(replacement, filename.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Replace('.', replacement);
    }

    public static string CleanUrl(this string url)
    {
        var split = url.Split('?');
        if (split.Length > 1)
        {
            var queries = HttpUtility.ParseQueryString(split[1]);
            queries.Remove("rss");
            queries.Remove("src");
            queries.Remove("utm_medium");
            queries.Remove("utm_source");
            queries.Remove("utm_campaign");
            queries.Remove("utm_content");

            if(queries.Count > 0)
            {
                return split[0] + "?" + queries;
            }
        }
        return split[0];
    }

    public static string UrlWithoutQueryString(this string url) => url.Split('?').First();
}