# Travel.WebApi - Complete System Generation Prompt

## Project Overview
Create a **Travel.WebApi** - a distributed order processing system demonstrating the **Transactional Outbox Pattern** with **message-based architecture** using .NET 8, PostgreSQL, RabbitMQ, and EF Core.

## Technology Stack
- **Framework**: .NET 8.0 (C# 12)
- **Database**: PostgreSQL with Npgsql.EntityFrameworkCore.PostgreSQL (8.0.11)
- **Message Broker**: RabbitMQ.Client (7.2.0)
- **API**: ASP.NET Core Minimal APIs
- **Validation**: FluentValidation.DependencyInjectionExtensions (12.1.1)
- **Documentation**: Swagger/OpenAPI (Swashbuckle 8.1.4)
- **Health Checks**: AspNetCore.HealthChecks.RabbitMQ & NpgSql (8.0.2)
- **ORM**: Entity Framework Core 8.0.23
- **Containerization**: Docker with Docker Compose

## Architecture Overview

### Folder Structure
```
Travel.WebApi/
??? Application/                    # Business Logic & Services
?   ??? Configuration/
?   ?   ??? ApplicationServicesCollectionExtensions.cs
?   ??? OrderService.cs            # Core business logic
?   ??? OrderMessageConsumer.cs    # RabbitMQ consumer (Background Service)
?   ??? OrderMessagePublisher.cs   # RabbitMQ publisher
?   ??? OutboxProcessorService.cs  # Outbox pattern processor (Background Service)
?   ??? ApplicationMetrics.cs      # Metrics tracking
?   ??? ApplicationConstants.cs    # Shared constants
??? Data/                          # Data Access Layer
?   ??? Configuration/
?   ?   ??? DatabaseServiceExtensions.cs
?   ??? Configurations/            # EF Core entity configurations
?   ?   ??? OrderConfiguration.cs
?   ?   ??? OrderItemConfiguration.cs
?   ?   ??? ItemConfiguration.cs
?   ?   ??? InventoryConfiguration.cs
?   ?   ??? OutboxMessageConfiguration.cs
?   ?   ??? ProcessedMessageConfiguration.cs
?   ??? Migrations/                # EF Core migrations
?   ??? AppDbContext.cs           # Main DbContext
?   ??? MigrationExtensions.cs    # Auto-migration helper
??? Domain/                        # Domain Models & Events
?   ??? Entities/
?   ?   ??? Order.cs
?   ?   ??? OrderItem.cs
?   ?   ??? Item.cs
?   ?   ??? Inventory.cs
?   ?   ??? OutboxMessage.cs
?   ?   ??? ProcessedMessage.cs
?   ?   ??? OrderStatuses.cs (enum)
?   ??? Events/
?       ??? OrderCreatedEvent.cs
??? Infrastructure/                # Infrastructure Services
?   ??? Rabbit/
?       ??? Configuration/
?       ?   ??? RabbitConfig.cs
?       ?   ??? RabbitServiceCollectionExtensions.cs
?       ??? RabbitConnectionFactory.cs
?       ??? RabbitChannelPool.cs
?       ??? ConsumerConnection.cs
??? WebApi/                        # Web Layer
?   ??? Configuration/
?   ?   ??? SwaggerServiceExtensions.cs
?   ??? Endpoints/
?   ?   ??? Orders/
?   ?   ?   ??? Models/
?   ?   ?   ?   ??? CreateOrderRequest.cs
?   ?   ?   ??? Validators/
?   ?   ?   ?   ??? CreateOrderRequestValidator.cs
?   ?   ?   ??? OrderEndpoints.cs
?   ?   ??? Infrastructure/
?   ?   ?   ??? InfrastructureEndpoints.cs
?   ?   ??? EndpointExtensions.cs
?   ??? Filters/
?   ?   ??? ValidationFilter.cs
?   ??? Mapping/
?   ?   ??? Mapper.cs
?   ??? ServiceCollectionExtensions.cs
?   ??? WebApiConstants.cs
??? Program.cs                     # Application entry point
??? Dockerfile
??? appsettings.json
```

## Domain Model

### Entities (All IDs are `long` for scalability)

#### 1. **Order** (`Orders` table)
```csharp
public class Order
{
    public long Id { get; set; }
    public long CustomerId { get; set; }
    public DateTime OrderDate { get; set; }
    public decimal TotalAmount { get; set; }
    public OrderStatuses Status { get; set; }
    public ICollection<OrderItem> Items { get; set; } = [];
}
```

**Configuration**:
- HasKey: Id
- OrderDate: default SQL = NOW()
- Status: Stored as int, default = New
- TotalAmount: Precision(18,2)
- HasMany Items with cascade delete

#### 2. **OrderItem** (`OrderItems` table) - Composite Key
```csharp
public class OrderItem
{
    public long OrderId { get; set; }
    public Order? Order { get; set; }
    public long ItemId { get; set; }
    public Item? Item { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}
```

**Configuration**:
- Composite key: (OrderId, ItemId)
- Price: Precision(18,2)
- Relations to Order and Item

#### 3. **Item** (`Items` table)
```csharp
public class Item
{
    public long Id { get; set; }
    public string Name { get; set; } = null!;
    public decimal Price { get; set; }
    public Inventory? Inventory { get; set; }
    public ICollection<OrderItem> OrderItems { get; set; } = [];
}
```

**Configuration**:
- HasKey: Id
- Name: MaxLength(200)
- Price: Precision(18,2)
- One-to-one with Inventory
- HasMany OrderItems with restrict delete

#### 4. **Inventory** (`Inventory` table)
```csharp
public class Inventory
{
    public long ItemId { get; set; }
    public Item? Item { get; set; }
    public int InStock { get; set; }
    public int Reserved { get; set; }
    public int InTransit { get; set; }
    public int Available => InStock - Reserved;
}
```

**Configuration**:
- HasKey: ItemId
- One-to-one with Item (cascade delete)
- Ignore: Available property (computed)

#### 5. **OutboxMessage** (`OutboxMessages` table)
```csharp
public class OutboxMessage
{
    public long Id { get; set; }
    public string Type { get; set; } = null!;
    public string Payload { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? Error { get; set; }
    public int RetryCount { get; set; }
}
```

**Configuration**:
- HasKey: Id
- Type: MaxLength(255)
- Error: MaxLength(2000)
- RetryCount: Default(0)
- Composite index: (ProcessedAt, RetryCount, CreatedAt) with filter WHERE ProcessedAt IS NULL
- Index name: IX_OutboxMessages_Unprocessed_Query

#### 6. **ProcessedMessage** (`ProcessedMessages` table)
```csharp
public class ProcessedMessage
{
    public long Id { get; set; }
    public long EntityId { get; set; }
    public string MessageId { get; set; } = null!;
    public DateTime ProcessedAt { get; set; }
    public string MessageType { get; set; } = null!;
}
```

**Configuration**:
- HasKey: Id
- EntityId: Required (long, no maxLength)
- MessageId: MaxLength(36)
- MessageType: MaxLength(100)
- Unique index: (MessageId, EntityId)
- Index name: IX_ProcessedMessages_MessageId_EntityId_Unique

#### 7. **OrderStatuses** (Enum)
```csharp
public enum OrderStatuses
{
    New = 0,
    Submitted = 1,
    Completed = 2
}
```

### Events

#### **OrderCreatedEvent**
```csharp
public class OrderCreatedEvent
{
    public long OrderId { get; set; }
    public string? CorrelationId { get; set; }
}
```

## Core Business Logic

### OrderService Implementation

#### **CreateOrderAsync** - Creates order with outbox pattern
```csharp
public async Task<long> CreateOrderAsync(Order order, CancellationToken cancellationToken)
{
    using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
    try
    {
        var correlationId = Guid.NewGuid().ToString();
        
        _db.Orders.Add(order);
        await _db.SaveChangesAsync(cancellationToken); // Generates order.Id
        
        var orderCreatedEvent = new OrderCreatedEvent
        {
            OrderId = order.Id, // Now has generated ID
            CorrelationId = correlationId
        };
        
        var orderCreatedOutbox = new OutboxMessage
        {
            Type = nameof(OrderCreatedEvent),
            Payload = JsonSerializer.Serialize(orderCreatedEvent),
            CreatedAt = DateTime.UtcNow
        };
        
        _db.OutboxMessages.Add(orderCreatedOutbox);
        await _db.SaveChangesAsync(cancellationToken);
        
        await transaction.CommitAsync(cancellationToken);
        
        return order.Id;
    }
    catch
    {
        await transaction.RollbackAsync(cancellationToken);
        throw;
    }
}
```

**Key Points**: 
- Order and OutboxMessage saved in single transaction (atomicity)
- SaveChanges called twice: first to generate ID, second for outbox
- Both commits are atomic

#### **SubmitOrderAsync** - Processes order submission
```csharp
public async Task SubmitOrderAsync(OrderCreatedEvent orderCreatedEvent, CancellationToken cancellationToken)
{
    // Step 1: Validate CorrelationId
    var messageId = orderCreatedEvent.CorrelationId;
    if (string.IsNullOrWhiteSpace(messageId) || !Guid.TryParse(messageId, out _))
    {
        throw new ArgumentException("CorrelationId must be a valid GUID", nameof(orderCreatedEvent));
    }
    
    var entityId = orderCreatedEvent.OrderId;
    
    // Step 2: Load order with items (no transaction)
    var order = await _db.Orders
        .Include(o => o.Items)
        .FirstOrDefaultAsync(o => o.Id == orderCreatedEvent.OrderId, cancellationToken);
    
    if (order == null || order.Status == OrderStatuses.Submitted)
        return; // Early exit
    
    // Step 3: Apply business logic OUTSIDE transaction
    await SimulateBusinessLogicAsync(order, cancellationToken);
    
    // Step 4: Prepare item IDs for locking
    var itemIds = order.Items.Select(x => x.ItemId).OrderBy(id => id).ToArray();
    
    // Step 5: BEGIN TRANSACTION
    using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
    try
    {
        // Idempotency check with FOR UPDATE lock
        var alreadyProcessed = await _db.Database
            .SqlQueryRaw<int>(ProcessedMessageLockQuery, entityId, messageId)
            .AnyAsync(cancellationToken);
        
        if (alreadyProcessed)
        {
            await tx.RollbackAsync(cancellationToken);
            return;
        }
        
        // Lock inventory rows with FOR UPDATE
        var inventories = await _db.InventoryItems
            .FromSqlRaw(InventoryLockQuery, itemIds)
            .ToListAsync(cancellationToken);
        
        // Validate all items exist
        var missingItems = itemIds.Except(inventories.Select(i => i.ItemId)).ToList();
        if (missingItems.Count > 0)
        {
            await tx.RollbackAsync(cancellationToken);
            return;
        }
        
        // Check and update inventory
        var insufficientItems = new List<(long ItemId, int Available, int Required)>();
        
        foreach (var orderItem in order.Items)
        {
            var inv = inventories.Single(i => i.ItemId == orderItem.ItemId);
            
            if (inv.InStock - orderItem.Quantity < 0)
            {
                insufficientItems.Add((inv.ItemId, inv.InStock, orderItem.Quantity));
                continue;
            }
            
            inv.Reserved += orderItem.Quantity;
            inv.InStock -= orderItem.Quantity;
        }
        
        if (insufficientItems.Count > 0)
        {
            await tx.RollbackAsync(cancellationToken);
            return;
        }
        
        // Update order status
        order.Status = OrderStatuses.Submitted;
        
        // Add processed message
        _db.ProcessedMessages.Add(new ProcessedMessage
        {
            EntityId = entityId,
            MessageId = messageId,
            ProcessedAt = DateTime.UtcNow,
            MessageType = nameof(OrderCreatedEvent)
        });
        
        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        
        // Update metrics after commit
        _metrics.IncrementProcessedOrders();
    }
    catch (DbUpdateConcurrencyException ex)
    {
        await tx.RollbackAsync(cancellationToken);
        throw new InvalidOperationException("Concurrency conflict detected. Please retry.", ex);
    }
    catch (DbUpdateException ex) when (
        ex.InnerException?.Message?.Contains("EntityId") == true && 
        ex.InnerException?.Message?.Contains("CorrelationId") == true)
    {
        // Duplicate caught by unique constraint (idempotent)
        await tx.RollbackAsync(cancellationToken);
    }
    catch (Exception ex)
    {
        await tx.RollbackAsync(cancellationToken);
        throw;
    }
}

private static async Task SimulateBusinessLogicAsync(Order order, CancellationToken cancellationToken)
{
    // Simulate external API call (1 second delay)
    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
    
    // Calculate discount: 5% off if total quantity > 3
    var totalQuantity = order.Items?.Sum(oi => oi.Quantity) ?? 0;
    if (totalQuantity > 3)
    {
        var discount = order.TotalAmount * 0.05m;
        order.TotalAmount -= discount;
    }
}
```

**Key Implementation Points**:
- Business logic (discount calculation, delays) executed OUTSIDE transaction
- Idempotency check with FOR UPDATE lock INSIDE transaction
- Inventory locks in sorted order (prevents deadlocks)
- Three catch blocks: concurrency, unique constraint (idempotent), general

### OutboxProcessorService - Background Service

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    while (!stoppingToken.IsCancellationRequested)
    {
        try
        {
            await ProcessOutboxMessagesAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            break;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing outbox messages");
        }
        
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
    }
}

