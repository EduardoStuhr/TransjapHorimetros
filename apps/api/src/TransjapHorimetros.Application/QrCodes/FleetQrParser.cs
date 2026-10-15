using System.Globalization;
using System.Text.RegularExpressions;

namespace TransjapHorimetros.Application.QrCodes;

public static partial class FleetQrParser
{
    public static bool TryParse(string? value, out int fleetNumber)
    {
        fleetNumber = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim();
        return TryParsePhysicalFleetQr(normalized, out fleetNumber)
            || TryParseLegacyFleetText(normalized, out fleetNumber);
    }

    public static bool TryParsePhysicalFleetQr(string value, out int fleetNumber)
    {
        fleetNumber = default;
        if (value.Length != 4 || value.Any(character => character is < '0' or > '9'))
        {
            return false;
        }

        return int.TryParse(
                value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out fleetNumber)
            && fleetNumber > 0;
    }

    public static bool TryParseLegacyFleetText(string value, out int fleetNumber)
    {
        fleetNumber = default;
        var numberText = IsPositiveNumberWithoutLeadingZero(value)
            ? value
            : LegacyFleetTextPattern().Match(value) is { Success: true } match
                ? match.Groups["number"].Value
                : null;

        return numberText is not null
            && int.TryParse(
                numberText,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out fleetNumber)
            && fleetNumber > 0;
    }

    private static bool IsPositiveNumberWithoutLeadingZero(string value) =>
        value.Length > 0
        && value[0] is >= '1' and <= '9'
        && value.All(character => character is >= '0' and <= '9');

    [GeneratedRegex(
        @"\AFROTA(?:[ \t]+|[ \t]*:[ \t]*)(?<number>[1-9][0-9]*)\z",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LegacyFleetTextPattern();
}
