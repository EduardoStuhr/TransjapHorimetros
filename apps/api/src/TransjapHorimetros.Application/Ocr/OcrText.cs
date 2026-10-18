namespace TransjapHorimetros.Application.Ocr;

public static class OcrText
{
    public static string Limit(string text)
    {
        if (text.Length <= 4000) return text;
        // Never turn the cut-off prefix of a longer numeric token into a plausible meter reading.
        var end = 4000;
        while (end > 0 && !char.IsWhiteSpace(text[end - 1])) end--;
        return text[..end].TrimEnd();
    }
}
