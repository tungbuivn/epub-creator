using System;
using System.Runtime.Caching;
using Autofac;
using Autofac.Core;

namespace epub_creator
{
    class Program
    {
        private static void Main(string[] args)
        {
            System.Net.ServicePointManager.DefaultConnectionLimit = 10;
            var builder = new ContainerBuilder();
            var config = new Config(args);
            builder.Register(c => config).AsSelf();
            builder.RegisterType<Main>().AsSelf();
            builder.RegisterType<Util>().AsSelf();
            builder.RegisterType<TruyenFull>()
                .As<IStorySite>()
                .Named<IStorySite>("truyenfull.vn")
                .AsSelf();
            builder.RegisterType<WebTruyen>()
                .As<IStorySite>()
                .Named<IStorySite>("webtruyen.com")
                .AsSelf();

            using (var container = builder.Build())
            {
                try
                {
                    config.Container = container;
                    var main = container.Resolve<Main>();
                    main.Run();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.StackTrace);
                }
                
            }




            //            Console.ReadKey();
        }



    }


}