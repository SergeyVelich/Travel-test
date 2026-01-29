namespace Travel.WebApi.Infrastructure.Rabbit.Configuration;

public class RabbitConfig
{
    public const string SectionName = "RabbitMQ";

    public string ConnectionString { get; set; } = string.Empty;
    public string QueueName { get; set; } = "order-processing-queue";
    public bool Durable { get; set; } = true;
    public bool AutoDelete { get; set; } = false;
    public ushort PrefetchCount { get; set; } = 1;
    public ushort PrefetchSize { get; set; } = 0;
    public int ChannelPoolSize { get; set; } = 10;
    public int MaxChannels { get; set; } = 50;
}
