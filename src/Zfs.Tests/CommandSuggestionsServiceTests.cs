namespace Zfs.Tests;

using Zfs.Core.Services;

public class CommandSuggestionsServiceTests
{
    [Theory]
    [InlineData("tank/Meine Daten", "'tank/Meine Daten'")]
    [InlineData("tank/O'Brien", "'tank/O'\\''Brien'")]
    public void SuggestSetProperty_QuotesDatasetOnlyInCommand(string dataset, string quotedDataset)
    {
        var suggestion = CommandSuggestionsService.SuggestSetProperty(dataset, "quota", "none");

        Assert.Equal($"sudo zfs set quota=none {quotedDataset}", suggestion.ZfsCommand);
        Assert.Equal($"Set quota to none on {dataset}", suggestion.Description);
    }

    [Theory]
    [InlineData("recordsize", "1M")]
    [InlineData("sync", "always")]
    public void SuggestSetProperty_UnsupportedProperty_OmitsNixOsConfig(string property, string value)
    {
        var suggestion = CommandSuggestionsService.SuggestSetProperty("tank", property, value);

        Assert.Equal($"sudo zfs set {property}={value} 'tank'", suggestion.ZfsCommand);
        Assert.Null(suggestion.NixOsConfig);
    }

    [Theory]
    [InlineData("tank/Meine Daten", "'tank/Meine Daten'", "'tank/Meine Daten_renamed'")]
    [InlineData("tank/O'Brien", "'tank/O'\\''Brien'", "'tank/O'\\''Brien_renamed'")]
    public void DatasetCommands_QuoteCompleteNames(string dataset, string quotedDataset, string quotedRenamed)
    {
        Assert.Equal($"sudo zfs destroy {quotedDataset}", CommandSuggestionsService.SuggestDestroyDataset(dataset).ZfsCommand);
        Assert.Equal($"sudo zfs destroy -r {quotedDataset}", CommandSuggestionsService.SuggestDestroyDatasetRecursive(dataset).ZfsCommand);
        Assert.Equal($"sudo zfs rename {quotedDataset} {quotedRenamed}", CommandSuggestionsService.SuggestRenameDataset(dataset).ZfsCommand);
    }

    [Theory]
    [InlineData("tank/Meine Daten@daily", "'tank/Meine Daten@daily'")]
    [InlineData("tank/O'Brien@daily", "'tank/O'\\''Brien@daily'")]
    public void SnapshotCommands_QuoteCompleteNames(string snapshot, string quotedSnapshot)
    {
        Assert.Equal($"sudo zfs rollback -r {quotedSnapshot}", CommandSuggestionsService.SuggestRollback(snapshot).ZfsCommand);
        Assert.Equal($"sudo zfs destroy {quotedSnapshot}", CommandSuggestionsService.SuggestDestroySnapshot(snapshot).ZfsCommand);
    }
}
