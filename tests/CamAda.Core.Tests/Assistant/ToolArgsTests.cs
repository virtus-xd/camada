using System.Text.Json.Nodes;
using CamAda.Core.Assistant;

namespace CamAda.Core.Tests.Assistant;

public class ToolArgsTests
{
    private static JsonObject Args(string json) => JsonNode.Parse(json)!.AsObject();

    [Theory]
    [InlineData("""{"yuzde":40}""", 40)]
    [InlineData("""{"yuzde":"40"}""", 40)]
    [InlineData("""{"yuzde":40.0}""", 40)]
    [InlineData("""{"yuzde":140}""", 100)]
    [InlineData("""{"yuzde":-5}""", 0)]
    public void Int_accepts_number_string_and_decimal(string json, int expected)
    {
        var result = ToolArgs.Int(Args(json), "yuzde", 0, 100);
        Assert.Null(result.Error);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public void Missing_or_wrong_type_is_error()
    {
        Assert.Equal("eksik bilgi: yuzde", ToolArgs.Int(Args("{}"), "yuzde", 0, 100).Error);
        Assert.Equal("geçersiz değer: yuzde", ToolArgs.Int(Args("""{"yuzde":"çok"}"""), "yuzde", 0, 100).Error);
        Assert.Equal("eksik bilgi: ad", ToolArgs.Text(Args("""{"ad":"  "}"""), "ad").Error);
    }

    [Theory]
    [InlineData("""{"acik":true}""", true)]
    [InlineData("""{"acik":"true"}""", true)]
    [InlineData("""{"acik":"evet"}""", true)]
    [InlineData("""{"acik":false}""", false)]
    [InlineData("""{"acik":"hayır"}""", false)]
    public void Bool_accepts_true_false_strings(string json, bool expected) =>
        Assert.Equal(expected, ToolArgs.Bool(Args(json), "acik").Value);

    [Fact]
    public void Text_is_trimmed() => Assert.Equal("Discord", ToolArgs.Text(Args("""{"ad":" Discord "}"""), "ad").Value);

    [Theory]
    [InlineData("https://www.youtube.com", "https://www.youtube.com")]
    [InlineData("youtube.com", "https://youtube.com")]
    [InlineData("YouTube", "https://youtube.com")]
    [InlineData("YouTube.com/watch?v=dQw4w9WgXcQ", "https://youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("http://a.b/c?d=1", "http://a.b/c?d=1")]
    [InlineData("file:///C:/x", null)]
    [InlineData("javascript:alert(1)", null)]
    [InlineData("", null)]
    [InlineData("a b", null)]
    public void Url_normalization(string input, string? expected) => Assert.Equal(expected, ToolArgs.NormalizeUrl(input));

    [Fact]
    public void Google_search_url() =>
        Assert.Equal("https://www.google.com/search?q=Ankara%20hava", ToolArgs.GoogleSearchUrl("Ankara hava"));
}
