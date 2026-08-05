using Zfs.Core.Models;
using Zfs.Core.Services;
using ZfsDashboard.Presentation;
using ZfsDashboard.ViewModels.Shared;

namespace ZfsDashboard.ViewModels.Snapshots;

public sealed record SnapshotRowViewModel
{
    public SnapshotRowViewModel(Snapshot snapshot, string collapseId, bool includeSeconds)
    {
        this.CollapseId = collapseId;
        this.Name = snapshot.SnapName;
        this.Used = snapshot.Used.FormatBytes();
        this.Referenced = snapshot.Refer.FormatBytes();
        this.Created = snapshot.Creation.LocalDateTime.ToString(includeSeconds ? "yyyy-MM-dd HH:mm:ss" : "yyyy-MM-dd HH:mm");
        this.RollbackCommand = new CopyableCommandViewModel(
            CommandSuggestionsService.SuggestRollback(snapshot.Name).ZfsCommand,
            Compact: true);
        this.DestroyCommand = new CopyableCommandViewModel(
            CommandSuggestionsService.SuggestDestroySnapshot(snapshot.Name).ZfsCommand,
            Compact: true);
    }

    public string CollapseId { get; }
    public string Name { get; }
    public string Used { get; }
    public string Referenced { get; }
    public string Created { get; }
    public CopyableCommandViewModel RollbackCommand { get; }
    public CopyableCommandViewModel DestroyCommand { get; }
}
