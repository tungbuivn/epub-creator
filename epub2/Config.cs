using Microsoft.Extensions.Hosting;

public class Config
{
    // public bool Done = false;
    // public readonly ConcurrentQueue<string> LogQueue =new ConcurrentQueue<string>();
    public const string StrUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/67.0.3396.99 Safari/537.36";
    public string[] CommandLine;
    public string CacheDirectory => Path.Combine(RootDirectory, ".cache");
    public string DataDirectory => Path.Combine(RootDirectory, "data");
    public string StoryDirectory => Path.Combine(DataDirectory, StoryDirName);
    public string StoryDataDirectory => Path.Combine(StoryDirectory, "data");
    public string ChapterJson => Path.Combine(StoryDirectory, "chapters.json");
    public readonly Lazy<string[]> BlackList;
    public string EPubFile=>(Url?.Split('/').Last(o => !string.IsNullOrEmpty(o)) ?? "");
        
    public string RootDirectory { get; set; }
    public string StoryDirName { get; set; } = "";
    public string GetOption(int idx)
    {
        return CommandLine[idx].Trim();


    }

    public string Url => GetOption(0);
    public Config(string[] args, IHostEnvironment env)
    {
        RootDirectory = env.ContentRootPath;
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(CacheDirectory);
        Directory.CreateDirectory(StoryDirectory);
        Directory.CreateDirectory(StoryDataDirectory);
        CommandLine = args;
        BlackList = new Lazy<string[]>(()=> File.ReadAllLines(Path.Combine(RootDirectory, "blacklist.txt")));
    }
}