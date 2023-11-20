using System.Reflection;
using epub2.Stories;

namespace epub2;

public class StoryFactory(IServiceProvider serviceProvider,Config config)
{
    public IStorySite GetSiteDriver()
    {
        var url = config.Url;
        var arr = url.Split("/");
        var domain = arr[2];
        return (Assembly.GetAssembly(typeof(Program))
            ?.ExportedTypes.Where(t => typeof(IStorySite).IsAssignableFrom(t) && typeof(IStorySite)!=t)
            .Where(cls =>
            {
                var attr = cls.GetCustomAttribute<SiteAttribute>();
                return attr != null && attr.Name.Contains(domain.ToLower());
            })
            .Select(cls => (IStorySite) serviceProvider.GetService(cls)!)
            // .ToList()
            .FirstOrDefault() ?? null) ??
               throw new InvalidOperationException("Cannot find class process for the domain");
       
        // return null;
    }
}