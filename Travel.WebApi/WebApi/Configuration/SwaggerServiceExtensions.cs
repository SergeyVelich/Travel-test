using Microsoft.OpenApi.Models;

namespace Travel.WebApi.WebApi.Configuration;

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
                c.RoutePrefix = string.Empty;
            });
        }

        return app;
    }
}
