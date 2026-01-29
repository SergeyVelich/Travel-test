using FluentValidation;
using System.Reflection;

namespace Travel.WebApi.WebApi;

public static class ServiceCollectionExtensions
{
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
}