using LanobeReader.Models;

namespace LanobeReader.Services;

public class NovelServiceFactory : INovelServiceFactory
{
    private readonly Dictionary<SiteType, INovelService> _services;

    public NovelServiceFactory(IEnumerable<INovelService> services)
    {
        _services = services.ToDictionary(s => s.SiteType);
    }

    public INovelService GetService(SiteType siteType)
    {
        if (_services.TryGetValue(siteType, out var service)) return service;
        throw new ArgumentOutOfRangeException(nameof(siteType), siteType, "Unknown site type");
    }
}
