using Datasets.Domain.Frames;

namespace Datasets.Application.Abstractions;

public interface IFrameRepository
{
    Task<Frame?> GetAsync(Guid id, CancellationToken ct);
    Task<int> CountNewAsync(Guid cameraId, CancellationToken ct);

    /// <summary>Самые старые кадры камеры, ещё не попавшие ни в один снимок (отслеживаемые — их будут менять).</summary>
    Task<IReadOnlyList<Frame>> TakeNewAsync(Guid cameraId, int max, CancellationToken ct);

    Task<IReadOnlyList<Frame>> ListBySnapshotAsync(Guid snapshotId, CancellationToken ct);
    Task<IReadOnlyList<Frame>> ListByCameraAsync(Guid cameraId, int skip, int take, CancellationToken ct);
    void Add(Frame frame);
}
