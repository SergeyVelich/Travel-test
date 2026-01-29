using Travel.WebApi.Services;

namespace Travel.WebApi;

public class OrderBackgroundService(IOrderProcessingQueue queue, IServiceProvider services, ILogger<OrderBackgroundService> logger) : BackgroundService
{
    private readonly IOrderProcessingQueue _queue = queue;
    private readonly IServiceProvider _services = services;
    private readonly ILogger<OrderBackgroundService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Order background service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var orderId = await _queue.DequeueAsync(stoppingToken);

            try
            {
                using var scope = _services.CreateScope();
                var orderService = scope.ServiceProvider.GetRequiredService<IOrderService>();
                await orderService.SubmitOrderAsync(orderId.Value, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing order {OrderId}", orderId.Value);
            }
        }

        _logger.LogInformation("Order background service stopping.");
    }
}