private async Task ProcessOutboxMessagesAsync(CancellationToken cancellationToken)
{
    using var scope = _serviceProvider.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var publisher = scope.ServiceProvider.GetRequiredService<IOrderMessagePublisher>();
    
    var pendingMessages = await dbContext.OutboxMessages
        .Where(m => m.ProcessedAt == null && m.RetryCount < 5)
        .OrderBy(m => m.CreatedAt)
        .Take(10)
        .ToListAsync(cancellationToken);
    
    foreach (var message in pendingMessages)
    {
        try
        {
            // Deserialize and publish based on Type
            switch (message.Type)
            {
                case nameof(OrderCreatedEvent):
                    var event = JsonSerializer.Deserialize<OrderCreatedEvent>(message.Payload);
                    await publisher.PublishAsync(event, cancellationToken);
                    break;
            }
            
            message.ProcessedAt = DateTime.UtcNow;
            message.Error = null;
            message.RetryCount = 0;
        }
        catch (JsonException ex)
        {
            // Permanent error - bad data
            message.RetryCount = 5; // Max retries
            message.Error = $"Invalid JSON payload: {ex.Message}";
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Message broker is currently unavailable"))
        {
            // Transient error - broker down
            message.RetryCount++;
            message.Error = $"Broker unavailable: {ex.InnerException?.Message ?? ex.Message}";
        }
        catch (Exception ex)
        {
            // Unknown error - retry
            message.RetryCount++;
            message.Error = $"{ex.GetType().Name}: {ex.Message}";
        }
    }
    
    await dbContext.SaveChangesAsync(cancellationToken);
}
```

**Constants**:
- `ProcessingIntervalSeconds`: 5
- `BatchSize`: 10
- `MaxRetryCount`: 5

### OrderMessagePublisher

```csharp
public async ValueTask PublishAsync(OrderCreatedEvent message, CancellationToken cancellationToken)
{
    ArgumentNullException.ThrowIfNull(message);
    
    if (string.IsNullOrWhiteSpace(message.CorrelationId))
    {
        message.CorrelationId = Guid.NewGuid().ToString();
    }
    
    IChannel? channel = null;
    try
    {
        channel = await _channelPool.AcquireAsync(cancellationToken);
        
        if (!channel.IsOpen)
        {
            throw new InvalidOperationException("Channel is closed");
        }
        
        await channel.QueueDeclareAsync(
            queue: _rabbitSettings.QueueName,
            durable: _rabbitSettings.Durable,
            exclusive: false,
            autoDelete: _rabbitSettings.AutoDelete,
            arguments: null,
            cancellationToken: cancellationToken);
        
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
        var properties = new BasicProperties
        {
            Persistent = _rabbitSettings.Durable,
            ContentType = MediaTypeNames.Application.Json,
            CorrelationId = message.CorrelationId,
            MessageId = Guid.NewGuid().ToString(),
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        };
        
        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: _rabbitSettings.QueueName,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);
    }
    catch (Exception ex)
    {
        throw new InvalidOperationException("Failed to publish message to broker", ex);
    }
    finally
    {
        if (channel != null)
        {
            await _channelPool.ReleaseAsync(channel);
        }
    }
}
```

**Key Features**:
- Channel pool for resource efficiency
- Validates channel is open
- Uses MediaTypeNames.Application.Json constant
- Sets message properties (Persistent, CorrelationId, MessageId, Timestamp)
- Always releases channel in finally block

### OrderMessageConsumer - Background Service

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    await _consumerConnection.InitializeAsync("Consumer", stoppingToken);
    var rabbitConnection = _consumerConnection.Connection;
    
    _channel = await rabbitConnection.CreateChannelAsync(cancellationToken: stoppingToken);
    
    await _channel.QueueDeclareAsync(
        queue: _rabbitSettings.QueueName,
        durable: _rabbitSettings.Durable,
        exclusive: false,
        autoDelete: _rabbitSettings.AutoDelete,
        arguments: null,
        cancellationToken: stoppingToken);
    
    await _channel.BasicQosAsync(
        prefetchSize: _rabbitSettings.PrefetchSize,
        prefetchCount: _rabbitSettings.PrefetchCount,
        global: false,
        cancellationToken: stoppingToken);
    
    var consumer = new AsyncEventingBasicConsumer(_channel);
    consumer.ReceivedAsync += (sender, ea) => HandleMessageAsync(sender, ea, stoppingToken);
    
    await _channel.BasicConsumeAsync(
        queue: _rabbitSettings.QueueName,
        autoAck: false,
        consumer: consumer,
        cancellationToken: stoppingToken);
    
    await Task.Delay(Timeout.Infinite, stoppingToken);
}

private async Task HandleMessageAsync(object sender, BasicDeliverEventArgs args, CancellationToken cancellationToken)
{
    var body = args.Body.ToArray();
    var messageJson = Encoding.UTF8.GetString(body);
    var message = JsonSerializer.Deserialize<OrderCreatedEvent>(messageJson);
    
    if (message == null || string.IsNullOrWhiteSpace(message.CorrelationId) || !Guid.TryParse(message.CorrelationId, out _))
    {
        await AcknowledgeMessageAsync(args.DeliveryTag, false, false, cancellationToken);
        return;
    }
    
    try
    {
        using var scope = _services.CreateScope();
        var orderService = scope.ServiceProvider.GetRequiredService<IOrderService>();
        await orderService.SubmitOrderAsync(message, cancellationToken);
        
        await AcknowledgeMessageAsync(args.DeliveryTag, true, false, cancellationToken);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to process message from RabbitMQ");
        await AcknowledgeMessageAsync(args.DeliveryTag, false, true, cancellationToken);
    }
}

private async Task AcknowledgeMessageAsync(ulong deliveryTag, bool ack, bool requeue, CancellationToken cancellationToken)
{
    await _channelLock.WaitAsync(cancellationToken);
    try
    {
        if (ack)
            await _channel.BasicAckAsync(deliveryTag, multiple: false, cancellationToken);
        else
            await _channel.BasicNackAsync(deliveryTag, multiple: false, requeue, cancellationToken);
    }
    finally
    {
        _channelLock.Release();
    }
}
```

