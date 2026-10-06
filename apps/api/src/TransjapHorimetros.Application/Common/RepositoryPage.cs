namespace TransjapHorimetros.Application.Common;

public sealed record RepositoryPage<T>(IReadOnlyList<T> Items, int TotalItems);
