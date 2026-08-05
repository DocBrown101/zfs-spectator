using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Zfs.Core.Models;
using Zfs.Core.Services;
using ZfsDashboard.Models;
using ZfsDashboard.Presentation;

namespace ZfsDashboard.Pages.Pools;

public class DetailsModel(IZpoolService zpool) : PageModel
{
    public Pool? Pool { get; private set; }
    public ScrubStatusViewModel Scrub { get; private set; } = ScrubStatusViewModel.Loading;
    public List<CommandSuggestion> Suggestions { get; } = [];
    public IReadOnlyList<PoolDeviceGroupViewModel> DeviceGroups { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(string name)
    {
        var result = await zpool.GetPoolWithScrubAsync(name);
        if (result is null) return this.NotFound();

        this.Pool = result.Value.Pool;
        this.Scrub = ScrubPresentationMapper.Map(result.Value.Scrub);
        this.DeviceGroups = BuildDeviceGroups(this.Pool);

        this.Suggestions.Add(CommandSuggestionsService.SuggestCreateChildDataset(name));
        this.Suggestions.Add(CommandSuggestionsService.SuggestScrub(name));
        this.Suggestions.Add(CommandSuggestionsService.SuggestSetSpecialSmallBlocks(name));
        this.Suggestions.Add(CommandSuggestionsService.SuggestPoolExport(name));
        if (this.Pool.Compression == "off")
            this.Suggestions.Add(CommandSuggestionsService.SuggestSetProperty(name, "compression", "lz4"));
        if (this.Pool.Atime == "on")
            this.Suggestions.Add(CommandSuggestionsService.SuggestSetProperty(name, "atime", "off"));

        return this.Page();
    }

    private static IReadOnlyList<PoolDeviceGroupViewModel> BuildDeviceGroups(Pool pool)
    {
        return new PoolDeviceGroupViewModel[]
        {
            new() { Title = "Data Devices", Icon = "hdd-rack", Devices = pool.DataDevices, Size = pool.Size, Allocated = pool.Alloc, Free = pool.Free },
            new() { Title = "Special VDEV (Metadata/Small Blocks)", Icon = "diamond", Devices = pool.SpecialDevices, Size = pool.SpecialSize, Allocated = pool.SpecialAlloc, Free = pool.SpecialFree },
            new() { Title = "L2ARC Cache", Icon = "lightning-charge", Devices = pool.CacheDevices },
            new() { Title = "ZFS Intent Log (SLOG)", Icon = "journal-text", Devices = pool.LogDevices },
            new() { Title = "Hot Spares", Icon = "shield-check", Devices = pool.SpareDevices },
        }.Where(group => group.Devices.Count > 0).ToList();
    }
}
