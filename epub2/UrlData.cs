using AngleSharp.Html.Dom;

namespace epub2;

public enum UrlType
{
    
    LIST_CHAPTERS,
    CONTENT
}
public class UrlData
{
    public bool ChapterDone { get; set; } = false;
    public UrlType Type { get; set; }
    public string Url { get; set; }
    public int Page { get; set; }
    public string FileName { get; set; }
    public IHtmlDocument Document { get; set; }
    public List<ChapterInfo> ChapterInfos = new();
    // public ChapterInfo Chapter { get; set; }
    public ChapterContent ChapterContent { get; set; } = null!;
}