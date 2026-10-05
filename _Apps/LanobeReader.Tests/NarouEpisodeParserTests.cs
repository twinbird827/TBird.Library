using LanobeReader.Helpers;
using LanobeReader.Services.Narou;
using NUnit.Framework;
using TBird.Maui.Web;

namespace LanobeReader.Tests;

public class NarouEpisodeParserTests
{
    // #195・#250: 前書き・本文・後書きが区画マーカー付きで DOM 順に並ぶことを固定する(#195 は前書きを本文として返していた)。

    [Test]
    public async Task ExtractContentKeepsSectionsInDomOrder()
    {
        const string html = """
            <div class="p-novel__body">
              <div class="js-novel-text p-novel__text p-novel__text--preface"><p>前書き1</p><p>前書き2</p></div>
              <div class="js-novel-text p-novel__text"><p>本文1</p><p>本文2</p></div>
              <div class="js-novel-text p-novel__text p-novel__text--afterword"><p>後書き1</p><p>後書き2</p></div>
            </div>
            """;
        var document = await AngleSharpHelper.ParseAsync(html);

        Assert.That(NarouEpisodeParser.ExtractContent(document), Is.EqualTo(string.Join("\n",
            EpisodeContentFormat.PrefaceStart, "前書き1", "前書き2",
            EpisodeContentFormat.BodyStart, "本文1", "本文2",
            EpisodeContentFormat.AfterwordStart, "後書き1", "後書き2")));
    }

    // #249: 挿絵の src はプロトコル相対のため、https の絶対 URL のマーカー行にする。

    [Test]
    public async Task ExtractContentResolvesProtocolRelativeImage()
    {
        const string html = """
            <div class="p-novel__body">
              <div class="js-novel-text p-novel__text"><p>本文1</p><p id="Lp2"><a href="//27570.mitemin.net/i1199316/" target="_blank"><img src="//27570.mitemin.net/userpageimage/viewimagebig/icode/i1199316/" alt="挿絵(By みてみん)" border="0" /></a></p></div>
            </div>
            """;
        var document = await AngleSharpHelper.ParseAsync(html);

        Assert.That(NarouEpisodeParser.ExtractContent(document), Is.EqualTo(string.Join("\n",
            EpisodeContentFormat.BodyStart, "本文1",
            EpisodeContentFormat.ImagePrefix + "https://27570.mitemin.net/userpageimage/viewimagebig/icode/i1199316/")));
    }

    // 画像の無い空段落は空行として残す(画像だけの段落の空テキストは落とす)。

    [Test]
    public async Task ExtractContentKeepsEmptyParagraph()
    {
        const string html = """
            <div class="p-novel__body">
              <div class="js-novel-text p-novel__text"><p>本文1</p><p></p><p>本文2</p></div>
            </div>
            """;
        var document = await AngleSharpHelper.ParseAsync(html);

        Assert.That(NarouEpisodeParser.ExtractContent(document), Is.EqualTo(string.Join("\n",
            EpisodeContentFormat.BodyStart, "本文1", "", "本文2")));
    }

    // 本文区画が無ければ、前書きだけを本文として返さず例外にする。

    [Test]
    public async Task ExtractContentThrowsWithoutBody()
    {
        const string html = """
            <div class="p-novel__body">
              <div class="js-novel-text p-novel__text p-novel__text--preface"><p>前書き1</p></div>
            </div>
            """;
        var document = await AngleSharpHelper.ParseAsync(html);

        Assert.Throws<InvalidOperationException>(() => NarouEpisodeParser.ExtractContent(document));
    }
}

public class EpisodeContentFormatTests
{
    [Test]
    public void ToHtmlWrapsPrefaceAndAfterword()
    {
        var content = string.Join("\n",
            EpisodeContentFormat.PrefaceStart, "前",
            EpisodeContentFormat.BodyStart, "本",
            EpisodeContentFormat.AfterwordStart, "後");

        Assert.That(EpisodeContentFormat.ToHtml(content), Is.EqualTo(
            "<div class=\"preface\"><p>前</p></div><p>本</p><div class=\"afterword\"><p>後</p></div>"));
    }

    [Test]
    public void ToHtmlRendersHttpImageOnly()
    {
        var content = string.Join("\n",
            EpisodeContentFormat.ImagePrefix + "https://example.com/a.jpg?x=1&y=2",
            EpisodeContentFormat.ImagePrefix + "javascript:alert(1)",
            "\u0001unknown");

        Assert.That(EpisodeContentFormat.ToHtml(content), Is.EqualTo(
            "<img src=\"https://example.com/a.jpg?x=1&amp;y=2\" alt=\"挿絵\">"));
    }

    [Test]
    public void ToHtmlTreatsUnmarkedContentAsBody()
    {
        Assert.That(EpisodeContentFormat.ToHtml("一\n\n<二>"), Is.EqualTo("<p>一</p><p></p><p>&lt;二&gt;</p>"));
    }
}
