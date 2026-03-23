namespace Zfs.Tests;

using Zfs.Core.Services.Parser;

public class JsonHelperTests
{
    [Theory]
    [InlineData("0B", 0)]
    [InlineData("B", 0)]
    [InlineData("", 0)]
    [InlineData("-", 0)]
    [InlineData(null, 0)]
    [InlineData("1K", 1024)]
    [InlineData("1M", 1024 * 1024)]
    [InlineData("1G", 1024.0 * 1024 * 1024)]
    [InlineData("100G", 100 * 1024.0 * 1024 * 1024)]
    [InlineData("1T", 1024.0 * 1024 * 1024 * 1024)]
    [InlineData("8.64T", 8.64 * 1024.0 * 1024 * 1024 * 1024)]
    [InlineData("7.65T", 7.65 * 1024.0 * 1024 * 1024 * 1024)]
    [InlineData("3.01M", 3.01 * 1024.0 * 1024)]
    [InlineData("1P", 1024.0 * 1024 * 1024 * 1024 * 1024)]
    [InlineData("1E", 1024.0 * 1024 * 1024 * 1024 * 1024 * 1024)]
    [InlineData("xyz", 0)]
    public void ParseByteString_ShouldParseCorrectly(string? input, double expected)
    {
        var result = JsonHelper.ParseByteString(input!);

        Assert.Equal(expected, result, precision: 0);
    }
}
