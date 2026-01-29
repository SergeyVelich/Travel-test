using Microsoft.EntityFrameworkCore;

namespace Travel.WebApi.Data.Configuration;

public static class DatabaseServiceExtensions
{
    public static IServiceCollection AddDatabaseServices<T>(this IServiceCollection services, string? connectionString)
        where T : DbContext
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
        }
        
        services.AddDbContext<T>(options => options.UseNpgsql(connectionString));
        
        return services;
    }
}