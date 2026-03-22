namespace Zfs.Core;

public interface ICommandExecutor
{
    Task<string> ExecuteAsync(string command, string arguments);
}
