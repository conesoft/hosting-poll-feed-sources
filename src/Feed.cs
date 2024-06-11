using Helpers;

record Feed(string Name, string Siteurl, string Feedurl, string Category)
{
    public string Filename => Name.SafeFilename();
}