**Key Features**:
- Dedicated consumer connection
- Manual acknowledgment (autoAck: false)
- SemaphoreSlim for thread-safe acknowledgments
- QoS: prefetchCount = 1
- Invalid messages: Nack without requeue
- Processing errors: Nack with requeue
- Graceful shutdown in StopAsync

### RabbitMQ Infrastructure

#### **RabbitConnectionFactory**
```csharp
public async Task<IConnection> GetConnectionAsync(string clientProvidedName, CancellationToken cancellationToken)
{
    const int maxRetries = 10;
    const int delayMilliseconds = 2000;
    
    for (int i = 0; i < maxRetries; i++)
    {
        try
        {
            return await CreateConnectionAsync(clientProvidedName, cancellationToken);
        }
        catch (Exception ex) when (i < maxRetries - 1)
        {
            logger.LogWarning("Failed to connect to RabbitMQ (attempt {Attempt}/{MaxRetries})", i + 1, maxRetries);
            await Task.Delay(delayMilliseconds, cancellationToken);
        }
    }
    
    throw new InvalidOperationException("Failed to connect to RabbitMQ after all retries");
}

private async Task<IConnection> CreateConnectionAsync(string? clientProvidedName, CancellationToken cancellationToken)
{
    var factory = new ConnectionFactory
    {
        Uri = new Uri(_rabbitConfig.ConnectionString),
        AutomaticRecoveryEnabled = true,
        TopologyRecoveryEnabled = true,
        NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
        RequestedHeartbeat = TimeSpan.FromSeconds(60),
        ClientProvidedName = clientProvidedName ?? string.Empty
    };
    
    var connection = await factory.CreateConnectionAsync(cancellationToken);
    
    // Event handlers for logging
    connection.ConnectionShutdownAsync += async (sender, args) => { /* log */ };
    connection.ConnectionRecoveryErrorAsync += async (sender, args) => { /* log */ };
    
    return connection;
}
```

**Constants**:
- `NetworkRecoveryIntervalSeconds`: 10
- `HeartbeatIntervalSeconds`: 60
- Max retries: 10 with 2-second delay

