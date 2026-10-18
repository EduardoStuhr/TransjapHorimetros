using System.Globalization;
using System.Text.RegularExpressions;
using TransjapHorimetros.Domain.Enums;

namespace TransjapHorimetros.Application.Ocr;

public sealed class MeterReadingExtractor : IMeterReadingExtractor
{
    private static readonly Regex Numbers = new(@"(?<![\p{L}\d.,:/-])\d{1,9}(?:[.,]\d{1,2})?(?![\p{L}\d.,:/-])", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private const string Hours = @"(?:HOURS?|HOR[ÍI]METRO|H|HR)";
    private const string Km = @"(?:ODO(?:METER|METRO)?|OD[ÔO]METRO|KMS?)";

    public MeterExtraction Extract(OcrProviderResult result, MeterUnit? unit)
    {
        var text = result.RawText[..Math.Min(4000, result.RawText.Length)].ToUpperInvariant();
        var candidates = new List<(decimal Value, string Token, bool Label)>();
        foreach (var line in text.Split('\n'))
        {
            foreach (Match number in Numbers.Matches(line))
            {
                var before = line[..number.Index];
                var after = line[(number.Index + number.Length)..];
                if (Regex.IsMatch(before, @"(?:SERIAL|S/N|SERIE|SÉRIE|SN|MODEL|MODELO)\s*[:#=]?\s*$")) continue;
                bool Near(string label) => Regex.IsMatch(before, @"\b" + label + @"\s*[:=]?\s*$")
                    || Regex.IsMatch(after, @"^\s*" + label + @"\b");
                var hours = Near(Hours); var km = Near(Km);
                if ((unit == MeterUnit.Hours && km) || (unit == MeterUnit.Km && hours)) continue;
                var value = decimal.Parse(number.Value.Replace(',', '.'), CultureInfo.InvariantCulture);
                candidates.Add((value, number.Value, hours || km));
            }
        }
        var labelled = candidates.Where(c => c.Label).ToArray();
        var selected = labelled.Length > 0 ? labelled : candidates.ToArray();
        // Bare readings are accepted only when the entire text is a single numeric display.
        if (labelled.Length == 0 && (selected.Length != 1 || text.Trim() != selected[0].Token))
            return new(null, null);
        if (selected.Length == 0 || selected.Select(c => c.Value).Distinct().Count() != 1)
            return new(null, null);
        var candidate = selected[0];
        var words = result.Words.Where(w => w.Text.Trim() == candidate.Token).ToArray();
        // Confidence comes only from the numeric token, never from unrelated labels or an invented score.
        decimal? confidence = words.Length > 0 && words.All(w => w.Confidence is >= 0 and <= 1)
            ? words.Min(w => w.Confidence) : null;
        return new(candidate.Value, confidence);
    }
}
