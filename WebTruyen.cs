using System;
using System.Collections.Generic;
using System.Diagnostics;
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

    public class WebTruyen : BaseStory, IStorySite
    {
        private readonly Util _util;

        public WebTruyen(Config cfg, Util util)
        {
            _util = util;
            Cfg = cfg;
        }
        public void GetListChapters(string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            if (File.Exists(Path.Combine(Cfg.StoryDirectory, "chapters.json"))) return;
            
            List<LinkPage> linkPages=new List<LinkPage>();
            linkPages.Add(new LinkPage(){ Link = url});
            while (linkPages.Any(o=>!o.Processed))
            {
                var page = linkPages.FirstOrDefault(o => !o.Processed);
                //ClearCurrentConsoleLine();
               
                //using (var wc = new WebClient())
                if (page!=null)
                {
                    Console.Write($"\r{page.Link}");
                    try
                    {
                        //wc.Headers.Add("User-Agent", Cfg.StrUserAgent);

                        //if (url != null)
                        {
                            //var bytes = wc.DownloadData(url);
                            //var html = Encoding.UTF8.GetString(bytes);
                            var html = _util.DownloadUrl(page.Link);
                            var parser = new HtmlParser();
                            var doc = parser.Parse(html);
                            var chaps = doc.QuerySelectorAll(".list-chapter")
                                .Where(o =>
                                {
                                    var f = o.QuerySelector("h2");
                                    if (f != null)
                                    {
                                        return Regex.Match(f.TextContent, "Danh sách chương", RegexOptions.IgnoreCase)
                                            .Success;
                                    }
                                    return false;
                                })
                                .Select(o => o.QuerySelector("ul"))
                                
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
                            var foundUrls = doc
                                .QuerySelectorAll(".pagination > ul > li > a")
                                .Where(o =>
                                {
                                    int.TryParse(o.Text(), out var i);
                                    return i > 0;
                                })
                                .Select(o=>o.Attributes["href"]?.Value)
                                .Where(o=>!string.IsNullOrEmpty(o))
                                .ToList()
                                .Except(linkPages.Select(o=>o.Link).ToList())
                                .Select(o=>new LinkPage(){Link = o})
                                .ToList();
                            linkPages.AddRange(foundUrls);
                        }
                        page.Processed = true;

                    }
                    catch (Exception ex)
                    {
                        Console.Write("\n");
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Error: {url} - {ex.Message}");
                        Console.ResetColor();
                        Task.Delay(1000).Wait();
                    }


                }
                
            }


        }

        public ChapterContent GetChapterContent(ChapterInfo chapterInfo, IHtmlDocument document)
        {
            try
            {
                var text = document.QuerySelector(".detailcontent");
                text.OuterHtml = "<div>" + text.InnerHtml + "</div>";
                
                text.QuerySelectorAll("script").ToList().ForEach(o => o.OuterHtml = "\n");
                text.Descendents<IComment>().ToList().ForEach(o => o.Remove());
                text.InnerHtml = Regex.Replace(text.InnerHtml, "[\n\r]", " ");

                text.QuerySelectorAll("*")
                    .Where(o => o.Attributes["id"]?.Value?.Contains("vtn-") ?? false)
                    .ToList()
                    .ForEach(o => o.Remove());
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
                            //Console.Write($"\r{o.TagName}");
                            o.OuterHtml = string.IsNullOrEmpty(o.Text()) ? "\n" : "\n" + o.InnerHtml;
                        });
                }
                //Console.Write("\n");

                //if (text.QuerySelectorAll("*").Any()) goto lbl1;
                var textClip = text.InnerHtml;
                foreach (var p in Cfg.BlackList.Value)
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
                //Console.Write($"\rParsed: {chapterInfo.Idx}       " );

                return new ChapterContent()
                {
                    Idx = chapterInfo.Idx,
                    FileName = fileName,
                    Title = chapterInfo.Title,
                    Content = $"{finalHtml}"
                };
            }
            catch (Exception ex)
            {
                Debug.Print(ex.Message);
                throw;
            }
           
        }
    }

    public class LinkPage
    {
        public string Link { get; set; }
        public bool Processed { get; set; }
    }
}