#### **RabbitChannelPool**
```csharp
public async ValueTask<IChannel> AcquireAsync(CancellationToken cancellationToken = default)
{
    ObjectDisposedException.ThrowIf(_disposed, this);
    
    if (!_connection.IsOpen)
        throw new InvalidOperationException("RabbitMQ connection is closed");
    
    await _semaphore.WaitAsync(cancellationToken);
    
    // Try to get existing open channel
    while (_channels.TryTake(out var channel))
    {
        if (channel.IsOpen)
            return channel;
        
        // Dispose closed channel
        await channel.DisposeAsync();
        Interlocked.Decrement(ref _currentSize);
    }
    
    // Create new channel
    try
    {
        var newChannel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);
        Interlocked.Increment(ref _currentSize);
        return newChannel;
    }
    catch
    {
        _semaphore.Release();
        throw;
    }
}

public ValueTask ReleaseAsync(IChannel? channel)
{
    ObjectDisposedException.ThrowIf(_disposed, this);
    
    if (channel == null)
    {
        _semaphore.Release();
        return ValueTask.CompletedTask;
    }
    
    if (channel.IsOpen && _connection.IsOpen)
    {
        _channels.Add(channel);
    }
    else
    {
        channel.Dispose();
        Interlocked.Decrement(ref _currentSize);
    }
    
    _semaphore.Release();
    return ValueTask.CompletedTask;
}
```

**Features**:
- Thread-safe with SemaphoreSlim and ConcurrentBag
- Max 10 channels (DefaultMaxSize constant)
- Auto-cleanup of closed channels
- IAsyncDisposable for graceful shutdown

#### **ConsumerConnection** (implements IRabbitConnection, IProducerConnection, IConsumerConnection)
```csharp
public class ConsumerConnection(IRabbitMQConnectionFactory factory) : IProducerConnection, IConsumerConnection
{
    private IConnection? _connection;
    
    public IConnection Connection => _connection ?? throw new InvalidOperationException("Connection not initialized");
    
    public async Task InitializeAsync(string clientProvidedName, CancellationToken cancellationToken)
    {
        if (_connection != null)
            return;
        
        _connection = await factory.GetConnectionAsync(clientProvidedName, cancellationToken);
    }
    
    public async ValueTask DisposeAsync()
    {
        if (_connection?.IsOpen == true)
            await _connection.CloseAsync();
        
        if (_connection != null)
            await _connection.DisposeAsync();
        
        GC.SuppressFinalize(this);
    }
}
```

**Interfaces**:
- `IRabbitConnection`: Base interface
- `IProducerConnection`: Marker interface for producer
- `IConsumerConnection`: Marker interface for consumer

**Pattern**: Lazy initialization, single instance per interface

### ApplicationMetrics

```csharp
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
```

**Features**:
- Uses .NET 8 System.Diagnostics.Metrics API
- IMeterFactory dependency injection
- Thread-safe with Interlocked operations
- Meter name: "Travel.WebApi", version: "1.0.0"

## API Layer

### OrderEndpoints (Minimal API)

```csharp
public sealed class OrderEndpoints
{
    public static void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPost("/orders", async (
            CreateOrderRequest request,
            IOrderService orderService,
            IMapper mapper,
            CancellationToken cancellationToken) =>
        {
            var domainModel = mapper.ToDomainModel(request);
            var orderId = await orderService.CreateOrderAsync(domainModel, cancellationToken);
            
            return Results.Ok(new { orderId });
        })
        .WithName("SubmitOrder")
        .WithOpenApi()
        .Produces<object>(StatusCodes.Status200OK)
        .ProducesValidationProblem()
        .AddEndpointFilter<ValidationFilter>();
    }
}
```

### CreateOrderRequest DTOs

```csharp
public class CreateOrderRequest
{
    public long CustomerId { get; set; }
    public List<OrderItemRequest> Items { get; set; } = [];
}

public class OrderItemRequest
{
    public long Id { get; set; }
    public decimal Price { get; set; }
    public int Quantity { get; set; }
}
```

### Mapper

```csharp
public class Mapper : IMapper
{
    public Order ToDomainModel(CreateOrderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        
        var entity = new Order
        {
            CustomerId = request.CustomerId,
            OrderDate = DateTime.UtcNow,
            Status = OrderStatuses.New
        };
        
        if (request.Items is not null)
        {
            foreach (var item in request.Items)
            {
                entity.Items.Add(ToDomainModel(item));
            }
        }
        
        entity.TotalAmount = entity.Items.Sum(i => i.Price * i.Quantity);
        
        return entity;
    }
    
    public OrderItem ToDomainModel(OrderItemRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        
        return new OrderItem
        {
            ItemId = request.Id,
            Price = request.Price,
            Quantity = request.Quantity
        };
    }
}
```

### Validation (FluentValidation)

```csharp
public class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderRequestValidator()
    {
        RuleFor(x => x.CustomerId)
            .GreaterThan(0)
            .WithMessage("CustomerId must be greater than 0");
        
        RuleFor(x => x.Items)
            .NotNull()
            .WithMessage("Items cannot be null")
            .NotEmpty()
            .WithMessage("Order must contain at least one item");
        
        RuleForEach(x => x.Items)
            .SetValidator(new OrderItemRequestValidator());
    }
}

public class OrderItemRequestValidator : AbstractValidator<OrderItemRequest>
{
    public OrderItemRequestValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithMessage("Item Id must be greater than 0");
        
        RuleFor(x => x.Quantity)
            .GreaterThan(0)
            .WithMessage("Quantity must be greater than 0");
        
        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Price cannot be negative");
    }
}
```

### ValidationFilter (IEndpointFilter)

```csharp
public class ValidationFilter(IServiceProvider serviceProvider) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        foreach (var argument in context.Arguments)
        {
            if (argument is null)
                continue;
            
            var argumentType = argument.GetType();
            var validatorType = typeof(IValidator<>).MakeGenericType(argumentType);
            
            if (serviceProvider.GetService(validatorType) is IValidator validator)
            {
                var validationContext = new ValidationContext<object>(argument);
                var validationResult = await validator.ValidateAsync(validationContext);
                
                if (!validationResult.IsValid)
                {
                    var errors = validationResult.Errors
                        .GroupBy(e => e.PropertyName)
                        .ToDictionary(
                            g => g.Key,
                            g => g.Select(e => e.ErrorMessage).ToArray()
                        );
                    
                    return Results.ValidationProblem(errors);
                }
            }
        }
        
        return await next(context);
    }
}
```

### InfrastructureEndpoints

```csharp
public sealed class InfrastructureEndpoints
{
    public static void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            Predicate = check => check.Name == WebApiConstants.HealthChecks.RabbitMq 
                || check.Name == WebApiConstants.HealthChecks.Npgsql,
            ResponseWriter = async (context, report) =>
            {
                context.Response.ContentType = MediaTypeNames.Application.Json;
                
                var result = new
                {
                    status = report.Status.ToString(),
                    checks = report.Entries.Select(e => new
                    {
                        e.Key,
                        e.Value.Status,
                        e.Value.Description
                    })
                };
                await context.Response.WriteAsync(JsonSerializer.Serialize(result));
            }
        });
    }
}
```

## Configuration & Extension Methods

### RabbitConfig

```csharp
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
```

### ApplicationServicesCollectionExtensions

```csharp
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
```

### RabbitServiceCollectionExtensions

