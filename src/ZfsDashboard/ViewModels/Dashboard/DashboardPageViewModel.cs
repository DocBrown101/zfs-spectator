using Zfs.Core.Models;

namespace ZfsDashboard.ViewModels.Dashboard;

public sealed record DashboardPageViewModel
{
    public DashboardPageViewModel(
        IReadOnlyList<Pool> pools,
        SystemInfo system,
        StaticSystemInfo staticSystem)
    {
        this.Uptime = system.Uptime;
        this.Cpu = new CpuCardViewModel(system, staticSystem);
        this.Memory = new MemoryCardViewModel(system.Memory);
        this.Arc = new ArcCardViewModel(system.Arc);
        this.Pools = pools.Select(pool => new PoolCardViewModel(pool)).ToList();
    }

    public string Uptime { get; }
    public CpuCardViewModel Cpu { get; }
    public MemoryCardViewModel Memory { get; }
    public ArcCardViewModel Arc { get; }
    public IReadOnlyList<PoolCardViewModel> Pools { get; }
}
