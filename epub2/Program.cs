// See https://aka.ms/new-console-template for more information

using System.Reflection;
using System.Runtime.CompilerServices;
using epub2;
using epub2.Stories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Neleus.DependencyInjection.Extensions;
using Serilog;
using Serilog.Exceptions;

// var logger = new LoggerConfiguration()
//     .WriteTo.Console()
//     .CreateLogger();
var builder = Host.CreateDefaultBuilder(args)
    .ConfigureLogging(loggingBuilder =>
    {
        loggingBuilder.ClearProviders();
        loggingBuilder.AddSerilog();

    })
    .ConfigureServices((bd,serviceCollection) =>
    {
        bd.HostingEnvironment.ContentRootPath = Directory.GetCurrentDirectory();
        serviceCollection.AddSingleton<Config>(c=>new Config(args,c.GetRequiredService<IHostEnvironment>()));
        serviceCollection.AddHostedService<Main>();
        serviceCollection.AddSerilog(new LoggerConfiguration()
            .Enrich.WithExceptionDetails()
            .WriteTo.Console()
            .CreateLogger());
        // register all class handler
        Assembly.GetAssembly(typeof(Program))
            ?.ExportedTypes.Where(t => typeof(IStorySite).IsAssignableFrom(t) && typeof(IStorySite)!=t)
            .ToList()
            .ForEach(cls => serviceCollection.AddSingleton(cls));
        
        serviceCollection.AddSingleton<StoryFactory>();
        serviceCollection.AddSingleton<IStorySite>((svc) =>
        {
            var cfg = svc.GetRequiredService<StoryFactory>();
            return cfg.GetSiteDriver();
        });
        serviceCollection.AddSingleton<Util>();
    })
    
    .UseConsoleLifetime();

// builder.   Services.AddSingleton(c => logger);
// services.AddHttpClient();
// services.AddDbContext<MyDbContext>();
var host = builder.Build();
// await host.Services.GetRequiredService<Main>().Run();
await host.RunAsync();

// await host.Services.GetService<Main>().Run();
// code

// await host.RunAsync();