```csharp
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
```

### DatabaseServiceExtensions

```csharp
public static IServiceCollection AddDatabaseServices<T>(
    this IServiceCollection services, 
    string? connectionString) where T : DbContext
{
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
    }
    
    services.AddDbContext<T>(options => options.UseNpgsql(connectionString));
    
    return services;
}
```

### ServiceCollectionExtensions

```csharp
public static T AddConfigurationSection<T>(
    this IServiceCollection services,
    IConfigurationManager configurationManager,
    string sectionName) where T : class, new()
{
    var configSection = configurationManager.GetSection(sectionName);
    services.Configure<T>(configSection);
    var config = configSection.Get<T>() ?? new T();
    return config;
}

public static IServiceCollection AddValidation(this IServiceCollection services)
{
    services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
    return services;
}
```

### SwaggerServiceExtensions

```csharp
public static class SwaggerServiceExtensions
{
    private const string ApiVersion = "v1";
    private const string ApiTitle = "Travel API";
    private const string ApiTitleWithVersion = "Travel API V1";
    
    public static IServiceCollection AddSwaggerServices(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc(ApiVersion, new OpenApiInfo 
            { 
                Title = ApiTitle, 
                Version = ApiVersion 
            });
        });
        
        return services;
    }
    
    public static IApplicationBuilder UseSwaggerConfiguration(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", ApiTitleWithVersion);
                c.RoutePrefix = string.Empty; // Swagger at root
            });
        }
        
        return app;
    }
}
```

### MigrationExtensions

```csharp
public static void RunDbInitializer<T>(this IServiceProvider serviceProvider)
    where T : DbContext, new()
{
    using var scope = serviceProvider.CreateScope();
    var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
    var logger = loggerFactory.CreateLogger("DbMigration");
    
    var dbContext = scope.ServiceProvider.GetRequiredService<T>();
    var pendingMigrations = dbContext.Database.GetPendingMigrations();
    
    if (pendingMigrations.Any())
    {
        dbContext.Database.Migrate();
    }
}
```

### EndpointExtensions

```csharp
public static class EndpointExtensions
{
    public static IEndpointRouteBuilder MapEndpoints(this IEndpointRouteBuilder app)
    {
        InfrastructureEndpoints.MapEndpoints(app);
        OrderEndpoints.MapEndpoints(app);
        
        return app;
    }
}
```

## Constants

### OrderService Constants
```csharp
private const string InventoryLockQuery = @"
    SELECT * FROM ""Inventory"" 
    WHERE ""ItemId"" = ANY({0})
    ORDER BY ""ItemId""
    FOR UPDATE";

private const string ProcessedMessageLockQuery = @"
    SELECT 1 FROM ""ProcessedMessages"" 
    WHERE ""EntityId"" = {0} AND ""MessageId"" = {1}
    FOR UPDATE";

private const int ProcessingDelaySeconds = 1;
private const int DiscountQuantityThreshold = 3;
private const decimal DiscountPercentage = 0.05m;
private const int MinimumInventory = 0;
```

### ApplicationConstants
```csharp
public static class ApplicationConstants
{
    public static class Messages
    {
        public const string CorrelationId = "CorrelationId";
        public const string EntityId = "EntityId";
    }
}
```

### WebApiConstants
```csharp
public static class WebApiConstants
{
    public static class HealthChecks
    {
        public const string RabbitMq = "rabbitmq";
        public const string Npgsql = "npgsql";
    }
}
```

## Program.cs - Service Registration & Middleware

```csharp
var builder = WebApplication.CreateBuilder(args);

// Database
var dbConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDatabaseServices<AppDbContext>(dbConnectionString);

// RabbitMQ
var rabbitConfig = builder.Services.AddConfigurationSection<RabbitConfig>(
    builder.Configuration, 
    RabbitConfig.SectionName);
builder.Services.AddRabbitMqServices(rabbitConfig);

// Application Services
builder.Services.AddLogicServices();
builder.Services.AddApplicationMetrics();

// Infrastructure
builder.Services.AddSwaggerServices();
builder.Services.AddValidation();
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString: dbConnectionString!, name: WebApiConstants.HealthChecks.Npgsql)
    .AddRabbitMQ(rabbitConnectionString: rabbitConfig.ConnectionString, name: WebApiConstants.HealthChecks.RabbitMq);

var app = builder.Build();

// Middleware
app.UseSwaggerConfiguration();
app.UseHttpsRedirection();
app.MapEndpoints();

// Auto-apply migrations
MigrationExtensions.RunDbInitializer<AppDbContext>(app.Services);

app.Run();
```

**Order of Operations**:
1. Database configuration
2. RabbitMQ configuration (reads and returns config)
3. Application services
4. Infrastructure (Swagger, Validation, Health Checks)
5. Middleware pipeline
6. Endpoint mapping
7. Migration application
8. Run

## Database Configuration

### AppDbContext

```csharp
public class AppDbContext : DbContext
{
    public DbSet<Order> Orders { get; set; } = null!;
    public DbSet<Item> Items { get; set; } = null!;
    public DbSet<OrderItem> OrderItems { get; set; } = null!;
    public DbSet<Inventory> InventoryItems { get; set; } = null!;
    public DbSet<OutboxMessage> OutboxMessages { get; set; } = null!;
    public DbSet<ProcessedMessage> ProcessedMessages { get; set; } = null!;
    
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
```

### Entity Configurations

All configurations use `IEntityTypeConfiguration<T>` pattern and are auto-discovered via `ApplyConfigurationsFromAssembly`.

**Key configurations**:
- **OrderConfiguration**: Cascade delete on Items
- **OrderItemConfiguration**: Composite key (OrderId, ItemId)
- **ItemConfiguration**: Restrict delete on OrderItems
- **InventoryConfiguration**: One-to-one with Item, Ignore Available property
- **OutboxMessageConfiguration**: Filtered index for efficient querying
- **ProcessedMessageConfiguration**: Unique index on (MessageId, EntityId)

### Seed Data (Migration)

Pre-populate Items table with 10 travel-related items:
1. Standard Seat - $49.99
2. Extra Legroom - $79.99
3. Window Seat - $59.99
4. Flight Meal - $12.50
5. Priority Boarding - $25.00
6. Checked Baggage - $35.00
7. Travel Insurance - $19.99
8. Lounge Access - $29.99
9. Seat Selection - $9.99
10. In-flight Wifi - $7.99

Pre-populate Inventory table for all 10 items:
- InStock: 100
- Reserved: 20
- InTransit: 0

## Configuration Files

### appsettings.json
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=traveldb;Username=postgres;Password=Your_strong_password"
  },
  "RabbitMQ": {
    "ConnectionString": "amqp://guest:guest@localhost:5672/",
    "QueueName": "order-processing-queue",
    "Durable": true,
    "AutoDelete": false,
    "PrefetchCount": 1,
    "PrefetchSize": 0,
    "ChannelPoolSize": 10,
    "MaxChannels": 50
  }
}
```

### docker-compose.yml
```yaml
version: '3.8'

