namespace Zfs.Tests.Helper;

using Zfs.Core;

/// <summary>
/// An executor that returns different responses on successive calls for a specific command+args pair.
/// Returns empty string for unmatched commands.
/// </summary>
internal class SequentialExecutor(string expectedCommand, string expectedArgs, Func<string> responseFactory) : ICommandExecutor
{
    public Task<string> ExecuteAsync(string command, string arguments)
    {
        if (command == expectedCommand && arguments == expectedArgs)
            return Task.FromResult(responseFactory());
        return Task.FromResult("");
    }
}
