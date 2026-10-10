namespace Quorum.Application.Abstractions;

/// <summary>Фиксация изменений. Реализуется DbContext'ом сервиса.</summary>
public interface IUnitOfWork
{
    Task CommitAsync(CancellationToken ct = default);
}
