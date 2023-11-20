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

namespace epub_creator
{
    public class ThichDocTruyen : BaseStory, IStorySite
    {
       
      
      

        public ChapterContent GetChapterContent(ChapterInfo chapterInfo)
        {
            var parser=new HtmlParser();
            var content = File.ReadAllText(Path.Combine(Cfg.StoryDataDirectory, chapterInfo.Idx.ToString()));
            var document = parser.Parse(content);
            var text = document.QuerySelector(".boxview");
            text.OuterHtml = "<div>" + text.InnerHtml + "</div>";
            text.QuerySelectorAll("script").ToList().ForEach(o => o.OuterHtml = "\n");
           


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
//                        Console.Write($"\r{o.TagName}");
                        o.OuterHtml = string.IsNullOrEmpty(o.Text()) ? "\n" : "\n" + o.InnerHtml;
                    });
            }
         

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
            var docTitle = document.CreateElement("p");
            var docstrong=document.CreateElement("strong");
            docstrong.TextContent = chapterInfo.Title;
            docTitle.AppendChild(docstrong);
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
            url = url ?? "";
            if (string.IsNullOrEmpty(url)) return;
            if (File.Exists(Path.Combine(Cfg.StoryDirectory, "chapters.json"))) return;
            var found = true;

          
            var chapHistories = new List<string>();
            var chapDone = new List<string>();
            chapHistories.Add(url+"/page1");
            var isLast = false;
            while (found)
            {
                //ClearCurrentConsoleLine();
                
                var url2 = url;
                Cfg.LogQueue.Enqueue($"{url2}");
                using (var wc = new WebClient())
                {
                    try
                    {
                        wc.Headers.Add("User-Agent", Cfg.StrUserAgent);

                        if (!string.IsNullOrEmpty(url))
                        {
                            
                            var bytes = wc.DownloadData(url);
                            var html = Encoding.UTF8.GetString(bytes);
                            var parser = new HtmlParser();
                            var doc = parser.Parse(html);
                            var chaps = doc.QuerySelectorAll("#dschuong")
                                .SelectMany(o => o.QuerySelectorAll("td")
                                    .Where(p => string.IsNullOrEmpty(
                                            p.Attributes["colspan"]?.Value?.ToString()??""
                                        )
                                    )
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
                            if (chaps.Count == 0) break;
                            AllStories.AddRange(chaps);
                            chapDone.Add(url);
                            var nextUrls= doc.QuerySelectorAll("#dschuong")
                                .SelectMany(o => o.QuerySelectorAll("td")
                                    .Where(p => !string.IsNullOrEmpty(
                                            p.Attributes["colspan"]?.Value?.ToString()??""
                                        )
                                    )
                                    .ToList()
                                )
                                .Where(o => o != null)
                                .SelectMany(o => o.QuerySelectorAll("a")
                                    .Select(p => 
                                       p.Attributes["href"]?.Value
                                    ).ToList())
                                .Where(o => o!=null)
                               
                                .ToList()
                                .SkipLast(2)
                                .ToList();
                            var newUrls=nextUrls.Where(nu => !chapHistories.Contains(nu));
                            chapHistories.AddRange(newUrls);

                            url = chapHistories.Where(o => !chapDone.Contains(o)).FirstOrDefault();
                            if (!isLast && string.IsNullOrEmpty(url))
                            {
                                isLast = true;
                                url = chapHistories.Last();
                                var pn = Regex.Match(url, "page(\\d+$)");
                                var num = Int32.Parse(pn.Groups[1].Value);
                                url = Regex.Replace(url, "page\\d+$", "page" + (num + 1));
                            }
                            // get chapters link
                            // get next list chapters

                        } 

                        found = !string.IsNullOrEmpty(url);
                        
                    }
                    catch (Exception ex)
                    {
                        
                        Cfg.LogQueue.Enqueue($"Error {url} -{ex.StackTrace}");
                      
                        //Task.Delay(1000).Wait();
                    }
                }
            }

            
        }


      
    }
}
