using System.Net;
using FCG.Payments.Api.Messaging;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace FCG.Payments.IntegrationTests;

public sealed class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public HealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                var massTransitHostedService = services.FirstOrDefault(descriptor =>
                    descriptor.ImplementationType?.FullName == "MassTransit.MassTransitHostedService");
                if (massTransitHostedService is not null)
                {
                    services.Remove(massTransitHostedService);
                }

                var outboxHostedService = services.FirstOrDefault(descriptor =>
                    descriptor.ImplementationType == typeof(PaymentOutboxBackgroundService));
                if (outboxHostedService is not null)
                {
                    services.Remove(outboxHostedService);
                }
            })).CreateClient();
    }

    [Fact]
    public async Task GetHealth_ReturnsHealthy()
    {
        using var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }
}
