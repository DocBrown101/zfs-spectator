namespace Zfs.Tests;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Zfs.Core;
using Zfs.Core.Services;
using Zfs.Core.Services.TestData;
using ZfsDashboard.Pages.Datasets;

public class DatasetDetailsTests
{
    [Theory]
    [InlineData("zfsPool/Cloud", "backup-2026-03-22")]
    [InlineData("zfsPool")]
    [InlineData("zfsPool/Media/Filme")]
    public async Task Details_ReadOnlyTheRequestedPool(string name, params string[] expectedSnapshots)
    {
        var executor = new RecordingDemoExecutor();
        var page = new DetailsModel(new ZfsService(executor), new ZpoolService(executor));

        Assert.IsType<PageResult>(await page.OnGetAsync(name));
        Assert.Equal(name, page.Dataset?.Name);
        Assert.Equal(expectedSnapshots, page.SnapshotGroup.Rows.Select(row => row.Name));
        Assert.Equal(2, executor.ZfsQueries.Count);
        Assert.All(executor.ZfsQueries, args => Assert.EndsWith(" zfsPool", args));
    }

    [Theory]
    [InlineData("unknown/Media", 0)]
    [InlineData("zfsPool/missing", 1)]
    public async Task Details_Return404WithoutReadingUnrelatedData(string name, int expectedQueries)
    {
        var executor = new RecordingDemoExecutor();
        var page = new DetailsModel(new ZfsService(executor), new ZpoolService(executor));

        Assert.IsType<NotFoundResult>(await page.OnGetAsync(name));
        Assert.Equal(expectedQueries, executor.ZfsQueries.Count);
    }

    private sealed class RecordingDemoExecutor : ICommandExecutor
    {
        private readonly EmbeddedJsonCommandExecutor demo = new();
        internal List<string> ZfsQueries { get; } = [];

        public Task<string> ExecuteAsync(string command, string arguments, CancellationToken cancellationToken = default)
        {
            if (command == "zfs") this.ZfsQueries.Add(arguments);
            return this.demo.ExecuteAsync(command, arguments, cancellationToken);
        }
    }
}
