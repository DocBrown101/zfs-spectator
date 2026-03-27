namespace Zfs.Tests.Helper;

using Zfs.Core;

/// <summary>
/// A fake command executor that returns canned responses based on exact command + arguments match,
/// falling back to substring matching. Later registrations take priority (override earlier ones).
/// </summary>
internal class FakeCommandExecutor : ICommandExecutor
{
    private readonly List<(string Command, string Args, bool Exact, string Response)> responses = [];

    public FakeCommandExecutor On(string command, string args, string response)
    {
        // Insert at front so later registrations win (last-registered-wins override semantics)
        this.responses.Insert(0, (command, args, Exact: true, response));
        return this;
    }

    public FakeCommandExecutor OnContains(string command, string argsContains, string response)
    {
        this.responses.Insert(0, (command, argsContains, Exact: false, response));
        return this;
    }

    public Task<string> ExecuteAsync(string command, string arguments)
    {
        foreach (var (cmd, args, exact, response) in this.responses)
        {
            if (command != cmd) continue;
            if (exact ? arguments == args : arguments.Contains(args))
                return Task.FromResult(response);
        }
        return Task.FromResult("");
    }
}
