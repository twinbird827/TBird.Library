using LanobeReader.Services.Narou;
using NUnit.Framework;
using TBird.Maui.Web;

namespace LanobeReader.Tests;

public class NarouEpisodeParserTests
{
    // #195: 前書き div が本文 div より前にあると前書きを本文として返していたため、前書き・後書きを持つ HTML から本文だけが返ることを固定する。

    [Test]
    public async Task ExtractContentSkipsPrefaceAndAfterword()
    {
        const string html = """
            <div class="p-novel__body">
              <div class="js-novel-text p-novel__text p-novel__text--preface"><p>前書き1</p><p>前書き2</p></div>
              <div class="js-novel-text p-novel__text"><p>本文1</p><p>本文2</p></div>
              <div class="js-novel-text p-novel__text p-novel__text--afterword"><p>後書き1</p><p>後書き2</p></div>
            </div>
            """;
        var document = await AngleSharpHelper.ParseAsync(html);

        Assert.That(NarouEpisodeParser.ExtractContent(document), Is.EqualTo("本文1\n本文2"));
    }
}
