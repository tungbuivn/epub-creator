using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace epub_creator
{
    public class Util
    {
        private readonly Config _cfg;
        public Util(Config cfg)
        {
            _cfg = cfg;
        }
        public string DownloadUrl(string url)
        {
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
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", _cfg.StrUserAgent);

                using (var result = client.GetAsync(url).Result)
                {
                    result.EnsureSuccessStatusCode();
                    var bytes = result.Content.ReadAsByteArrayAsync().Result;
                    if (bytes == null || (bytes.Length == 0)) throw new Exception("Can not get html data");
                    return Encoding.UTF8.GetString(bytes);
                }
            }
        }
    }
}
