namespace Zfs.Tests;

using Zfs.Core.Services;

public class CommandSuggestionsServiceTests
{
    [Theory]
    [InlineData("compression", "zstd")]
    [InlineData("atime", "on")]
    [InlineData("quota", "1T")]
    public void SuggestSetProperty_SupportedProperty_ProvidesNixOsConfig(string property, string value)
    {
        var suggestion = CommandSuggestionsService.SuggestSetProperty("tank/data", property, value);

        Assert.Equal($"sudo zfs set {property}={value} tank/data", suggestion.ZfsCommand);
        Assert.NotNull(suggestion.NixOsConfig);
    }

    [Theory]
    [InlineData("recordsize", "1M")]
    [InlineData("sync", "always")]
    public void SuggestSetProperty_UnsupportedProperty_OmitsNixOsConfig(string property, string value)
    {
        var suggestion = CommandSuggestionsService.SuggestSetProperty("tank", property, value);

        Assert.Equal($"sudo zfs set {property}={value} tank", suggestion.ZfsCommand);
        Assert.Null(suggestion.NixOsConfig);
    }
}
