namespace TransjapHorimetros.Application.Exceptions;

public sealed class UniqueConstraintException(string? constraintName, Exception innerException)
    : Exception("Uma restrição de unicidade foi violada.", innerException)
{
    public string? ConstraintName { get; } = constraintName;
}
