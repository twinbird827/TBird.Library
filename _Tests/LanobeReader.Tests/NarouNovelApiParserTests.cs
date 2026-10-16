using LanobeReader.Models;
using LanobeReader.Services.Narou;
using NUnit.Framework;

namespace LanobeReader.Tests;

public class NarouNovelApiParserTests
{
    // #261: 更新チェックの一括取得は NovelId(小文字 ncode)で応答を引くため、キーの正規化と各項目の読み方を固定する。

    [Test]
    public void ParseSkipsAllcountAndNormalizesItems()
    {
        const string json = """
            [
              {"allcount": 2},
              {"ncode": "N1234AB", "title": "題名1", "writer": "作者1", "general_all_no": 10, "end": 0, "general_lastup": "2024-01-02 03:04:05"},
              {"ncode": "N5678CD", "title": "題名2", "writer": "作者2", "general_all_no": 3, "end": 1, "general_lastup": "2024-06-30 12:00:00"}
            ]
            """;

        var results = NarouNovelApiParser.Parse(json);

        Assert.That(results, Has.Count.EqualTo(2));
        Assert.That(results[0].SiteType, Is.EqualTo(SiteType.Narou));
        Assert.That(results[0].NovelId, Is.EqualTo("n1234ab"));
        Assert.That(results[0].Title, Is.EqualTo("題名1"));
        Assert.That(results[0].Author, Is.EqualTo("作者1"));
        Assert.That(results[0].TotalEpisodes, Is.EqualTo(10));
        Assert.That(results[0].IsCompleted, Is.True);
        Assert.That(results[0].LastUpdatedAt, Is.EqualTo("2024-01-01T18:04:05.0000000Z"));
        Assert.That(results[1].NovelId, Is.EqualTo("n5678cd"));
        Assert.That(results[1].IsCompleted, Is.False);
        Assert.That(results[1].LastUpdatedAt, Is.EqualTo("2024-06-30T03:00:00.0000000Z"));
    }

    [Test]
    public void ParseSkipsItemsWithoutNcodeOrTitleAndDefaultsMissingFields()
    {
        const string json = """
            [
              {"allcount": 4},
              {"title": "ncode なし", "general_all_no": 1},
              {"ncode": "n0001aa", "general_all_no": 1},
              {"ncode": "n0002bb", "title": "作者なし", "general_all_no": 5},
              {"ncode": "n0003cc", "title": "話数なし", "writer": "作者3"}
            ]
            """;

        var results = NarouNovelApiParser.Parse(json);

        Assert.That(results.Select(r => r.NovelId), Is.EqualTo(new[] { "n0002bb", "n0003cc" }));
        Assert.That(results[0].Author, Is.EqualTo(""));
        Assert.That(results[0].TotalEpisodes, Is.EqualTo(5));
        Assert.That(results[1].Author, Is.EqualTo("作者3"));
        Assert.That(results[1].TotalEpisodes, Is.EqualTo(0));
    }

    [Test]
    public void ParseReturnsEmptyForAllcountOnly()
    {
        Assert.That(NarouNovelApiParser.Parse("""[{"allcount": 0}]"""), Is.Empty);
    }
}
