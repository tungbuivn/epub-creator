using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;

namespace epub2.Stories
{
    [Site("thichdoctruyen.vip")]
    // ReSharper disable once UnusedType.Global
    public class ThichDocTruyen : BaseStory, IStorySite
    {
        public List<ChapterInfo> GetListChapters(IHtmlDocument document)
        {
            var chaps = document.QuerySelectorAll("#dschuong")
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
            return chaps;
        }
        

        public List<string> GetListPages(IHtmlDocument document)
        {
            var nextUrls = document.QuerySelectorAll("#dschuong")
                .SelectMany(o => o.QuerySelectorAll("td")
                    .Where(p => !string.IsNullOrEmpty(
                            p.Attributes["colspan"]?.Value?.ToString() ?? ""
                        )
                    )
                    .ToList()
                )
                .Where(o => o != null)
                .SelectMany(o => o.QuerySelectorAll("a")
                    .Select(p =>
                        p.Attributes["href"]?.Value??""
                    ).ToList())
                .Where(o => o != null)

                .ToList();
                // .SkipLast(2)
                // .ToList();
            return nextUrls;
        }

        public int GetPageNumber(string url)
        {
            var pn = Regex.Match(url, "page(\\d+$)");
            if (pn.Success)
            {
                var num = Int32.Parse(pn.Groups[1].Value);
                return num;
            }

            return 0;
        }

        public string GetChapterContent(IHtmlDocument document)
        {
            // var parser=new HtmlParser();
            // var content = File.ReadAllText(Path.Combine(Cfg.StoryDataDirectory, chapterInfo.Idx.ToString()));
            // var document = parser.ParseDocument(content);
            var text = document.QuerySelector(".boxview");
            text!.OuterHtml = "<div>" + text.InnerHtml + "</div>";
            text.QuerySelectorAll("script").ToList().ForEach(o => o.OuterHtml = "\n");
           


            text.QuerySelectorAll("*")
                .Select(o => new { o, attr = o.Attributes["style"]?.Value })
                .Where(o => o.attr != null)
                .Where(o => o.attr!.Contains("font-size:0px")
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
            var textClip = text.TextContent;
            return textClip;

        }
    }
}
