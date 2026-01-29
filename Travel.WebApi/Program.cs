using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using System.Text.Json;
using Travel.WebApi;
using Travel.WebApi.Data;
using Travel.WebApi.Models;
using Travel.WebApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Register DbContext - use SQL Server from configuration
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
}
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));

// Health checks using community packages
builder.Services.AddHealthChecks()
    //TODO: add rabbit healthcheck
    .AddSqlServer(connectionString, name: "sqlserver");

// Order processing services
builder.Services.AddSingleton<IOrderProcessingQueue, OrderProcessingQueue>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddSingleton<IMapper, Mapper>();
builder.Services.AddHostedService<OrderBackgroundService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

// Health endpoints
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = (check) => check.Name == "sqlserver" || check.Name == "rabbitmq",
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
    var orderId = await orderService.StartSubmitOrderAsync(request, cancellationToken);
    return Results.Ok(orderId);
}).WithName("SubmitOrder");

app.Run();