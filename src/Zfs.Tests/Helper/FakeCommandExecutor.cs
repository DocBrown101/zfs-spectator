namespace Zfs.Tests.Helper;

using Zfs.Core;

/// <summary>
/// A fake command executor that returns canned responses for exact command + arguments matches.
/// Later registrations take priority (override earlier ones).
/// </summary>
internal class FakeCommandExecutor : ICommandExecutor
{
    private readonly List<(string Command, string Args, string Response)> responses = [];
    public List<(string Command, string Arguments)> Invocations { get; } = [];

    public FakeCommandExecutor On(string command, string args, string response)
    {
        // Insert at front so later registrations win (last-registered-wins override semantics)
        this.responses.Insert(0, (command, args, response));
        return this;
    }

    public Task<string> ExecuteAsync(string command, string arguments, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        this.Invocations.Add((command, arguments));
        foreach (var (cmd, args, response) in this.responses)
        {
            if (command == cmd && arguments == args)
                return Task.FromResult(response);
        }
        return Task.FromResult("");
    }
}
