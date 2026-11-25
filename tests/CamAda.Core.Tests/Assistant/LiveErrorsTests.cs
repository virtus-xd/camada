using CamAda.Core.Assistant;

namespace CamAda.Core.Tests.Assistant;

public class LiveErrorsTests
{
    [Theory]
    [InlineData("You exceeded your current quota, please check your plan and billing details.", AssistantError.QuotaExceeded)]
    [InlineData("API key not valid. Please pass a valid API key.", AssistantError.InvalidKey)]
    [InlineData("Payment Required", AssistantError.PaymentRequired)]
    [InlineData("Your prepaid balance is depleted; check billing.", AssistantError.PaymentRequired)]
    [InlineData("Thinking budget is not supported for this model.", AssistantError.ModelUnavailable)]
    [InlineData("models/gemini-x is not found for API version v1beta", AssistantError.ModelUnavailable)]
    [InlineData(null, AssistantError.Network)]
    [InlineData("", AssistantError.Network)]
    [InlineData("Something else happened", AssistantError.Unknown)]
    public void From_close(string? description, AssistantError expected) =>
        Assert.Equal(expected, LiveErrors.FromClose(description));

    [Theory]
    [InlineData("Internal error encountered.", 1011, AssistantError.Network)]   // sunucu hatası oturum sürerken: bağlantı koptu
    [InlineData("Service is currently unavailable.", 1013, AssistantError.Network)]
    [InlineData("Going away", 1001, AssistantError.Network)]
    [InlineData("anything", 1006, AssistantError.Network)]
    [InlineData("You exceeded your current quota, please check billing.", 1011, AssistantError.QuotaExceeded)] // metin önce
    [InlineData("API key not valid.", 1007, AssistantError.InvalidKey)]
    [InlineData("Something else happened", 1008, AssistantError.Unknown)]
    [InlineData("Something else happened", null, AssistantError.Unknown)]
    public void From_close_with_status(string description, int? status, AssistantError expected) =>
        Assert.Equal(expected, LiveErrors.FromClose(description, status));

    [Theory]
    [InlineData(400, """{"error":{"message":"API key not valid."}}""", AssistantError.InvalidKey)]
    [InlineData(400, """{"error":{"message":"bad request"}}""", AssistantError.Unknown)]
    [InlineData(403, null, AssistantError.InvalidKey)]
    [InlineData(402, null, AssistantError.PaymentRequired)]
    [InlineData(429, null, AssistantError.QuotaExceeded)]
    [InlineData(404, null, AssistantError.ModelUnavailable)]
    [InlineData(503, null, AssistantError.Network)]
    public void From_http(int status, string? body, AssistantError expected) =>
        Assert.Equal(expected, LiveErrors.FromHttp(status, body));
}
