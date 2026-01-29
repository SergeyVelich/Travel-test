using Travel.WebApi.WebApi.Endpoints.Infrastructure;
using Travel.WebApi.WebApi.Endpoints.Orders;

namespace Travel.WebApi.WebApi.Endpoints;

public static class EndpointExtensions
{
    public static IEndpointRouteBuilder MapEndpoints(this IEndpointRouteBuilder app)
    {
        InfrastructureEndpoints.MapEndpoints(app);
        OrderEndpoints.MapEndpoints(app);
        
        return app;
    }
}