using System.Xml;
using AngleSharp.Attributes;
using AngleSharp.Dom;
using AngleSharp.Xml;
using AngleSharp.Xml.Dom;
using Microsoft.Extensions.FileProviders;
using Newtonsoft.Json;

namespace EPubBook;

using System.IO.Compression;
using System.Text;

// [DomName("item")]
// public class ItemChapter :Element, IElement
// {
//     [DomName("id")]
//     public string Id { get; set; } 
// }
public class Epub : IDisposable
{
    // private readonly MemoryStream _ms ;
    // private List<string> ContentOpf = new();
    // private ZipArchive zipArchive ;
    private readonly List<EPubData> _listChapters = new();

    public Epub()
    {
    }

    // private ZipArchive EpubFile = null;
    public void AddChapter(string fileName, string title, string content)
    {
        _listChapters.Add(new EPubData()
        {
            Title = title,
            Content = content,
            FileName = fileName
        });
        // using ZipArchive archive = new ZipArchive(_ms, ZipArchiveMode.Create);
        // var entry = epubFile.CreateEntry("mimetype");
        // using var writer = new StreamWriter(entry.Open());
        // writer.WriteLine("Information about this package.");
        // writer.WriteLine("========================");
    }

    void BuildMeta(ZipArchive zipArchive)
    {
        var embeddedProvider = new EmbeddedFileProvider(GetType().Assembly);
        using var reader = embeddedProvider.GetFileInfo("Resources/META_INF/container.xml").CreateReadStream();
        // using (var m = new MemoryStream())
        var readmeEntry = zipArchive.CreateEntry("META-INF/container.xml");
        using var fs = readmeEntry.Open();
        reader.CopyTo(fs);
    }

    private void BuildMimetype(ZipArchive zipArchive)
    {
        var embeddedProvider = new EmbeddedFileProvider(GetType().Assembly);
        using var reader = embeddedProvider.GetFileInfo("Resources/mimetype").CreateReadStream();
        // using (var m = new MemoryStream())
        var entry = zipArchive.CreateEntry("mimetype");
        using var fs = entry.Open();
        reader.CopyTo(fs);
    }

    public void Save(EPubOptions options)
    {
        using FileStream zipToOpen = File.Create(options.FileName);
        // _ms = new MemoryStream();
        using var epub = new ZipArchive(zipToOpen, ZipArchiveMode.Create);
        BuildMimetype(epub);
        BuildMeta(epub);
        BuildContent(epub, options);

        // using (var m = new MemoryStream())
        // var entry = zipArchive.CreateEntry("mimetype");
        // using var fs = entry.Open();
        // reader.CopyTo(fs);
        // EPubBook/Resources/content.opf

        //
        // epubFile.Dispose();
        //
        // _ms.Seek(0, SeekOrigin.Begin);
        // zipToOpen.Write(_ms.ToArray());

        // _ms.CopyTo(zipToOpen);
    }

    string GetRes(string filename)
    {
        var embeddedProvider = new EmbeddedFileProvider(GetType().Assembly);
        using var reader = embeddedProvider.GetFileInfo(filename).CreateReadStream();
        using var m = new MemoryStream();
        reader.CopyTo(m);
        return Encoding.UTF8.GetString(m.ToArray());
    }

    

    private void BuildContent(ZipArchive epub, EPubOptions ePubOptions)
    {
        XmlDocument doc = new XmlDocument();
        var parser = new AngleSharp.Xml.Parser.XmlParser();


        var contentString = GetRes("Resources/content.opf");
        var tocString = GetRes("Resources/toc.ncx");
        // doc.LoadXml(str);
        var contentXml = parser.ParseDocument(contentString);
        var tocXml = parser.ParseDocument(tocString);
        contentXml.QuerySelector("title")!.TextContent = ePubOptions.Title;
        tocXml.QuerySelector("docTitle > text")!.TextContent = ePubOptions.Title;
        var navMap = tocXml.QuerySelector("navMap")!;
        navMap.Empty();
        contentXml.QuerySelector("date")!.TextContent = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        // var dso=JsonConvert.DeserializeXmlNode(str);
        var manifest = contentXml.QuerySelector("manifest")!;
        var spine = contentXml.QuerySelector("spine")!;
        spine.Empty();
        manifest.Empty();
        var ch = 0;
        _listChapters.ForEach(e =>
        {
            ch++;
            var id = $"chapter{ch}";
           
            IElement item = contentXml.CreateElement("item");
            item.SetAttribute("id", id);
            // href="chapter-1.xhtml" media-type="application/xhtml+xml"
            item.SetAttribute("href", e.FileName);
            item.SetAttribute("media-type", "application/xhtml+xml");
            item.AsSelfClosing();
            // var xl = item as AngleSharp.Dom.xml;

            manifest.AppendChild(item);

            item = contentXml.CreateElement("itemref");
            item.SetAttribute("idref", id);
            item.AsSelfClosing();
            spine.AppendChild(item);
            // <itemref idref="chapter1" />

            var navPoint = tocXml.CreateElement("navPoint");
            navPoint.SetAttribute("id",$"navPoint-{ch}");
            navPoint.SetAttribute("playOrder",$"{ch}");
            var navLabel = tocXml.CreateElement("navLabel");
            var text = tocXml.CreateElement("text");
            text.TextContent = e.Title;
            navLabel.AppendChild(text);
            navPoint.AppendChild(navLabel);
            var ctx = tocXml.CreateElement("content");
            ctx.SetAttribute("src",e.FileName);
            ctx.AsSelfClosing();
            navPoint.AppendChild(ctx);
            navMap.AppendChild(navPoint);

            // <navPoint id="navPoint-1" playOrder="1">
            //     <navLabel>
            //     <text>Chương 1: 1: Vào Sơn Môn</text>
            //     </navLabel>
            //     <content src="chapter-1.xhtml" />
            //     </navPoint>
            
            var entry = epub.CreateEntry(e.FileName);
            using (var writer = new StreamWriter(entry.Open()))
            {
                writer.Write(e.Content);

           
            }
        });


        // <item id="ncx" href="toc.ncx" media-type="application/x-dtbncx+xml" />
        var endEl = contentXml.CreateElement("item");
        endEl.SetAttribute("id", "ncx");
        endEl.SetAttribute("href", "toc.ncx");
        endEl.SetAttribute("media-type", "application/x-dtbncx+xml");
        endEl.AsSelfClosing();
        manifest.AppendChild(endEl);


        var entry = epub.CreateEntry("content.opf");
        using (var writer = new StreamWriter(entry.Open()))
        {
            writer.Write(contentXml.ToXml());

           
        }
        var entryToc = epub.CreateEntry("toc.ncx");
        using (var writerToc = new StreamWriter(entryToc.Open()))
        {
            writerToc.Write(tocXml.ToXml());
        }
    }

    public void Dispose()
    {
        // _ms.Close();
        // _ms.Dispose();
        // epubFile.Dispose();
    }
}