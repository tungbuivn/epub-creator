using System.Collections;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace epub2.Stories
{
    [Site("truyenchu.vn")]
    public class TruyenChu : BaseStory, IStorySite
    {
    

        
        public List<ChapterInfo> GetListChapters(IHtmlDocument document)
        {
            var text = document
                .QuerySelectorAll("script")
                .Select(o=>o.InnerHtml).FirstOrDefault(o => o.Contains("list_chapter"));
            var text1 = text?.Split("listChapter =").LastOrDefault();
            var jo = JsonConvert.DeserializeObject(text1?.Split("};").FirstOrDefault()+"}");
            var parser=new HtmlParser();
            INodeList? doc=null;
            if (jo != null)
                foreach (JToken k in ((JObject)jo).Children())
                {
                    if (k.Path == "list_chapter")
                    {
                        doc = parser.ParseFragment((string)k.First() ?? string.Empty, null);
                        break;
                    }
                }


            var chaps = doc?.QuerySelectorAll(".list-chapter")
                .SelectMany(o => o.QuerySelectorAll("li")
                    
                    .ToList()
                )
                .Where(o => o != null)
                .SelectMany(o => o.QuerySelectorAll("a")
                    .Select(p => new ChapterInfo()
                    {
                        Title = p.Text(),
                        Link = "https://truyenchu.vn"+p.Attributes["href"]?.Value
                    }).ToList())
                .Where(o => !string.IsNullOrEmpty(o.Link))
                .ToList();
            return chaps??new List<ChapterInfo>();
        }

        public List<string> GetListPages(IHtmlDocument document)
        {
            var text = document.QuerySelectorAll("script")
                .Select(o =>  o.InnerHtml)
                .Where(o=>o.Contains("list_chapter")).FirstOrDefault();
            var text1 = text.Split("listChapter =").LastOrDefault();
            var jo = JsonConvert.DeserializeObject(text1.Split("};").FirstOrDefault()+"}");
            var parser=new HtmlParser();
            INodeList doc=null;
            foreach (JToken k in ((JObject)jo).Children())
            {
                if (k.Path == "pagination")
                {
                   
                    doc = parser.ParseFragment((string)k.First(),null);
                    break;
                }
               
            }
            
            
            var links = doc?.QuerySelector(".pagination")
                .QuerySelectorAll("a")
                .Select(o =>  o.Attributes["href"].Value)
                .Where(v=>v.StartsWith("/"))
                .Select(v=>"https://truyenchu.vn"+v)
                .ToList();
            return links??new List<string>();
            // throw new NotImplementedException();
        }

        public int GetPageNumber(string url)
        {
            var m = Regex.Match(url, "page=(\\d+)");
            if (m.Success)
            {
                return int.Parse(m.Groups[1].Value);
            }

            return 0;
        }

        public string GetChapterContent(IHtmlDocument document)
        {
            var text = document.QuerySelector(".chapter-c");
            // var html=text.InnerHtml.Replace()
            return text?.InnerHtml??"";
        }
    }
}
