using System.Net.Mime;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using System.Text.Json;

namespace Travel.WebApi.WebApi.Endpoints.Infrastructure;

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