using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProductService.Application.Common.Behaviors;
using ProductService.Domain.Products;
using ProductService.Infrastructure.Persistence;
using ProductService.Infrastructure.Persistence.Repositories;
using Shared.Domain.Primitives;
using Shared.Infrastructure.Messaging;
using Shared.Infrastructure.Outbox;
using Shared.Infrastructure.Persistence.Interceptors;

namespace ProductService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddProductInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // EF Core with outbox interceptor
        services.AddSingleton<DomainEventToOutboxInterceptor>();

        services.AddDbContext<ProductDbContext>((sp, opts) =>
        {
            var interceptor = sp.GetRequiredService<DomainEventToOutboxInterceptor>();
            opts.UseSqlServer(
                    configuration.GetConnectionString("ProductDb"),
                    sql => sql.MigrationsAssembly(typeof(ProductDbContext).Assembly.FullName))
                .AddInterceptors(interceptor);
        });

        // Repository and Unit of Work (Database Per Service)
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<ProductDbContext>());

        // MediatR + pipeline behaviors
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(
                typeof(ProductService.Application.Products.Commands.CreateProduct.CreateProductCommand).Assembly);
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingPipelineBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationPipelineBehavior<,>));
        });

        // FluentValidation
        services.AddValidatorsFromAssembly(
            typeof(ProductService.Application.Products.Commands.CreateProduct.CreateProductCommandValidator).Assembly);

        // Event Bus (factory-driven — switches between ASB/RabbitMQ/Kafka)
        services.AddEventBus(configuration);

        // Outbox processor background service
        services.AddHostedService<OutboxProcessor<ProductDbContext>>();

        return services;
    }
}
