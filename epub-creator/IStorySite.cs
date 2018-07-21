using AngleSharp.Dom.Html;

namespace epub_creator
{
    public interface IStorySite
    {
        void GetListChapters(string url);
        void SaveListChapters();
        ChapterContent GetChapterContent(ChapterInfo chapterInfo);
    }
}