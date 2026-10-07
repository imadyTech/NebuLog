using Microsoft.AspNetCore.Mvc;

namespace NebuShop.Orders.Controllers;

/// <summary>
/// The MVC half of the shop: the same five operations as the Minimal API group, served through
/// controllers, model binding and an action filter.
/// </summary>
/// <remarks>
/// This exists to be compared, not because the shop needs two of everything. An identical request
/// through this pipeline produces noticeably more framework log entries — route matching, action
/// selection, model binding, filter execution, result execution — and seeing both side by side in
/// the console is the point of scenario 01.
/// </remarks>
[ApiController]
[Route("api/mvc")]
[ServiceFilter<ShopActionFilter>]
public sealed class OrdersController : ControllerBase
{
    /// <summary>The name this pipeline reports in its log entries.</summary>
    public const string PipelineName = "mvc";

    private readonly OrderService _orders;

    /// <summary>Creates the controller.</summary>
    /// <param name="orders">The shared implementation both pipelines call.</param>
    public OrdersController(OrderService orders) => _orders = orders;

    /// <summary>Looks up one order.</summary>
    /// <param name="id">The order identifier.</param>
    /// <returns>The order, or 404.</returns>
    [HttpGet("orders/{id:int}")]
    public ActionResult<OrderView> GetOrder(int id)
    {
        var order = _orders.GetOrder(id, PipelineName);
        return order is null ? NotFound() : Ok(order);
    }

    /// <summary>Places an order.</summary>
    /// <param name="request">What to order.</param>
    /// <returns>The created order, or a 400 with the offending field.</returns>
    [HttpPost("orders")]
    public ActionResult<OrderView> PlaceOrder([FromBody] PlaceOrderRequest request)
    {
        var (order, field, error) = _orders.PlaceOrder(request);

        if (order is not null)
        {
            return Ok(order);
        }

        var problem = ProblemDetails("The order was rejected.", error);
        problem.Extensions["field"] = field;
        return BadRequest(problem);
    }

    /// <summary>Checks out a basket, which calls the payments service.</summary>
    /// <param name="request">The basket.</param>
    /// <param name="cancellationToken">Cancels the outbound call.</param>
    /// <returns>The checkout result, or a 400.</returns>
    [HttpPost("checkout")]
    public async Task<ActionResult<CheckoutResult>> CheckoutAsync(
        [FromBody] CheckoutRequest request,
        CancellationToken cancellationToken)
    {
        var (result, error) = await _orders.CheckoutAsync(request, cancellationToken).ConfigureAwait(false);

        return result is not null
            ? Ok(result)
            : BadRequest(ProblemDetails("The checkout did not complete.", error));
    }

    /// <summary>Signs in, to demonstrate redaction.</summary>
    /// <param name="request">The credentials.</param>
    /// <returns>Who signed in.</returns>
    [HttpPost("signin")]
    public ActionResult<SignedIn> SignIn([FromBody] SignInRequest request)
    {
        _orders.SignIn(request);
        return Ok(new SignedIn(request.Username));
    }

    /// <summary>Throws, so the exception handler can turn it into a 500.</summary>
    /// <returns>Never returns.</returns>
    [HttpPost("broken")]
    public ActionResult Broken()
    {
        OrderService.Break();
        return Ok();
    }

    private ProblemDetails ProblemDetails(string title, string? detail) => new()
    {
        Title = title,
        Detail = detail,
        Status = StatusCodes.Status400BadRequest,
        Instance = HttpContext.Request.Path,
    };
}
