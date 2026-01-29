using Travel.WebApi.WebApi.Mapping;

namespace Travel.WebApi.Application.Configuration;

internal static class ApplicationServicesCollectionExtensions
{
    internal static IServiceCollection AddLogicServices(this IServiceCollection services)
    {
        services.AddScoped<IOrderService, OrderService>();
        services.AddSingleton<IMapper, Mapper>();
        services.AddSingleton<IOrderMessagePublisher, OrderMessagePublisher>();
        services.AddHostedService<OrderMessageConsumer>();
        services.AddHostedService<OutboxProcessorService>();

        return services;
    }

    internal static IServiceCollection AddApplicationMetrics(this IServiceCollection services)
    {
        services.AddSingleton<IApplicationMetrics, ApplicationMetrics>();

        return services;
    }
}