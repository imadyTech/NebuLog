using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace NebuShop.Orders;

/// <summary>
/// The Minimal API half of the shop. Every route here has an exact counterpart in
/// <see cref="Controllers.OrdersController"/>; the behaviour is the same so that the console shows
/// only the difference between the two pipelines.
/// </summary>
public static class MinimalApiEndpoints
{
    /// <summary>The name this pipeline reports in its log entries.</summary>
    public const string PipelineName = "minimal";

    /// <summary>Maps the Minimal API implementation under <c>/api/minimal</c>.</summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapShopMinimalApi(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var api = endpoints.MapGroup("/api/minimal").WithTags("NebuShop (Minimal API)");

        api.MapGet("/orders/{id:int}", Results<Ok<OrderView>, NotFound> (int id, OrderService orders) =>
        {
            var order = orders.GetOrder(id, PipelineName);
            return order is null ? TypedResults.NotFound() : TypedResults.Ok(order);
        });

        api.MapPost("/orders", Results<Ok<OrderView>, ProblemHttpResult> (
            PlaceOrderRequest request,
            OrderService orders) =>
        {
            var (order, field, error) = orders.PlaceOrder(request);

            return order is not null
                ? TypedResults.Ok(order)
                : TypedResults.Problem(
                    title: "The order was rejected.",
                    detail: error,
                    statusCode: StatusCodes.Status400BadRequest,
                    extensions: new Dictionary<string, object?> { ["field"] = field });
        });

        api.MapPost("/checkout", async Task<Results<Ok<CheckoutResult>, ProblemHttpResult>> (
            CheckoutRequest request,
            OrderService orders,
            CancellationToken cancellationToken) =>
        {
            var (result, error) = await orders.CheckoutAsync(request, cancellationToken).ConfigureAwait(false);

            return result is not null
                ? TypedResults.Ok(result)
                : TypedResults.Problem(
                    title: "The checkout did not complete.",
                    detail: error,
                    statusCode: StatusCodes.Status400BadRequest);
        });

        api.MapPost("/signin", Ok<SignedIn> (SignInRequest request, OrderService orders) =>
        {
            orders.SignIn(request);
            return TypedResults.Ok(new SignedIn(request.Username));
        });

        api.MapPost("/broken", IResult () =>
        {
            OrderService.Break();
            return TypedResults.Ok();
        });

        return endpoints;
    }
}

/// <summary>The response to a successful sign-in. It deliberately carries nothing sensitive.</summary>
/// <param name="Username">Who signed in.</param>
public sealed record SignedIn(string Username);
