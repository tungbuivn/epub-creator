using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading.Tasks.Dataflow;
using AngleSharp.Html.Parser;
using epub2.Stories;
using EPubFactory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
// using Microsoft.Extensions.Logging;
using Serilog;

namespace epub2;

public class Main : IHostedService
{
    private readonly IStorySite _storySite;
    private readonly Util _util;
    private readonly Config _config;
    private readonly ILogger _logger;
    private readonly IHostApplicationLifetime _appLifetime;
    // private Task _run;

    public Main(IStorySite storySite,Util util,Config config,ILogger logger, IHostApplicationLifetime appLifetime)
    {
        _storySite = storySite;
        _util = util;
        _config = config;
        _logger = logger;
        _appLifetime = appLifetime;
        // appLifetime.ApplicationStarted.Register(OnStarted);
        // appLifetime.ApplicationStopping.Register(OnStopping);
        // appLifetime.ApplicationStopped.Register(OnStopped);
    }

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

    private async Task ProcessChapters()
    {
        var allChap = new List<string>();
        // _chapterList = new List<UrlData>();
        var chapterDone = new ManualResetEvent(false);
        var ePubStream = File.Create($"{_config.EPubFile}.epub");

        var writer = await EPubWriter.CreateWriterAsync(
            ePubStream,
            Regex.Replace(Regex.Replace($"{_config.EPubFile}", "[^a-zA-Z0-9]", " "), "\\s+", " "),
            "tntdb",
            "08915002",
            new CultureInfo("vi-VN"));
        writer.Publisher = "tntdb";
        var urlQueue = new BufferBlock<UrlData>(new DataflowBlockOptions()
        {
            // BoundedCapacity = 100
            // EnsureOrdered = true
        });
        var downloadQueue = new TransformBlock<UrlData, UrlData>(async (data) =>
        {
            while (true)
            {
                try
                {
                 
                    data.FileName = await _util.DownloadAsync(data.Url);
                    
                    return data;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex,"Error download url: "+data.Url);
                    await Task.Delay(1000);
                }
            }
           
        }, new ExecutionDataflowBlockOptions()
        {
            EnsureOrdered = true,
            // MaxDegreeOfParallelism = 50,
            // SingleProducerConstrained = true
        });
        var processChapters = new ActionBlock<UrlData>(async (d) =>
        {

            try
            {
                var parser=new HtmlParser();
                var content = File.ReadAllText(Path.Combine(_config.CacheDirectory, _util.GetCacheFilename(d.Url)));
                var document = parser.ParseDocument(content);
           
                d.ChapterInfos=_storySite.GetListChapters( document);
                // var cs = chapterList.Select(o => o.Url).ToList();
                var pages = _storySite.GetListPages( document).Where(link=>!allChap.Contains(link)).Distinct().ToList();
                foreach (var link in pages)
                {
                    await urlQueue.SendAsync(new UrlData()
                    {
                        Url = link,
                        Type = UrlType.LIST_CHAPTERS
                    });
                }
                allChap.AddRange(pages);

                d.Page = _storySite.GetPageNumber(d.Url);
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
                _logger.Error(e,"Loi:");
                throw;
            }
            
            
        });
        var processContent = new ActionBlock<UrlData>(async (d) =>
        {
            var parser=new HtmlParser();
            var content = File.ReadAllText(Path.Combine(_config.CacheDirectory, _util.GetCacheFilename(d.Url)));
            var document = parser.ParseDocument(content);
            // var text = document.Body;
            document.QuerySelectorAll("script").ToList().ForEach(o => o.OuterHtml = "\n");
            d.ChapterContent.Content = _storySite.GetChapterContent(document);
            GenerateHtml(d.ChapterContent);

            await writer.AddChapterAsync(
                d.ChapterContent.FileName,
                d.ChapterContent.Title,
                d.ChapterContent.Content);
            // _logger.Information("done:" + d);
            await Task.CompletedTask;
        });
        urlQueue.LinkTo(downloadQueue);

        downloadQueue.LinkTo(processChapters, d => d.Type == UrlType.LIST_CHAPTERS);
        downloadQueue.LinkTo(processContent,d => d.Type == UrlType.CONTENT);
        // not valid data
        downloadQueue.LinkTo(DataflowBlock.NullTarget<UrlData>());
        allChap.Add(_config.Url);
        await urlQueue.SendAsync(new UrlData()
        {
            Type = UrlType.LIST_CHAPTERS,
            Url = _config.Url
        });
        chapterDone.WaitOne();
        lock (Clk)
        {
            _chapterList=_chapterList.OrderBy(a=>a.Page).ToList();
            if (_chapterList.Any(p => p.Page == 1))
            {
                if (_chapterList.Any(p => p.Page == 0))
                {
                    _chapterList = _chapterList.Where(o => o.Page != 1).ToList();
                }
            }
        }
        
       
        var idx = 0;
        foreach (var chap in _chapterList)
        {
            foreach (var it in chap.ChapterInfos)
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
        
        await writer.WriteEndOfPackageAsync();
        writer.Dispose();
       
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
        foreach (var p in _config.BlackList.Value)
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
                await ProcessChapters();
            }
          
            finally
            {
                _appLifetime.StopApplication();
            }
      
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}