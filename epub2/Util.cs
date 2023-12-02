using System.Security.Cryptography;
using System.Text;
using Serilog;

namespace epub2
{
    public class Util
    {
        private readonly Config _config;
        private readonly ILogger _logger;
        private readonly HttpClient _httpClient = new();
        private readonly MD5 _md5 = MD5.Create();

        public Util(Config config,ILogger logger)
        {
            // var md5 = MD5.Create();
            _config = config;
            _logger = logger;
            // _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", Config.StrUserAgent);
        }

        public string GetCacheFilename(string url)
        {
            using var md5 = MD5.Create();
            var hash = BitConverter.ToString(md5.ComputeHash(Encoding.UTF8.GetBytes(url))).Replace("-","");
            var fileName = Path.Combine(_config.CacheDirectory, hash);
            return fileName;
        }


        public async Task<string> DownloadAsync(string url)
        {
            var fileName = GetCacheFilename(url);
            _logger.Information("Downloading: "+url);
            if (File.Exists(fileName))
            {
                return fileName;
            }
            
            //var cookieContainer = new CookieContainer();
            //using (var handler = new HttpClientHandler() { CookieContainer = cookieContainer })
            //using (var client = new HttpClient(handler))
            //using (var client=new WebClient())
            //{
            //    client.Headers.Add("User-Agent", _cfg.StrUserAgent);
            //    var bytes=client.DownloadData(url);
            //    if (bytes == null || (bytes.Length == 0)) throw new Exception("Can not get html data");
            //    return Encoding.UTF8.GetString(bytes);
            //}
            // using (var client = new HttpClient())
            // {
            //     client.DefaultRequestHeaders.Add("User-Agent", Config.StrUserAgent);

            using var result = await _httpClient.GetAsync(url);
            result.EnsureSuccessStatusCode();
            var bytes = await result.Content.ReadAsByteArrayAsync();
            if (bytes == null || (bytes.Length == 0)) throw new Exception("Can not get html data");
            var str = Encoding.UTF8.GetString(bytes);
            await File.WriteAllTextAsync(fileName, str);
            return fileName;
            // }
        }
    }
}