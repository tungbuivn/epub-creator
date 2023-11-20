using AngleSharp.Html.Dom;

namespace epub2.Stories
{
    public interface IStorySite
    {
        
        /// <summary>
        /// return chapter list link anh chapter title from a content
        /// </summary>
        /// <param name="document"></param>
        /// <returns></returns>
        List<ChapterInfo> GetListChapters(IHtmlDocument document);
        /// <summary>
        /// return list of pagination which contain list of chapters, donot require order of the paging
        /// </summary>
        /// <param name="document"></param>
        /// <returns></returns>
        List<string> GetListPages(IHtmlDocument document);

        /// <summary>
        /// return page number from a url
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        int GetPageNumber(string url);
        /// <summary>
        /// return content html of the chapter
        /// </summary>
        /// <param name="document"></param>
        /// <returns></returns>
        string GetChapterContent(IHtmlDocument document);
    }
}