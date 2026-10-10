namespace Datasets.Domain.Snapshots;

public sealed class SnapshotFrame
{
    private SnapshotFrame() { }

    internal SnapshotFrame(Guid frameId, string storagePath, DataSplit split)
    {
        FrameId = frameId;
        StoragePath = storagePath;
        Split = split;
    }

    public Guid FrameId { get; private set; }
    public string StoragePath { get; private set; } = string.Empty;
    public DataSplit Split { get; private set; }
}
