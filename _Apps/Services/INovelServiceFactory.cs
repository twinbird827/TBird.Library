using LanobeReader.Models;

namespace LanobeReader.Services;

public interface INovelServiceFactory
{
    INovelService GetService(SiteType siteType);
}
