using LanobeReader.Models;
using LanobeReader.Services.Kakuyomu;
using LanobeReader.Services.Narou;

namespace LanobeReader.Services;

public class NovelServiceFactory : INovelServiceFactory
{
    private readonly NarouApiService _narouService;
    private readonly KakuyomuApiService _kakuyomuService;

    public NovelServiceFactory(NarouApiService narouService, KakuyomuApiService kakuyomuService)
    {
        _narouService = narouService;
        _kakuyomuService = kakuyomuService;
    }

    public INovelService GetService(SiteType siteType) => siteType switch
    {
        SiteType.Narou => _narouService,
        SiteType.Kakuyomu => _kakuyomuService,
        _ => throw new ArgumentOutOfRangeException(nameof(siteType), siteType, "Unknown site type"),
    };
}
