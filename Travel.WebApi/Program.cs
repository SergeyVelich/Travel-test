using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using System.Text.Json;
using Travel.WebApi;
using Travel.WebApi.Data;
using Travel.WebApi.Pubsub.Rabbit;
using Travel.WebApi.Services;
using Travel.WebApi.WebApi;
using Travel.WebApi.WebApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Db
var dbConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(dbConnectionString))
{
    throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
}
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(dbConnectionString));

// RabbitMQ
builder.Services.Configure<RabbitSettings>(builder.Configuration.GetSection(RabbitSettings.SectionName));
var rabbitMQConnectionString = builder.Configuration.GetValue<string>("RabbitMQ:ConnectionString");

builder.Services.AddSingleton<IRabbitMQConnectionFactory, RabbitConnectionFactory>();
builder.Services.AddSingleton(sp =>
{
    var connectionFactory = sp.GetRequiredService<IRabbitMQConnectionFactory>();
    return connectionFactory.GetConnection();
});

// Order processing services
builder.Services.AddSingleton<IOrderMessagePublisher, OrderMessagePublisher>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddSingleton<IMapper, Mapper>();
builder.Services.AddHostedService<OrderMessageConsumer>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Travel API", Version = "v1" });
});

builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString: dbConnectionString!, name: "npgsql")
    .AddRabbitMQ(rabbitConnectionString: rabbitMQConnectionString!, name: "rabbitmq");

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Travel API V1");
        c.RoutePrefix = string.Empty;
    });
}

app.UseHttpsRedirection();

// Health endpoints
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = (check) => check.Name == "rabbitmq" || check.Name == "npgsql",
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        
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

app.MapPost("/orders", async (CreateOrderRequest request, IOrderService orderService, CancellationToken cancellationToken) =>
{
    var orderId = await orderService.CreateOrderAsync(request, cancellationToken);
    return Results.Ok(orderId);
}).WithName("SubmitOrder");

app.MapGet("/metrics", () =>
{
    return Results.Text($"processed_orders_number {Metrics.ProcessedOrdersNumber}\n", "text/plain");
});

MigrationExtensions.RunDbInitializer<AppDbContext>(app.Services);

app.Run();