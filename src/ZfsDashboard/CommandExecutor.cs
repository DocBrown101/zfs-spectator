using System.Diagnostics;
using Zfs.Core;

namespace ZfsDashboard;

public class CommandExecutor(ILogger<CommandExecutor> logger) : ICommandExecutor
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan ProcessTerminationTimeout = TimeSpan.FromSeconds(2);

    public async Task<string> ExecuteAsync(string command, string arguments, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start {Command} {Arguments}", command, arguments);
            throw new InvalidOperationException($"Failed to start {command}", ex);
        }

        using var timeoutCts = new CancellationTokenSource(CommandTimeout);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            // Read stdout/stderr concurrently with waiting for exit to avoid
            // deadlocks when the process fills its output buffer.
            var outputTask = process.StandardOutput.ReadToEndAsync(cts.Token);
            var errorTask = process.StandardError.ReadToEndAsync(cts.Token);
            var exitTask = process.WaitForExitAsync(cts.Token);

            await Task.WhenAll(outputTask, errorTask, exitTask);

            if (!string.IsNullOrWhiteSpace(errorTask.Result))
                logger.LogWarning("stderr from {Command} {Arguments}: {StdErr}", command, arguments, errorTask.Result);

            if (process.ExitCode != 0)
            {
                logger.LogError("Command {Command} {Arguments} exited with code {ExitCode}", command, arguments, process.ExitCode);
                throw new InvalidOperationException($"{command} exited with code {process.ExitCode}: {errorTask.Result.Trim()}");
            }

            return outputTask.Result.Trim();
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Command {Command} {Arguments} timed out after {Timeout}s", command, arguments, CommandTimeout.TotalSeconds);
            await this.KillProcessAsync(process);
            throw new TimeoutException($"{command} timed out after {CommandTimeout.TotalSeconds} seconds");
        }
        catch (OperationCanceledException)
        {
            await this.KillProcessAsync(process);
            throw;
        }
    }

    private async Task KillProcessAsync(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None).WaitAsync(ProcessTerminationTimeout);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to terminate process");
        }
    }
}
