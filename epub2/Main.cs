using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading.Tasks.Dataflow;
using AngleSharp;
using AngleSharp.Html.Parser;
using Aspose.Pdf.Facades;
using epub2.Stories;
using EPubBook;
// using Aspose.Html;
// using Aspose.Pdf;
// using Aspose.Html.Converters;
// using Aspose.Html.Saving;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
// using Microsoft.Extensions.Logging;
using Serilog;

namespace epub2;

public class Main(Epub epub, IStorySite storySite, Util util, Config config, ILogger logger,
        IHostApplicationLifetime appLifetime)
    : IHostedService
{
    // private readonly Epub _epub = epub;

    // private Task _run;

    // appLifetime.ApplicationStarted.Register(OnStarted);
    // appLifetime.ApplicationStopping.Register(OnStopping);
    // appLifetime.ApplicationStopped.Register(OnStopped);

    // private void OnStopping()
    // {
    //     _logger.Information("3. OnStopping has been called.");
    // }
    //
    // private void OnStopped()
    // {
    //     _logger.Information("5. OnStopped has been called.");
    // }
    //
    // private void OnStarted()
    // {
    //     _logger.Information("2. OnStarted has been called.");
    //   
    //    
    // }
    private static readonly object Clk = new();
    private List<UrlData> _chapterList=new();

    // private async Task ProcessChapters()
    // {
    //     epub.AddChapter("file-01.xhtml","title","content");
    //     epub.Save(new EPubOptions()
    //     {
    //         Title = "Test some thing",
    //         FileName="./xxtrx.zip"
    //     });
    //     await Task.CompletedTask;
    // }
    private async Task ProcessChapters(CancellationToken cancellationToken)
    {
       
        // return;
        var allChap = new List<string>();
        // _chapterList = new List<UrlData>();
        var chapterDone = new ManualResetEvent(false);
        
        // using var ePubStream = File.Create($"{config.EPubFile}.epub");
        //
        // using var writer = await EPubWriter.CreateWriterAsync(
        //     ePubStream,
        //     Regex.Replace(Regex.Replace($"{config.EPubFile}", "[^a-zA-Z0-9]", " "), "\\s+", " "),
        //     "tntdb",
        //     "08915002",
        //     new CultureInfo("vi-VN"));
        // writer.Publisher = "tntdb";
        var urlQueue = new BufferBlock<UrlData>(new DataflowBlockOptions()
        {
            // BoundedCapacity = 100
            // EnsureOrdered = true
        });
        var downloadQueue = new TransformBlock<UrlData, UrlData>(async (data) =>
        {
            data.FileName = "";
            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    Environment.Exit(0);
                }
                try
                {
                 
                    data.FileName = await util.DownloadAsync(data.Url);
                    
                    return data;
                }
                catch (Exception ex)
                {
                    logger.Error(ex,"Error download url: "+data.Url);
                    await Task.Delay(1000);
                }
            }

           
            
            return data;

        }, new ExecutionDataflowBlockOptions()
        {
            EnsureOrdered = true,
            MaxDegreeOfParallelism = 10,
            // SingleProducerConstrained = true
        });
        var processChapters = new ActionBlock<UrlData>(async (d) =>
        {
        
            try
            {
                var parser=new HtmlParser();
                var content = await File.ReadAllTextAsync(Path.Combine(config.CacheDirectory, util.GetCacheFilename(d.Url)));
                var document = parser.ParseDocument(content);
           
                d.ChapterInfos=storySite.GetListChapters( document)
                    .Where(o=>o.Link.ToLower().StartsWith("http"))
                    .ToList();
                // var cs = chapterList.Select(o => o.Url).ToList();
                var pages = storySite.GetListPages( document).Where(link=>!allChap.Contains(link)).Distinct()
                    .Where(o=>o.ToLower().StartsWith("http"))
                    .ToList();
                foreach (var link in pages)
                {
                    await urlQueue.SendAsync(new UrlData()
                    {
                        Url = link,
                        Type = UrlType.LIST_CHAPTERS
                    });
                }
                allChap.AddRange(pages);
        
                d.Page = storySite.GetPageNumber(d.Url);
                d.ChapterDone = true;
                lock (Clk)
                {
                    
                    _chapterList.Add(d);
                
                    if (_chapterList.All(o => o.ChapterDone))
                    {
                        if (_chapterList.Count==allChap.Count)
                        {
                            chapterDone.Set();
                        }
               
                    }
                }
                
            }
            catch (Exception e)
            {
                logger.Error(e,"Loi:");
                throw;
            }
            
            
        });
        var processContent = new ActionBlock<UrlData>(async (d) =>
        {
            logger.Information("Add chapter: "+d.ChapterContent.Title);
            var parser=new HtmlParser();
            var content = await File.ReadAllTextAsync(Path.Combine(config.CacheDirectory, util.GetCacheFilename(d.Url)));
            content = Regex.Replace(content,"<\\!--.*?-->", "");
            var document = parser.ParseDocument(content);
            // var text = document.Body;
            document.QuerySelectorAll("script").ToList().ForEach(o => o.OuterHtml = "\n");
            document.QuerySelectorAll("style").ToList().ForEach(o => o.OuterHtml = "\n");
            var html=storySite.GetChapterContent(document);
        
            html = parser.ParseFragment(html, null!)
                .ToHtml(new TextMarkupFormatter())
                
                .Trim();
            
            
            
            // var splstr = "xxoosdbdbooxx";
            // html = Regex.Replace(html, "</[a-zA-Z]+>", splstr);
            // html = Regex.Replace(html, "<.[^>]*.", splstr);
            // html = Regex.Replace(html,splstr,"\n");
            
            html = Regex.Replace(html,"\\n+","\n");
            d.ChapterContent.Content = html;
            GenerateHtml(d.ChapterContent);
            epub.AddChapter(d.ChapterContent.FileName,
                d.ChapterContent.Title,
                d.ChapterContent.Content);
            // await writer.AddChapterAsync(
            //     d.ChapterContent.FileName,
            //     d.ChapterContent.Title,
            //     d.ChapterContent.Content);
            // _logger.Information("done:" + d);
            await Task.CompletedTask;
        });
        urlQueue.LinkTo(downloadQueue);
        
        downloadQueue.LinkTo(processChapters, d =>   d.Type == UrlType.LIST_CHAPTERS);
        downloadQueue.LinkTo(processContent,d =>  d.Type == UrlType.CONTENT);
        // not valid data
        downloadQueue.LinkTo(DataflowBlock.NullTarget<UrlData>());
        allChap.Add(config.Url);
        await urlQueue.SendAsync(new UrlData()
        {
            Type = UrlType.LIST_CHAPTERS,
            Url = config.Url
        });
        chapterDone.WaitOne();
        List<ChapterInfo> allChaps;
        lock (Clk)
        {
            _chapterList=_chapterList.DistinctBy(o=>o.Page).OrderBy(a=>a.Page).ToList();
            if (_chapterList.Any(p => p.Page == 1))
            {
                if (_chapterList.Any(p => p.Page == 0))
                {
                    _chapterList = _chapterList.Where(o => o.Page != 1).ToList();
                }
            }
        
            allChaps = _chapterList.SelectMany(ch => ch.ChapterInfos).DistinctBy(c=>c.Link).ToList();
        }
        
        
        var idx = 0;
        foreach (var it in allChaps)
        {
            // foreach (var it in chap.ChapterInfos)
            {
                idx++;
                it.Idx = idx;
                
                await urlQueue.SendAsync(new UrlData()
                {
                    Type = UrlType.CONTENT,
                    Url = it.Link,
                    ChapterContent = new()
                    {
                        Idx = idx,
                        Title = it.Title
                    }
                });
            }
          
        }
        
        foreach (var df in new IDataflowBlock[]{urlQueue,downloadQueue,processContent})
        {
            df.Complete();
            await df.Completion;
        }
        
        
        // urlQueue.Complete();
        // await urlQueue.Completion;
        // downloadQueue.Complete();
        // await downloadQueue.Completion;
        // processContent.Complete();
        // await processContent.Completion;
        epub.Save(new EPubOptions()
        {
            FileName = $"{config.EPubFile}.epub",
            Title = Regex.Replace(Regex.Replace($"{config.EPubFile}", "[^a-zA-Z0-9]", " "), "\\s+", " ")
        });
        
      

        // using var stream = File.OpenRead($"{config.EPubFile}.epub");
        // var options = new PdfSaveOptions();
        // Aspose.Html.License;
        // Converter.ConvertEPUB(stream, options, "output.pdf");
        // await writer.WriteEndOfPackageAsync();
        // writer.Dispose();
        //
    }

    private void GenerateHtml(ChapterContent chapterContent)
    {
        var parser=new HtmlParser();
        // var content = File.ReadAllText(Path.Combine(Cfg.StoryDataDirectory, chapterInfo.Idx.ToString()));
        var document = parser.ParseDocument("");
        var docTitle = document.CreateElement("p");
        var docstrong=document.CreateElement("strong");
        docstrong.TextContent = chapterContent.Title;
        docTitle.AppendChild(docstrong);
        var textClip = chapterContent.Content;
        foreach (var p in config.BlackList.Value)
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
       
        
        
        var finalHtml =
            $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><html xmlns=\"http://www.w3.org/1999/xhtml\"><head><title /></head><body>{docTitle.OuterHtml}{js}</body></html>";
        var fileName = $"chapter-{chapterContent.Idx}.xhtml";
        chapterContent.FileName = fileName;
        chapterContent.Content = finalHtml;
     

    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
      
            try
            {
                await ProcessChapters(cancellationToken);
            }
          
            finally
            {
                appLifetime.StopApplication();
            }
      
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}