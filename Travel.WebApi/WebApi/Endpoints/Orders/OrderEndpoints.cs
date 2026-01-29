using Travel.WebApi.Application;
using Travel.WebApi.WebApi.Endpoints.Orders.Models;
using Travel.WebApi.WebApi.Filters;
using Travel.WebApi.WebApi.Mapping;

namespace Travel.WebApi.WebApi.Endpoints.Orders;

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