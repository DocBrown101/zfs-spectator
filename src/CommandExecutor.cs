using System.Diagnostics;
using Zfs.Core;

namespace ZfsDashboard;

public class CommandExecutor(ILogger<CommandExecutor> logger) : ICommandExecutor
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(20);

    public async Task<string> ExecuteAsync(string command, string arguments)
    {
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
            return "";
        }

        using var cts = new CancellationTokenSource(CommandTimeout);

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
                logger.LogError("Command {Command} {Arguments} exited with code {ExitCode}", command, arguments, process.ExitCode);

            return outputTask.Result.Trim();
        }
        catch (OperationCanceledException)
        {
            logger.LogError("Command {Command} {Arguments} timed out after {Timeout}s", command, arguments, CommandTimeout.TotalSeconds);
            this.KillProcess(process);
            return "";
        }
    }

    private void KillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to kill timed-out process");
        }
    }
}
