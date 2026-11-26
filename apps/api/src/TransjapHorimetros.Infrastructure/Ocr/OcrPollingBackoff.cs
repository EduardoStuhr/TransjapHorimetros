using Microsoft.Data.SqlClient;
using TransjapHorimetros.Application.Ocr;

namespace TransjapHorimetros.Infrastructure.Ocr;

public sealed class OcrPollingBackoff(OcrOptions options)
{
    public TimeSpan Delay { get; private set; } = TimeSpan.FromSeconds(options.PollIntervalSeconds);

    public void RecordSuccess(bool hadWork)
    {
        Delay = hadWork ? TimeSpan.FromSeconds(options.PollIntervalSeconds)
            : TimeSpan.FromSeconds(Math.Min(options.MaximumIdlePollIntervalSeconds, Delay.TotalSeconds * 2));
    }

    public void RecordFailure(int? sqlError)
    {
        Delay = TimeSpan.FromSeconds(sqlError == 42119
            ? options.QuotaRetryIntervalSeconds
            : Math.Min(options.MaximumFailurePollIntervalSeconds,
                Math.Max(options.PollIntervalSeconds, Delay.TotalSeconds) * 2));
    }

    public static int? SqlError(Exception error)
    {
        for (Exception? current = error; current is not null; current = current.InnerException)
            if (current is SqlException sql) return sql.Number;
        return null;
    }
}
