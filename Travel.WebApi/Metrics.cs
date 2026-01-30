namespace Travel.WebApi;

public static class Metrics
{
    private static int _processedOrdersNumber;
    public static int ProcessedOrdersNumber => _processedOrdersNumber;

    public static int IncrementProcessedOrdersNumber()
    {
        return Interlocked.Increment(ref _processedOrdersNumber);
    }
}
