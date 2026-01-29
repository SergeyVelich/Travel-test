using System.Diagnostics.Metrics;

namespace Travel.WebApi.Application;

public interface IApplicationMetrics
{
    void IncrementProcessedOrders();
    long GetProcessedOrdersNumber();
}

public sealed class ApplicationMetrics : IApplicationMetrics
{
    private const string ProcessedOrdersCounterName = "orders.processed";
    private const string ProcessedOrdersCounterDescription = "Total number of processed orders";
    
    private readonly Counter<long> _processedOrdersCounter;
    private long _processedOrdersNumber;

    public ApplicationMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("Travel.WebApi", "1.0.0");
        _processedOrdersCounter = meter.CreateCounter<long>(
            ProcessedOrdersCounterName,
            unit: "{orders}",
            description: ProcessedOrdersCounterDescription);
    }

    public void IncrementProcessedOrders()
    {
        Interlocked.Increment(ref _processedOrdersNumber);
        _processedOrdersCounter.Add(1);
    }

    public long GetProcessedOrdersNumber() => Interlocked.Read(ref _processedOrdersNumber);
}
