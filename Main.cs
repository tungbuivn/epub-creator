using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Caching;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using AngleSharp.Extensions;
using AngleSharp.Parser.Html;
using Autofac;
using EPubFactory;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace epub_creator
{
    //public class ElementData
    //{
    //    public ChapterInfo chap { get; set; }
    //    public int idx { get; set; }
    //}

    public class Main
    {


        private readonly Config _cfg;
        private readonly Util _util;

        public Main(Config cfg, Util util)
        {
            _cfg = cfg;
            _util = util;
        }

        string GetOption(string name, string def = null)
        {
            return _cfg.CommandLine?.Where(o => o.StartsWith(name))
                       .Select(o => o.Trim()
                           .Split(new[] { name }, StringSplitOptions.None)
                           .Last()
                           .Trim()
                           .TrimStart('=')
                       ).FirstOrDefault() ?? def;
        }

        bool HasKey(string name)
        {
            return _cfg.CommandLine?.Where(o => o.StartsWith(name)).Any() ?? false;
        }

        //private static string[] _programArgs = null;
        
        string _dir = null;

        List<ChapterInfo> DownloadAll(List<ChapterInfo> allStories)
        {
            BlockingCollection<ChapterInfo> err = new BlockingCollection<ChapterInfo>();

            BufferBlock<ChapterInfo> urls = new BufferBlock<ChapterInfo>(new DataflowBlockOptions()
            {
                BoundedCapacity = 100
            });

            ActionBlock<ChapterInfo> download = new ActionBlock<ChapterInfo>(u =>
            {
                Console.WriteLine(u.Link);
                //using (var wc = new WebClient())
                //{
                //wc.Headers.Add("User-Agent",StrUserAgent);

                try
                {
                    var fileName = Path.Combine(_cfg.StoryDataDirectory, u.Idx.ToString());
                    if (!File.Exists(fileName))
                    {
                        var html = _util.DownloadUrl(u.Link);
                        if (!string.IsNullOrEmpty(html))
                            File.WriteAllText(fileName, html);
                        //if (bytes != null && bytes.Any())
                        //{
                        //    var html = Encoding.UTF8.GetString(bytes);

                        //}
                    }

                    //                            var parser = new HtmlParser();
                    //                            var doc = parser.Parse(html);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error:" + ex.Message);
                    err.Add(u);
                }
                //}
            },new ExecutionDataflowBlockOptions()
            {
                MaxDegreeOfParallelism = -1
            });
            urls.LinkTo(download, new DataflowLinkOptions()
            {
                PropagateCompletion = true
            });

            foreach (var o in allStories)
            {
                urls.SendAsync(o).Wait();
            }

            urls.Complete();
            urls.Completion.Wait();
            download.Complete();
            download.Completion.Wait();
            return err.ToList();
        }



        public static void ClearCurrentConsoleLine()
        {
            int currentLineCursor = Console.CursorTop;
            Console.SetCursorPosition(0, Console.CursorTop);
            Console.Write(new string(' ', Console.WindowWidth));
            Console.SetCursorPosition(0, currentLineCursor);
        }

        public string ResolveDriver(string url = "", string dir = "")
        {
            Uri driver;
            if (!string.IsNullOrEmpty(url))
            {
                driver = new Uri(url);
            }
            else
            {
                if (string.IsNullOrEmpty(dir)) throw new Exception("Không tìm được driver cho trang web");
                var chapters = JsonConvert.DeserializeObject<List<ChapterInfo>>(File.ReadAllText(_cfg.ChapterJson));
                driver = new Uri(chapters[0].Link);
            }
            if (driver == null) throw new Exception("Không tìm được driver cho trang web");
            return driver.Host;
        }
        IStorySite driver;

        public void Run()
        {
            //            new Aspose.Words.License().SetLicense(License.LStream);
            //            testAsp();

            string url = null;

            //bool help = false;
            bool epub = false;
            //int verbose = 0;


            url = GetOption("--url");
            _dir = GetOption("--dir");
            epub = HasKey("--epub");
            _dir = _dir ?? (url?.Split('/').Last(o => !string.IsNullOrEmpty(o)) ?? "");
            _cfg.RootDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase.Replace("file:///", ""));
            _cfg.StoryDirName = _dir;
            //_story = Path.Combine(_cfg.DataDirectory, _dir);

            Directory.CreateDirectory(_cfg.StoryDirectory);
            Directory.CreateDirectory(_cfg.StoryDataDirectory);

            
            driver = _cfg.Container.ResolveNamed<IStorySite>(ResolveDriver(url, _dir));
            driver.GetListChapters(url);
            driver.SaveListChapters();

            List<ChapterInfo> allStories = new List<ChapterInfo>();
            //if (!string.IsNullOrEmpty(url))
            //{
            //    var found = true;

            //    if (!File.Exists(Path.Combine(_story, "chapters.json")))
            //        while (found)
            //        {
            //            ClearCurrentConsoleLine();
            //            Console.Write($"\r{url}");
            //            using (var wc = new WebClient())
            //            {
            //                try
            //                {
            //                    wc.Headers.Add("User-Agent",_cfg.StrUserAgent);

            //                    if (url != null)
            //                    {
            //                        var bytes = wc.DownloadData(url);
            //                        var html = Encoding.UTF8.GetString(bytes);
            //                        var parser = new HtmlParser();
            //                        var doc = parser.Parse(html);
            //                        var chaps = doc.QuerySelectorAll("#list-chapter")
            //                            .SelectMany(o => o.QuerySelectorAll("ul")
            //                                .Where(p => p.Attributes["class"]?.Value?.ToString()
            //                                                .Contains("list-chapter") ??
            //                                            false)
            //                                .ToList()
            //                            )
            //                            .Where(o => o != null)
            //                            .SelectMany(o => o.QuerySelectorAll("a")
            //                                .Select(p => new ChapterInfo()
            //                                {
            //                                    Title = p.Text(),
            //                                    Link = p.Attributes["href"]?.Value
            //                                }).ToList())
            //                            .Where(o => !string.IsNullOrEmpty(o.Link))
            //                            .ToList();
            //                        allStories.AddRange(chaps);

            //                        // get chapters link
            //                        // get next list chapters
            //                        url = doc
            //                            .QuerySelectorAll(".pagination > li")
            //                            .ToList()
            //                            .Where(o => o.QuerySelectorAll(".glyphicon-menu-right").Length > 0)
            //                            .Select(o => o.QuerySelector("a"))
            //                            .Select(o => o.Attributes["href"]?.Value.Split('#').FirstOrDefault())
            //                            .FirstOrDefault(o => o != null);
            //                    }

            //                    found = !string.IsNullOrEmpty(url);
            //                }
            //                catch (Exception ex)
            //                {
            //                    Console.Write("\n");
            //                    Console.ForegroundColor = ConsoleColor.Red;
            //                    Console.WriteLine($"Error: {url} - {ex.Message}");
            //                    Console.ResetColor();
            //                    //Task.Delay(1000).Wait();
            //                }
            //            }
            //        }

            //    if (allStories.Any())
            //    {
            //        File.WriteAllText(Path.Combine(_story, "chapters.json"),
            //            JsonConvert.SerializeObject(allStories.Select((o, i) =>
            //                {
            //                    o.Idx = i;
            //                    return o;
            //                }).ToList()
            //            )
            //        );
            //    }
            //}

            if (!string.IsNullOrEmpty(_dir))
            {
                var chapFile = _cfg.ChapterJson;
                if (File.Exists(chapFile))
                {
                    allStories = JsonConvert.DeserializeObject<List<ChapterInfo>>(
                        File.ReadAllText(chapFile)
                    ).Select((o, i) =>
                    {
                        o.Idx = i;
                        return o;
                    }).ToList();

                    while (allStories.Any())
                    {
                        var errStories = DownloadAll(allStories);
                        allStories = errStories;
                    }
                }
            }


            if (epub)
            {
                TestEPub().Wait();
            }
        }


        private async Task TestEPub()
        {
            var chapters =
                JsonConvert.DeserializeObject<List<ChapterInfo>>(
                        File.ReadAllText(_cfg.ChapterJson))
                    .Select((o, i) =>
                    {
                        o.Idx = i;
                        return o;
                    }).ToList();

            BlockingCollection<ChapterContent> processedData = new BlockingCollection<ChapterContent>();
            var parser = new HtmlParser();
            BufferBlock<ChapterInfo> bufferBlock = new BufferBlock<ChapterInfo>(new DataflowBlockOptions()
            {
                BoundedCapacity = 100

            });
            int bl=0;
            int total = chapters.Count;
            int len = total.ToString().Length;

            ActionBlock<ChapterInfo> processContent = new ActionBlock<ChapterInfo>(chapterInfo =>
            {
                var content = File.ReadAllText(Path.Combine(_cfg.StoryDataDirectory, chapterInfo.Idx.ToString()));
                var document = parser.Parse(content);
                Interlocked.Increment(ref bl);
               
                Console.Write($"\rParsed: {bl.ToString().PadLeft(len,' ')} / {(total - bl).ToString().PadLeft(len, ' ')}");

                processedData.Add(driver.GetChapterContent(chapterInfo,document));
            }, new ExecutionDataflowBlockOptions()
            {
                MaxDegreeOfParallelism = -1
            });
            bufferBlock.LinkTo(processContent, new DataflowLinkOptions()
            {
                PropagateCompletion = true
            });
            chapters.ForEach((chap) =>
            {
                bufferBlock.SendAsync(chap).Wait();
            });
            bufferBlock.Complete();
            bufferBlock.Completion.Wait();
            processContent.Complete();
            processContent.Completion.Wait();
            Console.Write("\n");

            var ePubStream = File.Create($"{_dir}.epub");

            using (var writer = await EPubWriter.CreateWriterAsync(
                ePubStream,
                Regex.Replace(Regex.Replace($"{_dir}", "[^a-zA-Z0-9]", " "), "\\s+", " "),
                "tntdb",
                "08915002",
                new CultureInfo("vi-VN")))
            {
                //  Optional parameter
                writer.Publisher = "tntdb";
                bl = 0;
                foreach (var chapterContent in processedData.OrderBy(o => o.Idx).ToList())
                {
                    Interlocked.Increment(ref bl);
                    Console.Write($"\rCreating chapter {bl.ToString().PadLeft(len, ' ')} / {(total-bl).ToString().PadLeft(len, ' ')} : {chapterContent.FileName}");
                    //                    var idx = chapters.IndexOf(chap);
                    writer.AddChapterAsync(
                        chapterContent.FileName,
                        chapterContent.Title,
                        chapterContent.Content).Wait();
                }

                Console.Write("\n");
                //  Add a chapter with string content as x-html

                await writer.WriteEndOfPackageAsync();
            }
            Console.Write("\nDone!");
        }
    }
}