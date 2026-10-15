using System.Text.RegularExpressions;
using TransjapHorimetros.Infrastructure.Persistence.Migrations;
using TransjapHorimetros.Infrastructure.Persistence.Seed;

namespace TransjapHorimetros.IntegrationTests;

public sealed class PhysicalQrDataPatchDefinitionTests
{
    [Fact]
    public void Mapping_ContainsExactlyCurrentFleetsExcept296WithFourDigitTokens()
    {
        var mappings = Regex.Matches(
                ExistingMachineQrCodeDataPatch.Sql,
                @"\((?<fleet>\d+), N'(?<token>\d{4})'\)")
            .Select(match => new
            {
                FleetNumber = int.Parse(match.Groups["fleet"].Value),
                Token = match.Groups["token"].Value,
            })
            .ToArray();
        var expectedFleets = MachineSeedData.All
            .Select(machine => machine.FleetNumber)
            .Where(fleetNumber => fleetNumber != 296)
            .Order()
            .ToArray();

        Assert.Equal(ExistingMachineQrCodeDataPatch.ExpectedQrCodeCount, mappings.Length);
        Assert.Equal(51, mappings.Length);
        Assert.Equal(51, ExistingMachineQrCodeDataPatch.Mappings.Count);
        Assert.Equal(expectedFleets, mappings.Select(mapping => mapping.FleetNumber).Order());
        Assert.Equal(expectedFleets, ExistingMachineQrCodeDataPatch.Mappings.Select(m => m.FleetNumber).Order());
        Assert.All(
            mappings,
            mapping => Assert.Equal(mapping.FleetNumber.ToString("D4"), mapping.Token));
        Assert.All(
            ExistingMachineQrCodeDataPatch.Mappings,
            m => Assert.Equal(m.FleetNumber.ToString("D4"), m.Token));
        Assert.All(
            ExistingMachineQrCodeDataPatch.Mappings,
            m => Assert.Matches(@"^\d{4}$", m.Token));
        Assert.Equal(51, ExistingMachineQrCodeDataPatch.Mappings.Select(m => m.Token).Distinct().Count());
        Assert.Equal(51, ExistingMachineQrCodeDataPatch.Mappings.Select(m => m.FleetNumber).Distinct().Count());
        Assert.DoesNotContain(mappings, mapping => mapping.FleetNumber == 296);
        Assert.DoesNotContain(ExistingMachineQrCodeDataPatch.Mappings, m => m.FleetNumber == 296);
        Assert.All(
            ExistingMachineQrCodeDataPatch.Mappings,
            m => Assert.Contains($"({m.FleetNumber}, N'{m.Token}')", ExistingMachineQrCodeDataPatch.Sql));
    }

    [Theory]
    [InlineData(16)]
    [InlineData(76)]
    [InlineData(80)]
    [InlineData(86)]
    [InlineData(92)]
    [InlineData(94)]
    [InlineData(108)]
    [InlineData(114)]
    [InlineData(166)]
    [InlineData(184)]
    [InlineData(186)]
    [InlineData(188)]
    [InlineData(190)]
    [InlineData(196)]
    [InlineData(202)]
    [InlineData(206)]
    [InlineData(220)]
    [InlineData(222)]
    [InlineData(224)]
    [InlineData(226)]
    [InlineData(234)]
    [InlineData(252)]
    [InlineData(272)]
    [InlineData(276)]
    [InlineData(286)]
    [InlineData(288)]
    public void Mapping_DoesNotContainOldPdfFleet(int oldFleetNumber)
    {
        Assert.DoesNotContain(
            ExistingMachineQrCodeDataPatch.Mappings,
            m => m.FleetNumber == oldFleetNumber);
        Assert.DoesNotContain(
            $"({oldFleetNumber}, N'{oldFleetNumber:D4}')",
            ExistingMachineQrCodeDataPatch.Sql,
            StringComparison.Ordinal);
    }
}