services:
  webapi:
    build:
      context: .
      dockerfile: Travel.WebApi/Dockerfile
    image: travel.webapi:local
    ports:
      - "8080:8080"
    environment:
      - ASPNETCORE_URLS=http://+:8080
      - DOTNET_ENVIRONMENT=Development
      - ConnectionStrings__DefaultConnection=Host=postgres;Port=5432;Database=traveldb;Username=postgres;Password=Your_strong_password
      - RabbitMQ__ConnectionString=amqp://guest:guest@rabbitmq:5672/
    depends_on:
      - rabbitmq
      - postgres

  rabbitmq:
    image: rabbitmq:3-management
    ports:
      - "15672:15672" # management UI
      - "5672:5672"   # AMQP
    environment:
      RABBITMQ_DEFAULT_USER: guest
      RABBITMQ_DEFAULT_PASS: guest

  postgres:
    image: postgres:15
    restart: always
    environment:
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: Your_strong_password
      POSTGRES_DB: traveldb
    ports:
      - "5432:5432"
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres -d traveldb"]
      interval: 10s
      timeout: 10s
      retries: 10
    volumes:
      - postgres-data:/var/lib/postgresql/data

volumes:
  postgres-data:
```

### Dockerfile (Multi-stage)
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
ARG APP_UID=0
USER ${APP_UID}
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Travel.WebApi/Travel.WebApi.csproj", "Travel.WebApi/"]
RUN dotnet restore "Travel.WebApi/Travel.WebApi.csproj"
COPY . .
WORKDIR "/src/Travel.WebApi"
RUN dotnet build "Travel.WebApi.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Travel.WebApi.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Travel.WebApi.dll"]
```

## Key Implementation Patterns

### 1. Transactional Outbox Pattern
**Problem**: Ensure order creation and event publishing are atomic
**Solution**: 
- Store order and outbox message in single transaction
- Background service polls outbox and publishes to RabbitMQ
- Guarantees at-least-once delivery
- Prevents lost events due to RabbitMQ downtime

**Flow**:
```
POST /orders ? Save Order ? Save OutboxMessage ? Commit
           ?
OutboxProcessor ? Poll ? Publish ? Mark Processed
           ?
RabbitMQ ? Consumer ? SubmitOrder
```

### 2. Idempotency Pattern
**Problem**: Handle duplicate message processing in distributed system
**Solution**:
- ProcessedMessages table with unique constraint on (MessageId, EntityId)
- Check existence with FOR UPDATE lock inside transaction
- Catch unique constraint violations and treat as success
- GUID validation for all message IDs

**Benefits**:
- Exactly-once semantics
- Safe message replay
- No duplicate processing

### 3. Pessimistic Locking
**Problem**: Concurrent inventory updates causing overselling
**Solution**:
- Use PostgreSQL FOR UPDATE locks
- Lock inventory rows in deterministic order (sorted by ItemId)
- Hold locks only during critical section (inventory update)
- Execute business logic OUTSIDE transaction

**Benefits**:
- Prevents race conditions
- Prevents deadlocks (sorted order)
- No overselling
- Minimal lock duration

### 4. Channel Pooling
**Problem**: Creating RabbitMQ channels is expensive
**Solution**:
- Maintain pool of reusable channels (max 10)
- Thread-safe acquire/release with SemaphoreSlim and ConcurrentBag
- Dispose closed channels automatically
- Validate channel state before use

**Benefits**:
- Reduced connection overhead
- Better resource utilization
- Improved throughput

### 5. Separation of Concerns
**Principle**: Keep transaction scope minimal
**Implementation**:
- Business logic (discount calculation, delays) executed OUTSIDE transaction
- Only critical section (inventory updates, status changes) inside transaction
- Reduces lock duration
- Improves concurrency

## Coding Standards (from .github/copilot-instructions.md)

1. **Use C# 12 features**: Primary constructors, collection expressions `[]`
2. **Prefer immutability**: Readonly fields, init-only properties where possible
3. **Follow Clean Architecture**: Domain ? Application ? Infrastructure ? WebApi layers
4. **Avoid static mutable state**: Use dependency injection (exception: ApplicationMetrics for demo)
5. **All async methods**:
   - Must accept CancellationToken parameter
   - Must have "Async" suffix
6. **Constants**: Extract hardcoded values to constants in same class or shared Constants class
   - Exception: string.Empty, true, false are not constants
   - Don't move endpoints to constants
   - Use MediaTypeNames.Application.* constants for content types
7. **No comments unless necessary**: Code should be self-explanatory

## Critical SQL Queries

### Inventory Lock Query
```sql
SELECT * FROM "Inventory" 
WHERE "ItemId" = ANY({0})
ORDER BY "ItemId"
FOR UPDATE
```
- Locks multiple rows atomically
- Sorted order prevents deadlocks
- PostgreSQL-specific syntax

### Processed Message Lock Query
```sql
SELECT 1 FROM "ProcessedMessages" 
WHERE "EntityId" = {0} AND "MessageId" = {1}
FOR UPDATE
```
- Locks specific message record
- Prevents race condition between concurrent consumers
- Part of idempotency check

## API Request Flow

### Create Order Flow
1. **POST /orders** ? ValidationFilter validates CreateOrderRequest
2. Mapper converts request to Order domain entity
3. OrderService.CreateOrderAsync:
   - Transaction starts
   - Order saved (ID generated)
   - OrderCreatedEvent created with order.Id and new CorrelationId (GUID)
   - OutboxMessage created with serialized event
   - Transaction commits atomically
   - Return orderId
4. **OutboxProcessorService** (background):
   - Polls OutboxMessages every 5 seconds
   - Finds unprocessed messages (ProcessedAt IS NULL, RetryCount < 5)
   - Deserializes OrderCreatedEvent from JSON payload
   - Calls OrderMessagePublisher.PublishAsync
   - Publishes to RabbitMQ queue
   - Marks message as processed or increments retry count
5. **OrderMessageConsumer** (background):
   - Consumes from RabbitMQ queue (prefetchCount = 1)
   - Deserializes OrderCreatedEvent
   - Validates CorrelationId (must be valid GUID)
   - Creates scoped IOrderService
   - Calls SubmitOrderAsync
   - BasicAck on success, BasicNack on error

### Submit Order Flow (in SubmitOrderAsync)
1. **Validate CorrelationId** format (must be valid GUID)
2. **Load order** with Items, return if not found or already Submitted
3. **Apply business logic** OUTSIDE transaction (no DB locks held):
   - Simulate external API call (1 second delay)
   - Calculate discount: If total quantity > 3, apply 5% discount to TotalAmount
4. **Prepare item IDs** for inventory locking (sorted array)
5. **BEGIN TRANSACTION** (critical section):
   - **Idempotency check with FOR UPDATE lock**:
     - Query ProcessedMessages with FOR UPDATE
     - If already processed, rollback and return (idempotent)
   - **Lock inventory rows**: FOR UPDATE in sorted order
   - **Validate all items exist**: If missing items, log error, rollback, return
   - **Check inventory availability**:
     - For each order item, check if `InStock - Quantity >= 0`
     - Collect insufficient items in list
     - If any insufficient, log warning, rollback, return
   - **Update inventory**:
     - `Reserved += Quantity`
     - `InStock -= Quantity`
   - **Update order status** to Submitted
   - **Add ProcessedMessage** record (EntityId, MessageId, ProcessedAt, MessageType)
   - **Save changes**
   - **Commit transaction**
6. **After successful commit**:
   - Increment metrics counter
   - Log success with processed orders count
