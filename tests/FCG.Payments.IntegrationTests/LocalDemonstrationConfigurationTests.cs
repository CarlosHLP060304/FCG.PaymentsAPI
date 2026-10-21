using FCG.Payments.Api.Startup;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FCG.Payments.IntegrationTests;

public sealed class LocalDemonstrationConfigurationTests
{
    [Theory]
    [InlineData(null, "Development", false)]
    [InlineData(null, "Production", false)]
    [InlineData("false", "Production", false)]
    [InlineData("false", "Development", false)]
    [InlineData("true", "Development", true)]
    public void IsEnabled_RequiresExplicitOptInInDevelopment(
        string? configuredValue, string environmentName, bool expected)
    {
        var configuration = Configuration(configuredValue);

        Assert.Equal(expected, LocalDemonstrationDatabase.IsEnabled(configuration, environmentName));
    }

    [Theory]
    [InlineData("true", "Production")]
    [InlineData("true", "Staging")]
    [InlineData("yes", "Development")]
    [InlineData("1", "Development")]
    [InlineData("", "Development")]
    public void IsEnabled_RejectsUnsafeOrMalformedConfiguration(
        string configuredValue, string environmentName)
    {
        var configuration = Configuration(configuredValue);

        Assert.Throws<InvalidOperationException>(() =>
            LocalDemonstrationDatabase.IsEnabled(configuration, environmentName));
    }

    private static IConfiguration Configuration(string? value) =>
        new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["DemonstracaoLocal:Habilitada"] = value }).Build();
}
