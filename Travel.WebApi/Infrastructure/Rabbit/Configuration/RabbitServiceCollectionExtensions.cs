namespace Travel.WebApi.Infrastructure.Rabbit.Configuration;

public static class RabbitServiceCollectionExtensions
{
    public static IServiceCollection AddRabbitMqServices(
        this IServiceCollection services,
        RabbitConfig? config)
    {
        ArgumentNullException.ThrowIfNull(config);

        services.AddSingleton<IRabbitMQConnectionFactory, RabbitConnectionFactory>();
        
        services.AddSingleton<IProducerConnection, ConsumerConnection>();
        services.AddSingleton<IRabbitConnection, ConsumerConnection>();

        services.AddSingleton<IRabbitChannelPool>(sp =>
        {
            var producerConn = sp.GetRequiredService<IProducerConnection>();
            var logger = sp.GetRequiredService<ILogger<RabbitChannelPool>>();
            
            //TODO: Initialize connection synchronously in DI - not ideal but needed for singleton registration
            producerConn.InitializeAsync("Producer", CancellationToken.None).GetAwaiter().GetResult();
            
            return new RabbitChannelPool(producerConn, logger);
        });

        return services;
    }
}