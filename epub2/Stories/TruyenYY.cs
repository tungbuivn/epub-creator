using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;

namespace epub2.Stories
{
    [Site("truyenyy.vip")]
    public class TruyenYY : BaseStory, IStorySite
    {
        private string SiteUrl = "https://truyenyy.vip";
        private string listChapPath = "";
        private Util _util;
        public TruyenYY(Util util)
        {
            _util = util;
        }
       
      
      


        public List<ChapterInfo> GetListChapters(IHtmlDocument document)
        {
            var paging = document.QuerySelector(".pagination");
            if (paging == null)
            {
                var href=document.QuerySelectorAll(".nav-link")
                    .Select(o=>o.GetAttribute("href"))
                    .Where(o=>Regex.IsMatch(o,"/danh-sach-chuong"))
                    .FirstOrDefault();
                if (!string.IsNullOrEmpty(href))
                {
                    var url=SiteUrl+href;
                    var parser=new HtmlParser();
                    var content = _util.DownloadAsync(url).Result;
                    document = parser.ParseDocument(File.ReadAllText(content));
                    
                }
            }
          
            var chapshtml = document.QuerySelectorAll(".table-rounded td");
                var chaps=chapshtml
                    // .SelectMany(o=>o.QuerySelectorAll("a"))
        .Select(o => new ChapterInfo()
        {
            Title = o.QuerySelector("a")?.Text(),
            Link =SiteUrl+ o.QuerySelector("a")?.Attributes["href"]?.Value
        })
        
        .ToList();

    // Group chapters by every 3 rows
    var groupedChaps = chaps
        .Select((chapter, index) => new { chapter, index })
        .GroupBy(x => x.index / 3, x => x.chapter)
        .Select(o=>o.First())  
        .ToList();
                // .SelectMany(o => o.QuerySelectorAll("ul")
                //     .Where(p => p.Attributes["class"]?.Value?.ToString()
                //                     .Contains("list-chapter") ??
                //                 false)
                //     .ToList()
                // )
                // .Where(o => o != null)
                // .SelectMany(o => o.QuerySelectorAll("a")
                //     .Select(p => new ChapterInfo()
                //     {
                //         Title = p.Text(),
                //         Link = p.Attributes["href"]?.Value
                //     }).ToList())
                // .Where(o => !string.IsNullOrEmpty(o.Link))
                // .ToList();
                if (string.IsNullOrEmpty(listChapPath))
                {
                    var link = groupedChaps.FirstOrDefault().Link.Split("/").ToList();
                    link.Remove(link.Last());
                    listChapPath=string.Join("/",link);
                }
                
            return groupedChaps;
        }

        public List<string> GetListPages(IHtmlDocument document)
        {
            var links = document.QuerySelector(".pagination");
            if (links == null)
            {
               
                    var href=document.QuerySelectorAll(".nav-link")
                        .Select(o=>o.GetAttribute("href"))
                        .Where(o=>Regex.IsMatch(o,"/danh-sach-chuong"))
                        .FirstOrDefault();
                    if (!string.IsNullOrEmpty(href))
                    {
                        var url=SiteUrl+href;
                        var parser=new HtmlParser();
                        var content = _util.DownloadAsync(url).Result;
                        document = parser.ParseDocument(File.ReadAllText(content));
                        links = document.QuerySelector(".pagination");
                    }
                
            }

            // var orgLink = document.QuerySelectorAll("a")
            //     .Select(o => o.GetAttribute("href"))
            //     .Where(o => Regex.IsMatch(o,"\\/chuong\\-"))
            //     .FirstOrDefault();
            // var lstLink = orgLink.Split("/").ToList();
            // lstLink.Remove(lstLink.Last());
            // orgLink = SiteUrl+ String.Join("/", lstLink);
                
                var rs=links.QuerySelectorAll("a")
                    .Select(o =>  o.Attributes["href"].Value)
                .Where(o=>o.StartsWith("?p="))
                .Select(o => listChapPath+"/danh-sach-chuong/"+ o)
                
                .ToList();
            return rs;
            // throw new NotImplementedException();
        }

        public int GetPageNumber(string url)
        {
            var m = Regex.Match(url, "/\\?p=(\\d+)");
            if (m.Success)
            {
                return int.Parse(m.Groups[1].Value);
            }

            return 0;
        }

        public string GetChapterContent(IHtmlDocument document)
        {
            // <div class="chap-content serif-font no-select">
            //     
            //     <div id="inner_chap_content_1"><div><ol>
            var headerel=document.QuerySelector("#inner_chap_content_1 ol");
            headerel?.Remove();
            var text = document.QuerySelector(".chap-content");

            // var html=text.InnerHtml.Replace()
            return text?.InnerHtml??"";
        }

        public string GetSiteAttribute()
        {
            var siteAttribute = (SiteAttribute)Attribute.GetCustomAttribute(typeof(TruyenYY), typeof(SiteAttribute));
            return siteAttribute?.Name?.FirstOrDefault(); // Assuming SiteAttribute has a property SiteName
        }
    }
}
