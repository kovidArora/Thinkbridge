using OrderSystem.Application.Common;
using OrderSystem.Application.Dtos;

namespace OrderSystem.Application.Interfaces;

public interface IOrderService
{
    Task<Result<OrderResponse>> CreateOrderAsync(OrderRequest request, CancellationToken cancellationToken);
    Task<OrderResponse?> GetByIdAsync(int id, CancellationToken cancellationToken);
}
