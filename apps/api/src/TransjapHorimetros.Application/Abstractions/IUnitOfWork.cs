namespace TransjapHorimetros.Application.Abstractions;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    void ClearChanges();

    Task<T> ExecuteForMachineAsync<T>(Guid machineId, Func<Task<T>> operation, CancellationToken cancellationToken)
        => operation();
}
