namespace TransjapHorimetros.Application.Abstractions;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    void ClearChanges();
}
