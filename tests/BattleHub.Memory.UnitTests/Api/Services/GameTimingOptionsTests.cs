using BattleHub.Memory.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BattleHub.Memory.UnitTests.Api.Services;

public sealed class GameTimingOptionsTests
{
    private static readonly Dictionary<string, string?> ValidSettings = new()
    {
        ["GameTiming:TurnTimeoutSeconds"] = "20",
        ["GameTiming:PreviewSeconds"] = "7",
        ["GameTiming:ResultRetryIntervalSeconds"] = "3"
    };

    // Misma validación que registra Program.cs.
    private static GameTimingOptions Read(IEnumerable<KeyValuePair<string, string?>> settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<GameTimingOptions>()
            .Bind(configuration.GetSection(GameTimingOptions.SectionName))
            .ValidateDataAnnotations();

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<GameTimingOptions>>().Value;
    }

    [Fact]
    public void ReadsTheValuesFromTheConfiguration()
    {
        var timing = Read(ValidSettings);

        Assert.Equal(20, timing.TurnTimeoutSeconds);
        Assert.Equal(7, timing.PreviewSeconds);
        Assert.Equal(3, timing.ResultRetryIntervalSeconds);
    }

    [Theory]
    [InlineData("GameTiming:TurnTimeoutSeconds", "0")]
    [InlineData("GameTiming:TurnTimeoutSeconds", "-1")]
    [InlineData("GameTiming:TurnTimeoutSeconds", "3601")]
    [InlineData("GameTiming:PreviewSeconds", "0")]
    [InlineData("GameTiming:PreviewSeconds", "-1")]
    [InlineData("GameTiming:PreviewSeconds", "3601")]
    [InlineData("GameTiming:ResultRetryIntervalSeconds", "0")]
    [InlineData("GameTiming:ResultRetryIntervalSeconds", "-1")]
    [InlineData("GameTiming:ResultRetryIntervalSeconds", "3601")]
    public void RejectsValuesOutOfRange(string key, string value)
    {
        var settings = new Dictionary<string, string?>(ValidSettings) { [key] = value };

        Assert.Throws<OptionsValidationException>(() => Read(settings));
    }

    [Fact]
    public void RejectsAMissingSection()
    {
        Assert.Throws<OptionsValidationException>(() => Read([]));
    }
}