7. **Error Handling**:
   - **DbUpdateConcurrencyException**: Log warning, rollback, throw InvalidOperationException
   - **DbUpdateException with unique constraint** (EntityId + MessageId): Log as idempotent success, rollback silently
   - **Generic Exception**: Log error, rollback, re-throw

## Error Handling Strategy

### OrderService.SubmitOrderAsync
- **ArgumentException**: Invalid CorrelationId (not GUID)
- **DbUpdateConcurrencyException**: Rollback, throw InvalidOperationException("Concurrency conflict detected. Please retry.")
- **DbUpdateException (unique constraint on MessageId+EntityId)**: Rollback silently (idempotent success)
- **Generic Exception**: Rollback, re-throw

### OutboxProcessorService
- **JsonException**: Mark as permanent failure (RetryCount = 5)
- **InvalidOperationException (broker unavailable)**: Increment retry (transient error)
- **Other exceptions**: Increment retry, log with retry count
- Messages reaching max retries stay in outbox with error message

### OrderMessageConsumer
- **Null message**: BasicNack(requeue=false)
- **Invalid CorrelationId**: BasicNack(requeue=false)
- **Processing exception**: BasicNack(requeue=true)
- **Success**: BasicAck

## NuGet Packages (Travel.WebApi.csproj)
```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UserSecretsId>caae0b6c-2ab1-4652-8906-77b2abfab6aa</UserSecretsId>
    <DockerDefaultTargetOS>Linux</DockerDefaultTargetOS>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="AspNetCore.HealthChecks.RabbitMQ" Version="8.0.2" />
    <PackageReference Include="AspNetCore.HealthChecks.NpgSql" Version="8.0.2" />
    <PackageReference Include="FluentValidation.DependencyInjectionExtensions" Version="12.1.1" />
    <PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="8.0.23" />
    <PackageReference Include="Microsoft.VisualStudio.Azure.Containers.Tools.Targets" Version="1.22.1" />
    <PackageReference Include="Microsoft.EntityFrameworkCore" Version="8.0.23" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="8.0.23" PrivateAssets="all" />
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.0.11" />
    <PackageReference Include="RabbitMQ.Client" Version="7.2.0" />
    <PackageReference Include="Swashbuckle.AspNetCore.Swagger" Version="8.1.4" />
    <PackageReference Include="Swashbuckle.AspNetCore.SwaggerGen" Version="8.1.4" />
    <PackageReference Include="Swashbuckle.AspNetCore.SwaggerUI" Version="8.1.4" />
  </ItemGroup>
</Project>
```

## Logging Guidelines

- **Use structured logging** with placeholders: `{OrderId}`, `{CorrelationId}`, `{MessageId}`, etc.
- **Log levels**:
  - **Trace**: Channel pool operations (acquire/release)
  - **Debug**: Message flow (published, dequeued, reused channel)
  - **Information**: Successful operations, service start/stop, idempotent skips
  - **Warning**: Retry attempts, insufficient inventory, concurrency conflicts, connection issues
  - **Error**: Invalid data, missing records, unexpected exceptions, max retries reached

## Monitoring & Observability

### Health Checks (`/health`)
```json
{
  "status": "Healthy",
  "checks": [
    {
      "key": "npgsql",
      "status": "Healthy",
      "description": null
    },
    {
      "key": "rabbitmq",
      "status": "Healthy",
      "description": null
    }
  ]
}
```

### Metrics
- **System.Diagnostics.Metrics** API
- Counter: "orders.processed" (long)
- Unit: "{orders}"
- Meter: "Travel.WebApi", version "1.0.0"
- Thread-safe with Interlocked

## Design Trade-offs & Known Limitations

1. **Single service**: Order and Inventory in one service (should be separate microservices in production)
2. **Synchronous DI initialization**: RabbitChannelPool initializes connection with GetAwaiter().GetResult() (not ideal but needed for singleton)
3. **Simple retry logic**: Linear retry in RabbitConnectionFactory, should use Polly with exponential backoff
4. **No dead letter queue**: Max retry messages just stay in outbox with error
5. **No authentication/authorization**: For demo purposes only
6. **Minimal validation**: Production would need more comprehensive validation
7. **In-memory metrics**: ApplicationMetrics state lost on restart (should use persistent metrics store)
8. **Channel pool size hardcoded**: DefaultMaxSize = 10, should be configurable via RabbitConfig
9. **No saga pattern**: Transaction can be bottleneck under high load
10. **Migration on startup**: Better to use dedicated migration process in production
11. **No cleanup jobs**: Old ProcessedMessages and OutboxMessages accumulate (need scheduled cleanup)

## Expected Behavior

### 1. **Happy Path**
- POST /orders ? Returns orderId immediately
- OutboxProcessor picks up message ? Publishes to RabbitMQ (within 5 seconds)
- Consumer receives message ? Calls SubmitOrderAsync
- Inventory updated atomically (Reserved +, InStock -)
- Order status = Submitted
- Metrics incremented
- ProcessedMessage recorded

### 2. **Insufficient Inventory**
- Transaction rolls back
- Order remains in New status
- Logged as warning with insufficient items details
- No inventory changes

### 3. **Duplicate Message**
- ProcessedMessage check catches it (FOR UPDATE lock)
- Transaction rolls back
- Logged as informational (idempotent skip)
- No double processing

### 4. **RabbitMQ Down**
- Orders created successfully (outbox pattern)
- Outbox messages accumulate with retry count
- After 5 retries, messages remain in outbox with error
- When RabbitMQ recovers, processor resumes publishing

### 5. **Concurrent Orders for Same Item**
- FOR UPDATE ensures serialized access to inventory
- First transaction wins
- Subsequent transactions may fail if insufficient inventory
- No race conditions or overselling

### 6. **Unique Constraint Violation** (Race Condition)
- Two consumers process same message simultaneously
- First commits ProcessedMessage
- Second hits unique constraint on (MessageId, EntityId)
- DbUpdateException caught and treated as idempotent success

## Testing Scenarios

1. **Idempotency**: Replay same OrderCreatedEvent ? Only processed once
2. **Concurrency**: Multiple simultaneous orders for same item ? No over-reservation
3. **Invalid GUID**: Message with bad CorrelationId ? Nack without requeue
4. **RabbitMQ Failure**: Create orders while RabbitMQ down ? Events published when recovered
5. **Database Failure**: Consumer cannot connect ? Messages requeued and retried
6. **Discount Logic**: Order with 4 items ? 5% discount applied to TotalAmount
7. **Missing Items**: Order with non-existent ItemId ? Transaction rolled back
8. **Channel Pool**: Multiple concurrent publishes ? Channels reused efficiently
9. **Graceful Shutdown**: Stop application ? Connections closed cleanly

## Performance Considerations

1. **Pessimistic locking**: Serializes access to same inventory items (bottleneck under high contention)
2. **Business logic outside transaction**: Minimizes lock duration
3. **Channel pool**: Amortizes channel creation cost
4. **Batch processing**: Outbox processes 10 messages at once
5. **Single prefetch**: Consumer processes one message at a time (prevents overwhelming)
6. **Sorted locking**: Prevents deadlocks in multi-item orders
7. **Indexed queries**: Filtered index on OutboxMessages for efficient polling

## C# 12 & .NET 8 Features to Use

