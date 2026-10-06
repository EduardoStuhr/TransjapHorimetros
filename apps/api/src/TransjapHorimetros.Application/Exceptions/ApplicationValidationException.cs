namespace TransjapHorimetros.Application.Exceptions;

public sealed class ApplicationValidationException : Exception
{
    public ApplicationValidationException(string field, string message)
        : base(message)
    {
        Errors = new Dictionary<string, string[]> { [field] = [message] };
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}
