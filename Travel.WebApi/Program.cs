using Microsoft.EntityFrameworkCore;
using Travel.WebApi.Application.Configuration;
using Travel.WebApi.Data;
using Travel.WebApi.Data.Configuration;
using Travel.WebApi.Infrastructure.Rabbit.Configuration;
using Travel.WebApi.WebApi;
using Travel.WebApi.WebApi.Configuration;
using Travel.WebApi.WebApi.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Db
var dbConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDatabaseServices<AppDbContext>(dbConnectionString);

// RabbitMQ
var rabbitConfig = builder.Services.AddConfigurationSection<RabbitConfig>(builder.Configuration, RabbitConfig.SectionName);
builder.Services.AddRabbitMqServices(rabbitConfig);

// Logic Services
builder.Services.AddLogicServices();
builder.Services.AddApplicationMetrics();

// Infrastructure
builder.Services.AddSwaggerServices();
builder.Services.AddValidation();
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString: dbConnectionString!, name: WebApiConstants.HealthChecks.Npgsql)
    .AddRabbitMQ(rabbitConnectionString: rabbitConfig.ConnectionString, name: WebApiConstants.HealthChecks.RabbitMq);

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseSwaggerConfiguration();

app.UseHttpsRedirection();

app.MapEndpoints();

MigrationExtensions.RunDbInitializer<AppDbContext>(app.Services);

app.Run();