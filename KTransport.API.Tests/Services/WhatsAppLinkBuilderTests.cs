using KTransport.API.Services;
using Xunit;

namespace KTransport.API.Tests.Services;

/// <summary>
/// Unit tests for the free WhatsApp click-to-chat link builder. Phone
/// normalisation is the fiddly part (operators enter numbers every which way),
/// so it carries the bulk of the cases.
/// </summary>
public class WhatsAppLinkBuilderTests
{
    [Theory]
    [InlineData("9876543210", "919876543210")]          // bare 10-digit
    [InlineData("98765 43210", "919876543210")]          // spaces
    [InlineData("98765-43210", "919876543210")]          // dashes
    [InlineData("+91 98765 43210", "919876543210")]      // +91 prefix
    [InlineData("919876543210", "919876543210")]         // already country-coded
    [InlineData("09876543210", "919876543210")]          // trunk 0 prefix
    [InlineData("0091 9876543210", "919876543210")]      // 00 international prefix
    public void NormalizeMobile_HandlesCommonIndianFormats(string input, string expected)
    {
        Assert.Equal(expected, WhatsAppLinkBuilder.NormalizeMobile(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("12345")]        // too short
    [InlineData("abcd")]         // no digits
    public void NormalizeMobile_ReturnsNull_ForUnusable(string? input)
    {
        Assert.Null(WhatsAppLinkBuilder.NormalizeMobile(input));
    }

    [Fact]
    public void BuildShareUrl_WithValidMobile_TargetsThatContact_AndEncodesText()
    {
        var url = WhatsAppLinkBuilder.BuildShareUrl("9876543210", "Track here: https://x/t?lr=GR 1");

        Assert.StartsWith("https://wa.me/919876543210?text=", url);
        Assert.Contains("Track%20here", url);          // space encoded
        Assert.Contains("https%3A%2F%2F", url);        // URL inside the message encoded
        Assert.DoesNotContain(" ", url);
    }

    [Fact]
    public void BuildShareUrl_WithoutUsableMobile_FallsBackToContactPicker()
    {
        var url = WhatsAppLinkBuilder.BuildShareUrl(null, "Hello");
        Assert.Equal("https://wa.me/?text=Hello", url);
    }

    [Fact]
    public void NormalizeMobile_RespectsCustomCountryCode()
    {
        Assert.Equal("9715551234567", WhatsAppLinkBuilder.NormalizeMobile("5551234567", "971"));
    }
}
