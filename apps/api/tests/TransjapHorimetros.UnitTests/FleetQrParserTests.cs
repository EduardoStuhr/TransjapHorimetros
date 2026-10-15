using TransjapHorimetros.Application.QrCodes;

namespace TransjapHorimetros.UnitTests;

public sealed class FleetQrParserTests
{
    [Theory]
    [InlineData("0068", 68)]
    [InlineData("0070", 70)]
    [InlineData("0112", 112)]
    [InlineData("0294", 294)]
    public void TryParsePhysicalFleetQr_ValidPhysicalCode_ReturnsFleet(
        string value,
        int expectedFleetNumber)
    {
        var parsed = FleetQrParser.TryParsePhysicalFleetQr(value, out var fleetNumber);

        Assert.True(parsed);
        Assert.Equal(expectedFleetNumber, fleetNumber);
    }

    [Theory]
    [InlineData("0000")]
    [InlineData("68A0")]
    [InlineData("00680")]
    [InlineData("068")]
    public void TryParsePhysicalFleetQr_InvalidPhysicalCode_IsRejected(string value)
    {
        Assert.False(FleetQrParser.TryParsePhysicalFleetQr(value, out _));
    }

    [Theory]
    [InlineData("68", 68)]
    [InlineData("112", 112)]
    [InlineData("294", 294)]
    [InlineData("FROTA 68", 68)]
    [InlineData("frota 68", 68)]
    [InlineData("FROTA: 68", 68)]
    public void TryParseLegacyFleetText_ValidLegacyValue_ReturnsFleet(
        string value,
        int expectedFleetNumber)
    {
        var parsed = FleetQrParser.TryParseLegacyFleetText(value, out var fleetNumber);

        Assert.True(parsed);
        Assert.Equal(expectedFleetNumber, fleetNumber);
    }

    [Theory]
    [InlineData("0068", 68)]
    [InlineData("0070", 70)]
    [InlineData("0112", 112)]
    [InlineData("0294", 294)]
    [InlineData("68", 68)]
    [InlineData("FROTA 68", 68)]
    [InlineData("frota 68", 68)]
    [InlineData("FROTA: 68", 68)]
    [InlineData(" 0068 ", 68)]
    [InlineData(" FROTA: 68 ", 68)]
    public void TryParse_ValidInputs_ReturnsExpectedFleet(string value, int expectedFleetNumber)
    {
        var parsed = FleetQrParser.TryParse(value, out var fleetNumber);

        Assert.True(parsed);
        Assert.Equal(expectedFleetNumber, fleetNumber);
    }

    [Theory]
    [InlineData("0000")]
    [InlineData("00680")]
    [InlineData("68A0")]
    [InlineData("068")]
    [InlineData("equipamento 0068")]
    [InlineData("máquina 0068 obra 123")]
    [InlineData("qr da máquina 0068")]
    [InlineData("http://site/0068")]
    [InlineData("https://example.com/0068")]
    [InlineData("https://example.com/machines/0068")]
    [InlineData("FROTA0068")]
    [InlineData("FROTA 0068")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void TryParse_InvalidCases_AreRejected(string? value)
    {
        Assert.False(FleetQrParser.TryParse(value, out _));
    }
}
