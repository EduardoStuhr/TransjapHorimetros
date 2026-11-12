using System.Globalization;
using System.Text.RegularExpressions;
using TransjapHorimetros.Domain.Enums;

namespace TransjapHorimetros.Application.Ocr;

public sealed class MeterReadingExtractor : IMeterReadingExtractor
{
    public const string Version = "meter-normalization-v2";
    private static readonly Regex Numbers = new(@"(?<![\p{L}\d.,:/+\-])\d+(?:[.,]\d+)*(?![\d.,:/\-])",
        RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private const string Hours = @"(?:HOURS?|HOR[ÍI]METRO|HRS?|H)";
    private const string Km = @"(?:ODO(?:METER|METRO)?|OD[ÔO]METRO|KMS?)";

    public MeterExtraction Extract(OcrProviderResult result, MeterUnit? unit)
    {
        var text = OcrText.Limit(result.RawText).ToUpperInvariant();
        var candidates = new List<OcrCandidate>();
        var ambiguousFormat = false;
        var wrongUnit = false;
        foreach (var line in text.Split('\n'))
        {
            foreach (Match number in Numbers.Matches(line))
            {
                var before = line[..number.Index];
                var after = line[(number.Index + number.Length)..];
                bool Near(string label) => Regex.IsMatch(before, @"\b" + label + @"\s*[:=]?\s*$")
                    || Regex.IsMatch(after, @"^\s*" + label + @"\b");
                if (Regex.IsMatch(before, @"(?:SERIAL|S/N|S[ÉE]RIE|SN|MODEL[O]?|RPM|FUEL|TEMP|VOLTS?)\s*[:#=]?\s*$")
                    || Regex.IsMatch(after, @"^\s*(?:%|RPM|V\b|°)")) continue;
                var hours = Near(Hours);
                var km = Near(Km);
                if (after.Length > 0 && char.IsLetter(after[0])
                    && !Regex.IsMatch(after, "^(?:" + Hours + "|" + Km + @")\b")) continue;
                if ((unit == MeterUnit.Hours && km) || (unit == MeterUnit.Km && hours))
                {
                    wrongUnit = true;
                    continue;
                }
                if (Regex.IsMatch(before, @"[\d.,]\s+$")
                    && (hours || km || candidates.LastOrDefault()?.HasUnit == true)) ambiguousFormat = true;
                var normalized = Normalize(number.Value);
                if (normalized.Ambiguous && (hours || km || text.Trim() == number.Value)) ambiguousFormat = true;
                foreach (var value in normalized.Values)
                    candidates.Add(new(number.Value, value, hours || km));
                if (candidates.Count > 20)
                    return new(null, null, "TOO_MANY_CANDIDATES", candidates.Take(20).ToArray());
            }
        }
        var labelled = candidates.Where(c => c.HasUnit).ToArray();
        var selected = labelled.Length > 0 ? labelled : candidates.ToArray();
        if (ambiguousFormat) return new(null, null, "AMBIGUOUS_FORMAT", candidates.ToArray());
        if (selected.Select(c => c.Value).Distinct().Count() > 1)
            return new(null, null, "AMBIGUOUS_VALUES", selected);
        if (selected.Length == 0) return new(null, null, wrongUnit ? "UNIT_MISMATCH" : "NO_VALUE", []);
        if (labelled.Length == 0 && (selected.Length != 1 || text.Trim() != selected[0].Token))
            return new(null, null, "UNLABELLED_CONTEXT", selected);
        var token = selected[0];
        var words = result.Words.Where(w => w.Text.Trim() == token.Token).ToArray();
        decimal? confidence = words.Length > 0 && words.All(w => w.Confidence is >= 0 and <= 1)
            ? words.Min(w => w.Confidence) : null;
        return new(token.Value, confidence, confidence.HasValue ? "SELECTED" : "CONFIDENCE_MISSING", selected);
    }

    private static (decimal[] Values, bool Ambiguous) Normalize(string token)
    {
        if (token.Length > 32) return ([], false);
        var comma = token.LastIndexOf(',');
        var dot = token.LastIndexOf('.');
        var last = Math.Max(comma, dot);
        var normalized = token;
        if (comma >= 0 && dot >= 0)
        {
            var fraction = token[(last + 1)..];
            var integer = token[..last];
            var grouping = comma > dot ? '.' : ',';
            if (fraction.Length is < 1 or > 2 || !ValidGrouping(integer, grouping)) return ([], false);
            normalized = integer.Replace(grouping.ToString(), "") + "." + fraction;
        }
        else if (last >= 0)
        {
            var separator = token[last];
            var count = token.Count(c => c == separator);
            if (count > 1)
            {
                if (!ValidGrouping(token, separator)) return ([], false);
                normalized = token.Replace(separator.ToString(), "");
            }
            else if (token.Length - last - 1 == 3)
            {
                // A single separator followed by three digits has two conventions.
                var possibilities = new List<decimal>();
                if (ValidGrouping(token, separator) && Parse(token.Replace(separator.ToString(), ""), out var grouped))
                    possibilities.Add(grouped);
                if (Parse(token.Replace(',', '.'), out var fractional)) possibilities.Add(fractional);
                return (possibilities.Distinct().ToArray(), true);
            }
            else
            {
                if (token.Length - last - 1 is < 1 or > 2) return ([], false);
                normalized = token.Replace(',', '.');
            }
        }
        return Parse(normalized, out var value) ? ([value], false) : ([], false);
    }

    private static bool ValidGrouping(string text, char separator)
    {
        var parts = text.Split(separator);
        return parts.Length > 1 && parts[0].Length is >= 1 and <= 3
            && parts.Skip(1).All(part => part.Length == 3 && part.All(char.IsAsciiDigit));
    }

    private static bool Parse(string text, out decimal value) =>
        decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value)
        && value is >= 0 and <= 9_999_999_999.99m && decimal.Round(value, 2) == value;
}
