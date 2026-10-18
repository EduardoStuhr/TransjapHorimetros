using TransjapHorimetros.Domain.Enums;

namespace TransjapHorimetros.Domain.Rules;

public static class ReadingStatusTransitionPolicy
{
    public static bool CanTransition(ReadingStatus from, ReadingStatus to) => (from, to) switch
    {
        (ReadingStatus.Pending, ReadingStatus.Validated or ReadingStatus.Suspect or ReadingStatus.Rejected) => true,
        (ReadingStatus.Suspect, ReadingStatus.Validated or ReadingStatus.Rejected) => true,
        (ReadingStatus.Validated, ReadingStatus.Corrected) => true,
        _ => false,
    };

    public static void EnsureTransition(ReadingStatus from, ReadingStatus to)
    {
        if (!CanTransition(from, to)) throw new InvalidOperationException($"Transição {from} → {to} não permitida.");
    }

    public static void EnsureInitial(ReadingStatus status, ReadingType type)
    {
        var valid = type == ReadingType.Correction ? status == ReadingStatus.Pending
            : status is ReadingStatus.Validated or ReadingStatus.Suspect;
        if (!valid) throw new ArgumentException("Status inicial incompatível com o tipo da leitura.");
    }
}
