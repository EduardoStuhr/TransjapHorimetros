namespace TransjapHorimetros.Domain.Entities;

public sealed class ReadingReview
{
    private ReadingReview() { }
    public ReadingReview(Guid readingId, bool approved, string reason, Guid actorId, string actorEmail, DateTimeOffset now)
    {
        ReadingId = readingId;
        Approved = approved;
        Reason = reason.Trim();
        ActorId = actorId;
        ActorEmail = actorEmail;
        CreatedAt = now;
    }
    public Guid ReadingId { get; private set; }
    public bool Approved { get; private set; }
    public string Reason { get; private set; } = "";
    public Guid ActorId { get; private set; }
    public string ActorEmail { get; private set; } = "";
    public DateTimeOffset CreatedAt { get; private set; }
}
