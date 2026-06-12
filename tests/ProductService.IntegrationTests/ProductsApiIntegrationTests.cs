using DotNet.Testcontainers.Builders;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProductService.Infrastructure.Persistence;
using Testcontainers.MsSql;
using Xunit;

namespace ProductService.IntegrationTests;

/// <summary>
/// Integration test factory using Testcontainers.
/// Spins up a real SQL Server in Docker for each test class.
/// Tests the full stack: Controller → MediatR → EF Core → SQL Server.
/// </summary>
public sealed class ProductServiceWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _sqlContainer = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword("Test@1234!Strong")
        .WithCleanUp(true)
        .Build();

    public async Task InitializeAsync()
    {
        await _sqlContainer.StartAsync();
    }

    public new async Task DisposeAsync()
    {
        await _sqlContainer.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove existing DbContext registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ProductDbContext>));
            if (descriptor is not null) services.Remove(descriptor);

            // Register with Testcontainers SQL Server
            services.AddDbContext<ProductDbContext>(options =>
            {
                options.UseSqlServer(_sqlContainer.GetConnectionString());
            });

            // Auto-migrate
            using var scope = services.BuildServiceProvider().CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ProductDbContext>();
            db.Database.Migrate();
        });

        builder.UseEnvironment("Testing");
    }
}

/// <summary>
/// Integration tests for Products API.
/// Tests real HTTP requests through the full pipeline.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ProductsApiIntegrationTests : IClassFixture<ProductServiceWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ProductsApiIntegrationTests(ProductServiceWebApplicationFactory factory) =>
        _client = factory.CreateClient();

    [Fact]
    public async Task GetCatalog_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/products");

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetById_NonExistent_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync($"/api/v1/products/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var request = new
        {
            name = "Laptop",
            description = "Desc",
            price = 999.99,
            currency = "USD",
            categoryId = Guid.NewGuid(),
            sku = "LAPTOP-001"
        };

        var content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(request),
            System.Text.Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/v1/products", content);

        // Assert — requires authentication
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }
}

// FluentAssertions extension import
using FluentAssertions;
