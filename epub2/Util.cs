using System.Net;
using System.Security.Cryptography;
using System.Text;
using Serilog;
using System.Diagnostics;
using System.Runtime.InteropServices;

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
            _logger.Information("Downloading: "+url +" => "+fileName);
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

            using var handler = new HttpClientHandler()
            {
                Proxy = new WebProxy("8c5906b99fbd1c0bcd0f916d545c565af7dbb24d2cae1aa14e9a67673f5862de75661dc8d195273459c85c7bc73d9d96ba707f7546586c8f1bb0806605478a46812d73d8de1e1ff1ebd8f9a92f3918d2login:e7tws07mo9c9@proxy.toolip.io:31113"),
                UseProxy = true
            };
            using var client = new HttpClient(handler);
            // using var client = new HttpClient();
            // client.GetByteArrayAsync(url);
            byte[] bytes;
            if (true)
            {
                var proxyUrl = $"-x \"socks5h://129.226.194.214:1081\"";
                // var proxyUrl = "--socks4 202.123.178.202:30208";
                var tempFile = Path.GetTempFileName();
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "curl.exe" : "curl",
                    // Arguments = $"-o \"{tempFile}\" -x \"{proxyUrl}\" \"{url}\"",
                    Arguments = $"-o \"{tempFile}\"  {proxyUrl}  \"{url}\"",
                    
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processStartInfo);
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    var error = await process.StandardError.ReadToEndAsync();
                    throw new Exception($"Curl error: {error}");
                }
                bytes = File.ReadAllBytes(tempFile);
                File.Delete(tempFile);
            }
            else
            {
                bytes = await client.GetByteArrayAsync(url);
            }
           
            if (bytes == null || (bytes.Length == 0)) throw new Exception("Can not get html data");
            var str = Encoding.UTF8.GetString(bytes);
            await File.WriteAllTextAsync(fileName, str);
            return fileName;
            // }
        }
    }
}