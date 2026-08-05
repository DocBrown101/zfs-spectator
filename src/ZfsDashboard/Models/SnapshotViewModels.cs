namespace ZfsDashboard.Models;

public sealed record SnapshotTableViewModel
{
    public required IReadOnlyList<SnapshotRowViewModel> Rows { get; init; }
    public string EmptyMessage { get; init; } = "No snapshots";
}

public sealed record SnapshotRowViewModel
{
    public required string CollapseId { get; init; }
    public required string Name { get; init; }
    public required string Used { get; init; }
    public required string Referenced { get; init; }
    public required string Created { get; init; }
    public required CopyableCommandViewModel RollbackCommand { get; init; }
    public required CopyableCommandViewModel DestroyCommand { get; init; }
}

public sealed record SnapshotGroupViewModel
{
    public required string DatasetName { get; init; }
    public required SnapshotTableViewModel Table { get; init; }
}
