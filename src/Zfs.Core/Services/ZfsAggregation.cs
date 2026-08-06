namespace Zfs.Core.Services;

public static class ZfsAggregation
{
    public static async Task<List<(string Pool, List<T> Items)>> GetAllByPoolAsync<T>(
        IZpoolService zpool,
        Func<string, Task<List<T>>> fetch)
    {
        var names = await zpool.GetPoolNamesAsync();
        var results = await Task.WhenAll(names.Select(fetch));
        return names.Select((name, i) => (name, results[i])).ToList();
    }
}
