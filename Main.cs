using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using AngleSharp.Extensions;
using AngleSharp.Parser.Html;
using EPubFactory;
using Newtonsoft.Json;

namespace epub_creator
{
    //public class ElementData
    //{
    //    public ChapterInfo chap { get; set; }
    //    public int idx { get; set; }
    //}

    internal class ChapterContent
    {
        public string FileName { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public int Idx { get; set; }
    }

    public class ChapterInfo
    {
        public string Title { get; set; }
        public string Link { get; set; }
        public int Idx { get; set; }
    }

    public class Main
    {
        private const string StrUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/67.0.3396.99 Safari/537.36";
        private static string _root;

        static string GetOption(string name, string def = null)
        {
            return _programArgs?.Where(o => o.StartsWith(name))
                       .Select(o => o.Trim()
                           .Split(new[] {name}, StringSplitOptions.None)
                           .Last()
                           .Trim()
                           .TrimStart('=')
                       ).FirstOrDefault() ?? def;
        }

        static bool HasKey(string name)
        {
            return _programArgs?.Where(o => o.StartsWith(name)).Any() ?? false;
        }

        private static string[] _programArgs = null;
        private static string _story;
        string _dir = null;

        List<ChapterInfo> DownloadAll(List<ChapterInfo> allStories)
        {
            BlockingCollection<ChapterInfo> err = new BlockingCollection<ChapterInfo>();

            BufferBlock<ChapterInfo> urls = new BufferBlock<ChapterInfo>(new DataflowBlockOptions()
            {
                BoundedCapacity = Environment.ProcessorCount
            });

            ActionBlock<ChapterInfo> download = new ActionBlock<ChapterInfo>(u =>
            {
                Console.WriteLine(u.Link);
                using (var wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent",StrUserAgent);

                    try
                    {
                        var fileName = Path.Combine(_story, "data", u.Idx.ToString());
                        if (!File.Exists(fileName))
                        {
                            var bytes = wc.DownloadData(u.Link);
                            if (bytes != null && bytes.Any())
                            {
                                var html = Encoding.UTF8.GetString(bytes);
                                File.WriteAllText(fileName, html);
                            }
                        }

                        //                            var parser = new HtmlParser();
                        //                            var doc = parser.Parse(html);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error:" + ex.Message);
                        err.Add(u);
                    }
                }
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

        string Download(string url)
        {
            var cookieContainer = new CookieContainer();
            using (var handler = new HttpClientHandler() {CookieContainer = cookieContainer})
            using (var client = new HttpClient(handler))
            {
                client.DefaultRequestHeaders.Add("User-Agent",StrUserAgent);

                using (var result = client.GetAsync(url).Result)
                {
                    result.EnsureSuccessStatusCode();
                    return Encoding.UTF8.GetString(result.Content.ReadAsByteArrayAsync().Result);
                }
            }
        }

        public static void ClearCurrentConsoleLine()
        {
            int currentLineCursor = Console.CursorTop;
            Console.SetCursorPosition(0, Console.CursorTop);
            Console.Write(new string(' ', Console.WindowWidth));
            Console.SetCursorPosition(0, currentLineCursor);
        }

        public void Run(string[] args)
        {
            //            new Aspose.Words.License().SetLicense(License.LStream);
            //            testAsp();
            _programArgs = args;
            string url = null;

            //bool help = false;
            bool epub = false;
            //int verbose = 0;


            url = GetOption("--url");
            _dir = GetOption("--dir");
            epub = HasKey("--epub");
            _root = Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase.Replace("file:///", ""));
            var rootData = Path.Combine(_root, "data");
            var url1 = url;
            _dir = _dir ?? new Lazy<string>(() => url1?.Split('/').Last(o => !string.IsNullOrEmpty(o)) ?? "").Value;


            _story = Path.Combine(rootData, _dir);
            Directory.CreateDirectory(_story);
            Directory.CreateDirectory(Path.Combine(_story, "data"));

            List<ChapterInfo> allStories = new List<ChapterInfo>();
            if (!string.IsNullOrEmpty(url))
            {
                var found = true;

                if (!File.Exists(Path.Combine(_story, "chapters.json")))
                    while (found)
                    {
                        ClearCurrentConsoleLine();
                        Console.Write($"\r{url}");
                        using (var wc = new WebClient())
                        {
                            try
                            {
                                wc.Headers.Add("User-Agent",StrUserAgent);

                                if (url != null)
                                {
                                    var bytes = wc.DownloadData(url);
                                    var html = Encoding.UTF8.GetString(bytes);
                                    var parser = new HtmlParser();
                                    var doc = parser.Parse(html);
                                    var chaps = doc.QuerySelectorAll("#list-chapter")
                                        .SelectMany(o => o.QuerySelectorAll("ul")
                                            .Where(p => p.Attributes["class"]?.Value?.ToString()
                                                            .Contains("list-chapter") ??
                                                        false)
                                            .ToList()
                                        )
                                        .Where(o => o != null)
                                        .SelectMany(o => o.QuerySelectorAll("a")
                                            .Select(p => new ChapterInfo()
                                            {
                                                Title = p.Text(),
                                                Link = p.Attributes["href"]?.Value
                                            }).ToList())
                                        .Where(o => !string.IsNullOrEmpty(o.Link))
                                        .ToList();
                                    allStories.AddRange(chaps);

                                    // get chapters link
                                    // get next list chapters
                                    url = doc
                                        .QuerySelectorAll(".pagination > li")
                                        .ToList()
                                        .Where(o => o.QuerySelectorAll(".glyphicon-menu-right").Length > 0)
                                        .Select(o => o.QuerySelector("a"))
                                        .Select(o => o.Attributes["href"]?.Value.Split('#').FirstOrDefault())
                                        .FirstOrDefault(o => o != null);
                                }

                                found = !string.IsNullOrEmpty(url);
                            }
                            catch (Exception ex)
                            {
                                Console.Write("\n");
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine($"Error: {url} - {ex.Message}");
                                Console.ResetColor();
                                //Task.Delay(1000).Wait();
                            }
                        }
                    }

                if (allStories.Any())
                {
                    File.WriteAllText(Path.Combine(_story, "chapters.json"),
                        JsonConvert.SerializeObject(allStories.Select((o, i) =>
                            {
                                o.Idx = i;
                                return o;
                            }).ToList()
                        )
                    );
                }
            }

            if (!string.IsNullOrEmpty(_dir))
            {
                var chapFile = Path.Combine(_story, "chapters.json");
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
                        File.ReadAllText(Path.Combine(_story, @"chapters.json")))
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

            ActionBlock<ChapterInfo> processContent = new ActionBlock<ChapterInfo>(chapterInfo =>
            {
                var content = File.ReadAllText(Path.Combine(_story, @"data", chapterInfo.Idx.ToString()));
                var document = parser.Parse(content);
                var text = document.QuerySelector(".chapter-c");
                text.OuterHtml = "<div>" + text.InnerHtml + "</div>";
                text.QuerySelectorAll("script").ToList().ForEach(o => o.OuterHtml = "\n");
                while (text.QuerySelector("i") != null)
                {
                    var i = text.QuerySelector("i");
                    i.OuterHtml = i.InnerHtml;
                }


                text.QuerySelectorAll("*")
                    .Select(o => new {o, attr = o.Attributes["style"]?.Value})
                    .Where(o => o.attr != null)
                    .Where(o => o.attr.Contains("font-size:0px")
                                || o.attr.Contains("font-size:1px")
                                || o.attr.Contains("font-size:2px")
                                || o.attr.Contains("font-size:3px")
                                || o.attr.Contains("font-size:4px")
                                || o.attr.Contains("color:white;")
                    )
                    .ToList()
                    .ForEach(o => o.o.Remove());
                
                while (text.QuerySelectorAll("*").Any())
                {
                    text.QuerySelectorAll("*")
                        .ToList()
                        .ForEach(o =>
                        {
                            Console.Write($"\r{o.TagName}");
                            o.OuterHtml = string.IsNullOrEmpty(o.Text()) ? "\n" : "\n" + o.InnerHtml;
                        });
                }
                Console.Write("\n");

                //if (text.QuerySelectorAll("*").Any()) goto lbl1;
                var textClip = text.InnerHtml;
                foreach (var p in File.ReadAllLines(Path.Combine(_root, "blacklist.txt")))
                {
                    textClip = Regex.Replace(textClip, p, "", RegexOptions.IgnoreCase);
                }

                var js = String.Join("",
                    textClip
                        .Split('\n')
                        .Where(o => o != null && !string.IsNullOrEmpty(o))
                        .Select(o =>
                        {
                            var doc = document.CreateElement("p");
                            doc.TextContent = o.Trim();
                            return doc.OuterHtml;
                        })
                        .ToArray());
                var docTitle = document.CreateElement("h1");
                docTitle.TextContent = chapterInfo.Title;
                var finalHtml =
                    $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><html xmlns=\"http://www.w3.org/1999/xhtml\"><head><title /></head><body>{docTitle.OuterHtml}{js}</body></html>";
                var fileName = $"chapter-{chapterInfo.Idx}.xhtml";
                Console.Write("\rParsed: " + chapterInfo.Idx);

                processedData.Add(new ChapterContent()
                {
                    Idx = chapterInfo.Idx,
                    FileName = fileName,
                    Title = chapterInfo.Title,
                    Content = $"{finalHtml}"
                });
            }, new ExecutionDataflowBlockOptions()
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount
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

                foreach (var chapterContent in processedData.OrderBy(o => o.Idx).ToList())
                {
                    Console.Write("\rCreating chapter: " + chapterContent.FileName);
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