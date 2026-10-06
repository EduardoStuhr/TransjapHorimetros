namespace TransjapHorimetros.Application.Exceptions;

public sealed class EntityNotFoundException(string entity, object key)
    : Exception($"{entity} não encontrado para o identificador '{key}'.")
{
    public string Entity { get; } = entity;

    public object Key { get; } = key;
}
