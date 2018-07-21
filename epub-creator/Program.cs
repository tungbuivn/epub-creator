using System;
using System.Threading.Tasks;
using Autofac;
using Autofac.Core;
using Serilog;
using Serilog.Exceptions;
using Serilog.Formatting.Json;

namespace epub_creator
{
    public class Program
    {
        public static void RegisterDependencies(string[] args, Action<IContainer> cb)
        {
            var builder = new ContainerBuilder();
            var config = new Config(args);

            builder.Register(c => new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .Enrich.WithExceptionDetails()
                    .WriteTo.Debug()
//                    .WriteTo.Console()
                    .WriteTo.RollingFile(
                        new JsonFormatter(renderMessage: true), 
                        @"log-{Date}.txt")    
                  
                    .CreateLogger())
                .As<ILogger>().SingleInstance();
            builder.Register(c => config).AsSelf().SingleInstance();
            builder.RegisterType<Main>().AsSelf().SingleInstance();
            builder.RegisterType<Util>().AsSelf().SingleInstance();
            builder.RegisterType<TruyenFull>()
                .As<IStorySite>()
                .Named<IStorySite>("truyenfull.vn")
                .OnActivating(InitializeProperty)
                .AsSelf()
                .SingleInstance();
            builder.RegisterType<WebTruyen>()
                .As<IStorySite>()
                .Named<IStorySite>("webtruyen.com")
                .OnActivating(InitializeProperty)
                .AsSelf().SingleInstance();
            using (var container = builder.Build())
            {
                try
                {
                    config.Container = container;
                    cb(container);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.StackTrace);
                }
            }
        }

        private static void InitializeProperty(IActivatingEventArgs<BaseStory> e)
        {
            e.Instance.Cfg = e.Context.Resolve<Config>();
            e.Instance.Log = e.Context.Resolve<ILogger>();
            e.Instance.Util = e.Context.Resolve<Util>();
        }

        private static void Main(string[] args)
        {
//            System.Net.ServicePointManager.DefaultConnectionLimit = 50;
//            Console.OutputEncoding = System.Text.Encoding.UTF8;
            RegisterDependencies(args, (container) =>
            {
                var main = container.Resolve<Main>();
                var t=Task.Factory.StartNew(main.Run);
                var config = container.Resolve<Config>();
                var log = container.Resolve<ILogger>();
                while (!config.Done)
                {
                    if (config.logQueue.TryDequeue(out var act))
                    {
                        log.Debug(act);
                    }
                    else
                    {
                        Task.Delay(300).Wait();    
                    }

                    
                }
                while (config.logQueue.TryDequeue(out var act))
                {
                    log.Debug(act);
                }
                t.Wait();
            });


            //            Console.ReadKey();
        }
    }
}