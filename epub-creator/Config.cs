using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autofac;

namespace epub_creator
{
    public class Config
    {
        public IContainer Container;
        public bool Done = false;
        public readonly ConcurrentQueue<string> LogQueue =new ConcurrentQueue<string>();
        public string StrUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/67.0.3396.99 Safari/537.36";
        public string[] CommandLine;
        public string DataDirectory => Path.Combine(RootDirectory, "data");
        public string StoryDirectory => Path.Combine(DataDirectory, StoryDirName);
        public string StoryDataDirectory => Path.Combine(StoryDirectory, "data");
        public string ChapterJson => Path.Combine(StoryDirectory, "chapters.json");
        public readonly Lazy<string[]> BlackList;
        
        public string RootDirectory { get; set; }
        public string StoryDirName { get; set; }

        public Config(string[] args)
        {
            CommandLine = args;
            BlackList = new Lazy<string[]>(()=> File.ReadAllLines(Path.Combine(RootDirectory, "blacklist.txt")));
        }
    }
}
