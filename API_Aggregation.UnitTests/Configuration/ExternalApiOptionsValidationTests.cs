using API_Aggregation.Application.Abstractions;
using API_Aggregation.Application.Options;
using API_Aggregation.Infrastructure.Configurations;
using API_Aggregation.Infrastructure.ExternalApis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace API_Aggregation.UnitTests.Configuration;

public class ExternalApiOptionsValidationTests
{
    private readonly ExternalApiOptionsValidator _validator = new ExternalApiOptionsValidator();

    private static ExternalApiOptions _valid() => new ExternalApiOptions { BaseUrl = "https://api.example.com/" };

    [Fact]
    public void Validate_Succeeds_ForValidOptions() =>
        Assert.True(_validator.Validate("Api", _valid()).Succeeded);

    [Fact]
    public void Validate_Fails_WhenBaseUrlIsMissing_AndMentionsTheSection()
    {
        var result = _validator.Validate("GitHub", new ExternalApiOptions());

        Assert.True(result.Failed);
        Assert.Contains("'ExternalApis:GitHub:BaseUrl' is required", result.FailureMessage);
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("/relative/path")]
    [InlineData("ftp://api.example.com")]
    public void Validate_Fails_ForAnInvalidBaseUrl(string baseUrl)
    {
        var options = _valid();
        options.BaseUrl = baseUrl;

        Assert.True(_validator.Validate("Api", options).Failed);
    }

    [Theory]
    [InlineData(nameof(ExternalApiOptions.MaxItems))]
    [InlineData(nameof(ExternalApiOptions.CacheDurationSeconds))]
    [InlineData(nameof(ExternalApiOptions.StaleCacheDurationMinutes))]
    public void Validate_Fails_WhenAPositiveValueIsZero(string property)
    {
        var options = _valid();
        typeof(ExternalApiOptions).GetProperty(property)!.SetValue(options, 0);

        var result = _validator.Validate("Api", options);

        Assert.True(result.Failed);
        Assert.Contains(property, result.FailureMessage);
    }

    [Fact]
    public void Registration_SkipsTheClient_WhenTheConfigurationSectionIsMissing()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
        using var provider = new ServiceCollection()
            .AddExternalApiClient<HackerNewsClient>(HackerNewsClient.ApiName, configuration)
            .BuildServiceProvider();

        Assert.Empty(provider.GetServices<IExternalApiClient>());
        Assert.Null(provider.GetService<HackerNewsClient>());
    }

    [Fact]
    public void Registration_OnlyRegistersTheConfiguredClients()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ExternalApis:OpenMeteo:BaseUrl"] = "https://api.open-meteo.com/",
            ["ExternalApis:GitHub:BaseUrl"] = "https://api.github.com/"
            // no HackerNews section
        }).Build();
        using var provider = new ServiceCollection()
            .AddLogging()
            .AddSingleton(TimeProvider.System)
            .AddExternalApis(configuration)
            .BuildServiceProvider();

        var names = provider.GetServices<IExternalApiClient>().Select(c => c.Name);

        Assert.Equal([OpenMeteoClient.ApiName, GitHubClient.ApiName], names);
    }

    [Fact]
    public void Registration_Throws_WhenAPresentSectionIsInvalid()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ExternalApis:HackerNews:BaseUrl"] = "not-a-url"
        }).Build();
        using var provider = new ServiceCollection()
            .AddExternalApiClient<HackerNewsClient>(HackerNewsClient.ApiName, configuration)
            .BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptionsMonitor<ExternalApiOptions>>().Get(HackerNewsClient.ApiName));

        Assert.Contains("ExternalApis:HackerNews:BaseUrl", exception.Message);
    }

    [Fact]
    public void Registration_Succeeds_WhenTheConfigurationSectionIsPresent()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ExternalApis:HackerNews:BaseUrl"] = "https://hn.algolia.com/",
            ["ExternalApis:HackerNews:MaxItems"] = "5"
        }).Build();
        using var provider = new ServiceCollection()
            .AddExternalApiClient<HackerNewsClient>(HackerNewsClient.ApiName, configuration)
            .BuildServiceProvider();

        var options = provider.GetRequiredService<IOptionsMonitor<ExternalApiOptions>>().Get(HackerNewsClient.ApiName);

        Assert.Equal(5, options.MaxItems);
    }
}
