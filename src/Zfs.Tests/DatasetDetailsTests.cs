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
        Assert.DoesNotContain(page.Suggestions, s => s.ZfsCommand.Contains("keylocation="));
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

    [Theory]
    [InlineData("unavailable", "file:///etc/zfs/keys/cloud.key", "raw", "File", "/etc/zfs/keys/cloud.key")]
    [InlineData("available", "file:///etc/zfs/keys/cloud.key", "hex", "File", "/etc/zfs/keys/cloud.key")]
    [InlineData("unavailable", "prompt", "passphrase", "Prompt", null)]
    [InlineData("unavailable", "https://user:secret@example.org/key?token=secret", "raw", "HTTPS", null)]
    public async Task Details_ShowKeySettingsForInheritedEncryption(string status, string location, string format,
        string expectedSource, string? expectedPath)
    {
        var executor = new RecordingDemoExecutor($$"""
            {"datasets":{
              "zfsPool":{"name":"zfsPool","properties":{
                "encryption":{"value":"aes-256-gcm"},"encryptionroot":{"value":"zfsPool"},
                "keylocation":{"value":"{{location}}"},"keyformat":{"value":"{{format}}"}
              } },
              "zfsPool/Cloud":{"name":"zfsPool/Cloud","properties":{
                "encryption":{"value":"aes-256-gcm"},"encryptionroot":{"value":"zfsPool"},
                "keystatus":{"value":"{{status}}"},"keylocation":{"value":"none"}
              } }
            } }
            """);
        var page = new DetailsModel(new ZfsService(executor), new ZpoolService(executor));

        Assert.IsType<PageResult>(await page.OnGetAsync("zfsPool/Cloud"));
        var query = Assert.Single(executor.ZfsQueries, args => args.Contains("-t filesystem"));
        var fields = query.Split(" -o ", 2)[1].Split(' ', 2)[0].Split(',');
        Assert.Contains("encryptionroot", fields);
        Assert.Contains("keylocation", fields);
        Assert.Contains("keyformat", fields);
        Assert.Equal(expectedSource, page.Encryption?.Key?.Source);
        Assert.Equal(format, page.Encryption?.Key?.Format);
        Assert.Equal(expectedPath, page.Encryption?.Key?.FilePath);
        Assert.Contains(page.Suggestions, s => s.ZfsCommand == "sudo zfs set keylocation=file:///etc/zfs/new.key 'zfsPool'");
        Assert.Equal(status == "unavailable" ? "sudo zfs load-key 'zfsPool'" : null, page.Encryption?.LoadKeyCommand);
    }

    private sealed class RecordingDemoExecutor(string? datasetsJson = null) : ICommandExecutor
    {
        private readonly EmbeddedJsonCommandExecutor demo = new();
        internal List<string> ZfsQueries { get; } = [];

        public Task<string> ExecuteAsync(string command, string arguments, CancellationToken cancellationToken = default)
        {
            if (command == "zfs") this.ZfsQueries.Add(arguments);
            if (datasetsJson != null && arguments.Contains("-t filesystem")) return Task.FromResult(datasetsJson);
            return this.demo.ExecuteAsync(command, arguments, cancellationToken);
        }
    }
}
