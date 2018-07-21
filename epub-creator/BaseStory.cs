using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Serilog;

namespace epub_creator
{
    public abstract class BaseStory 
    {
     
      
        public  Config Cfg { get; set; }
        public   ILogger Log { get; set; }
        public    Util Util { get; set; }
        protected  List<ChapterInfo> AllStories = new List<ChapterInfo>();

//        protected BaseStory(Config cfg, Config log, Util util)
//        {
//            Cfg = cfg;
//            Log = log;
//            _util = util;
//        }
        public void SaveListChapters()
        {
            if (AllStories.Any())
            {
                File.WriteAllText(Path.Combine(Cfg.StoryDirectory, "chapters.json"),
                    JsonConvert.SerializeObject(AllStories.Select((o, i) =>
                        {
                            o.Idx = i;
                            return o;
                        }).ToList()
                    )
                );
            }
        }
    }
}