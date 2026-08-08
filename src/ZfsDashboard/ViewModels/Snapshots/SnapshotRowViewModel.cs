using Zfs.Core.Models;
using Zfs.Core.Services;
using ZfsDashboard.Presentation;
using ZfsDashboard.ViewModels.Shared;

namespace ZfsDashboard.ViewModels.Snapshots;

public sealed record SnapshotRowViewModel
{
    private readonly Snapshot snapshot;
    private readonly string collapseId;
    private readonly bool includeSeconds;

    public SnapshotRowViewModel(Snapshot snapshot, string collapseId, bool includeSeconds)
    {
        this.snapshot = snapshot;
        this.collapseId = collapseId;
        this.includeSeconds = includeSeconds;
    }

    public string CollapseId => this.collapseId;
    public string Name => this.snapshot.SnapName;
    public string Used => this.snapshot.Used.FormatBytes();
    public string Referenced => this.snapshot.Refer.FormatBytes();
    public string Created =>
        this.snapshot.Creation.LocalDateTime.ToString(this.includeSeconds ? "yyyy-MM-dd HH:mm:ss" : "yyyy-MM-dd HH:mm");

    public CopyableCommandViewModel RollbackCommand =>
        new CopyableCommandViewModel(
            CommandSuggestionsService.SuggestRollback(this.snapshot.Name).ZfsCommand,
            Compact: true);

    public CopyableCommandViewModel DestroyCommand =>
        new CopyableCommandViewModel(
            CommandSuggestionsService.SuggestDestroySnapshot(this.snapshot.Name).ZfsCommand,
            Compact: true);
}