1. **Primary constructors** for all services and classes
2. **Collection expressions** `[]` instead of `new List<T>()`
3. **nameof()** for type-safe event type names
4. **ValueTask** for async performance (PublishAsync)
5. **IAsyncDisposable** for graceful resource cleanup
6. **ObjectDisposedException.ThrowIf()** for disposed checks
7. **ArgumentNullException.ThrowIfNull()** for null validation
8. **File-scoped namespaces** everywhere
9. **Global using directives** (ImplicitUsings enabled)
10. **Target-typed new** expressions where type is inferred
11. **Nullable reference types** enabled
12. **Pattern matching** in catch blocks with when clauses
13. **Expression-bodied members** for simple properties
14. **String interpolation** in structured logging

## Implementation Checklist

When generating this solution from scratch:

- [x] Create project structure with folder-based organization (not separate projects)
- [x] Install all NuGet packages with exact versions
- [x] Implement all domain entities with long IDs
- [x] Create all EF Core configurations (IEntityTypeConfiguration<T>)
- [x] Implement AppDbContext with ApplyConfigurationsFromAssembly
- [x] Create migrations: Init, SeedInit, AddOutboxMessages, AddProcessedMessages, ChangeIdsToLong
- [x] Implement RabbitMQ infrastructure (Factory, Pool, Connection with interfaces)
- [x] Implement OrderService with CreateOrderAsync (transactional outbox) and SubmitOrderAsync (pessimistic locking)
- [x] Implement OutboxProcessorService with polling, batch processing, and retry logic
- [x] Implement OrderMessagePublisher with channel pool pattern
- [x] Implement OrderMessageConsumer with dedicated channel and manual acknowledgment
- [x] Create Mapper with ArgumentNullException.ThrowIfNull validation
- [x] Implement FluentValidation validators for request DTOs
- [x] Create ValidationFilter as IEndpointFilter
- [x] Implement OrderEndpoints with Minimal API pattern
- [x] Implement InfrastructureEndpoints with custom health check JSON response
- [x] Create all service extension methods (Database, Rabbit, Application, Swagger, Validation)
- [x] Configure Program.cs with proper service registration order
- [x] Create appsettings.json with all configuration sections
- [x] Create Dockerfile with multi-stage build (base, build, publish, final)
- [x] Create docker-compose.yml with webapi, postgres, rabbitmq services
- [x] Implement ApplicationMetrics with IMeterFactory and System.Diagnostics.Metrics
- [x] Add all constants to respective classes (no magic numbers/strings)
- [x] Ensure all async methods have CancellationToken parameter
- [x] Add structured logging throughout with appropriate log levels
- [x] Implement graceful shutdown in all background services
- [x] Use MediaTypeNames.Application.Json for content types
- [x] Apply AutomaticRecoveryEnabled and TopologyRecoveryEnabled in RabbitMQ
- [x] Verify build succeeds
- [x] Test Docker build works
- [x] Test docker-compose up starts all services

## Expected Project Files

### Minimum Required Files (54 files)

**Domain Layer (7)**:
- Domain/Entities/Order.cs
- Domain/Entities/OrderItem.cs
- Domain/Entities/Item.cs
- Domain/Entities/Inventory.cs
- Domain/Entities/OutboxMessage.cs
- Domain/Entities/ProcessedMessage.cs
- Domain/Entities/OrderStatuses.cs
- Domain/Events/OrderCreatedEvent.cs

**Data Layer (14)**:
- Data/AppDbContext.cs
- Data/MigrationExtensions.cs
- Data/Configuration/DatabaseServiceExtensions.cs
- Data/Configurations/OrderConfiguration.cs
- Data/Configurations/OrderItemConfiguration.cs
- Data/Configurations/ItemConfiguration.cs
- Data/Configurations/InventoryConfiguration.cs
- Data/Configurations/OutboxMessageConfiguration.cs
- Data/Configurations/ProcessedMessageConfiguration.cs
- Data/Migrations/[timestamp]_Init.cs
- Data/Migrations/[timestamp]_SeedInit.cs
- Data/Migrations/[timestamp]_AddOutboxMessages.cs
- Data/Migrations/[timestamp]_AddProcessedMessages.cs
- Data/Migrations/[timestamp]_ChangeIdsToLong.cs

**Application Layer (6)**:
- Application/OrderService.cs
- Application/OrderMessageConsumer.cs
- Application/OrderMessagePublisher.cs
- Application/OutboxProcessorService.cs
- Application/ApplicationMetrics.cs
- Application/ApplicationConstants.cs
- Application/Configuration/ApplicationServicesCollectionExtensions.cs

**Infrastructure Layer (6)**:
- Infrastructure/Rabbit/RabbitConnectionFactory.cs
- Infrastructure/Rabbit/RabbitChannelPool.cs
- Infrastructure/Rabbit/ConsumerConnection.cs
- Infrastructure/Rabbit/Configuration/RabbitConfig.cs
- Infrastructure/Rabbit/Configuration/RabbitServiceCollectionExtensions.cs

**WebApi Layer (9)**:
- WebApi/Endpoints/Orders/OrderEndpoints.cs
- WebApi/Endpoints/Orders/Models/CreateOrderRequest.cs
- WebApi/Endpoints/Orders/Validators/CreateOrderRequestValidator.cs
- WebApi/Endpoints/Infrastructure/InfrastructureEndpoints.cs
- WebApi/Endpoints/EndpointExtensions.cs
- WebApi/Filters/ValidationFilter.cs
- WebApi/Mapping/Mapper.cs
- WebApi/Configuration/SwaggerServiceExtensions.cs
- WebApi/ServiceCollectionExtensions.cs
- WebApi/WebApiConstants.cs

**Root Files (6)**:
- Program.cs
- appsettings.json
- appsettings.Development.json
- Dockerfile
- docker-compose.yml
- Travel.WebApi.csproj

## How to Run

### Using Docker Compose (Recommended)
```bash
docker-compose up -d
```
Access Swagger UI at http://localhost:8080

### Using dotnet run
```bash
# Start PostgreSQL and RabbitMQ first
dotnet run --project Travel.WebApi/Travel.WebApi.csproj
```
Access Swagger UI at https://localhost:7113

### Accessing Services
- **Swagger UI**: http://localhost:8080 (Docker) or https://localhost:7113 (local)
- **Health Checks**: GET /health
- **RabbitMQ Management**: http://localhost:15672 (guest/guest)
- **PostgreSQL**: localhost:5432 (postgres/Your_strong_password)

## Success Criteria

? Order creation returns immediately (outbox pattern working)  
? Order processing via RabbitMQ is asynchronous  
? Duplicate messages are detected and skipped (idempotency)  
? Inventory is never over-reserved (pessimistic locking)  
? RabbitMQ downtime doesn't lose events (outbox persistence)  
? System recovers automatically from failures (automatic recovery)  
? Multiple services can process messages concurrently (scalable)  
? Health checks report accurate dependency status  
? Metrics track processed orders count  
? Docker Compose starts all services correctly  
? Migrations apply automatically on startup  
? Validation errors return proper problem details  
? All background services shut down gracefully  

---

**This prompt contains the complete specification to recreate the Travel.WebApi solution exactly as implemented, including all architectural patterns, configurations, implementation details, and the full codebase structure.**
