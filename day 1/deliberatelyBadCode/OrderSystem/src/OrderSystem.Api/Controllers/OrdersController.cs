using Microsoft.AspNetCore.Mvc;
using OrderSystem.Application.Common;
using OrderSystem.Application.Dtos;
using OrderSystem.Application.Interfaces;

namespace OrderSystem.Api.Controllers;

[ApiController]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status402PaymentRequired)]
    public async Task<IActionResult> Create([FromBody] OrderRequest request, CancellationToken cancellationToken)
    {
        var result = await _orderService.CreateOrderAsync(request, cancellationToken);

        if (result.IsSuccess)
        {
            return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
        }

        return result.ErrorType switch
        {
            ResultErrorType.NotFound => NotFound(Problem(result.Error, StatusCodes.Status404NotFound)),
            ResultErrorType.Validation => BadRequest(Problem(result.Error, StatusCodes.Status400BadRequest)),
            ResultErrorType.Conflict => Conflict(Problem(result.Error, StatusCodes.Status409Conflict)),
            ResultErrorType.PaymentDeclined => StatusCode(
                StatusCodes.Status402PaymentRequired, Problem(result.Error, StatusCodes.Status402PaymentRequired)),
            _ => StatusCode(StatusCodes.Status500InternalServerError, Problem(result.Error, StatusCodes.Status500InternalServerError))
        };
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var order = await _orderService.GetByIdAsync(id, cancellationToken);
        return order is null ? NotFound() : Ok(order);
    }

    private static ProblemDetails Problem(string? title, int statusCode) => new()
    {
        Title = title,
        Status = statusCode
    };
}
