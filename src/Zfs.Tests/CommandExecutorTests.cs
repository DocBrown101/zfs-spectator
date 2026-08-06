using Microsoft.Extensions.Logging.Abstractions;
using ZfsDashboard;

namespace Zfs.Tests;

public class CommandExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_StartFailureThrows()
    {
        var executor = new CommandExecutor(NullLogger<CommandExecutor>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ExecuteAsync("/definitely/not/a/command", ""));
    }

    [Fact]
    public async Task ExecuteAsync_CancellationStopsCommand()
    {
        var executor = new CommandExecutor(NullLogger<CommandExecutor>.Instance);
        var pidFile = Path.Combine(Path.GetTempPath(), $"zfs-spectator-{Guid.NewGuid():N}.pid");
        using var cancellation = new CancellationTokenSource();
        var execution = executor.ExecuteAsync(
            "sh",
            $"-c \"echo $$ > '{pidFile}'; exec sleep 10\"",
            cancellation.Token);

        try
        {
            var pidText = "";
            for (var attempt = 0; attempt < 100 && pidText.Length == 0; attempt++)
            {
                await Task.Delay(10);
                if (File.Exists(pidFile))
                    pidText = (await File.ReadAllTextAsync(pidFile)).Trim();
            }

            Assert.True(int.TryParse(pidText, out var pid), "The command did not publish its process ID");
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => execution);
            Assert.False(Directory.Exists($"/proc/{pid}"));
        }
        finally
        {
            cancellation.Cancel();
            try
            {
                await execution;
            }
            catch (OperationCanceledException)
            {
            }
            File.Delete(pidFile);
        }
    }

    [Fact]
    public async Task ExecuteAsync_NonZeroExitThrows()
    {
        var executor = new CommandExecutor(NullLogger<CommandExecutor>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ExecuteAsync("false", ""));
    }

    [Fact]
    public async Task ExecuteAsync_AlreadyCancelledDoesNotStartCommand()
    {
        var executor = new CommandExecutor(NullLogger<CommandExecutor>.Instance);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            executor.ExecuteAsync("/definitely/not/a/command", "", cancellation.Token));
    }
}
