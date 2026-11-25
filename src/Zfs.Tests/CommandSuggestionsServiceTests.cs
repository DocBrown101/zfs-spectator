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

    [Fact]
    public void SuggestDestroyDatasetRecursive_UsesCompleteDatasetName()
    {
        var suggestion = CommandSuggestionsService.SuggestDestroyDatasetRecursive("tank/currentdataset");

        Assert.Equal("sudo zfs destroy -r tank/currentdataset", suggestion.ZfsCommand);
    }
}
