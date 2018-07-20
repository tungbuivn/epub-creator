using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AngleSharp.Dom;
using AngleSharp.Dom.Html;
using AngleSharp.Extensions;
using AngleSharp.Parser.Html;
using Newtonsoft.Json;

namespace epub_creator
{
    public abstract class BaseStory 
    {
        protected  Config Cfg;
        public List<ChapterInfo> AllStories = new List<ChapterInfo>();
        
        public void SaveListChapters()
        {
            if (AllStories.Any())
            {
                File.WriteAllText(Path.Combine(Cfg.StoryDirectory, "chapters.json"),
                    JsonConvert.SerializeObject(AllStories.Select((o, i) =>
                        {
                            o.Idx = i;
                            return o;
                        }).ToList()
                    )
                );
            }
        }
    }
    public class TruyenFull : BaseStory, IStorySite
    {
       
      
        public TruyenFull(Config cfg)
        {
            Cfg = cfg;
        }

        public ChapterContent GetChapterContent(ChapterInfo chapterInfo,IHtmlDocument document)
        {
            var text = document.QuerySelector(".chapter-c");
            text.OuterHtml = "<div>" + text.InnerHtml + "</div>";
            text.QuerySelectorAll("script").ToList().ForEach(o => o.OuterHtml = "\n");
            while (text.QuerySelector("i") != null)
            {
                var i = text.QuerySelector("i");
                i.OuterHtml = i.InnerHtml;
            }


            text.QuerySelectorAll("*")
                .Select(o => new { o, attr = o.Attributes["style"]?.Value })
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
            foreach (var p in File.ReadAllLines(Path.Combine(Cfg.RootDirectory, "blacklist.txt")))
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
            //Console.Write("\rParsed: " + chapterInfo.Idx);

            return new ChapterContent()
            {
                Idx = chapterInfo.Idx,
                FileName = fileName,
                Title = chapterInfo.Title,
                Content = $"{finalHtml}"
            };
        }
        public void GetListChapters(string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            if (File.Exists(Path.Combine(Cfg.StoryDirectory, "chapters.json"))) return;
            var found = true;
            while (found)
            {
                //ClearCurrentConsoleLine();
                Console.Write($"\r{url}");
                using (var wc = new WebClient())
                {
                    try
                    {
                        wc.Headers.Add("User-Agent", Cfg.StrUserAgent);

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
                            AllStories.AddRange(chaps);

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

            
        }

       
    }
}
