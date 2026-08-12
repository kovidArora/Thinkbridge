using OrderSystem.Domain.Entities;

namespace OrderSystem.Application.Interfaces;

public interface IOrderRepository
{
    void Add(Order order);
    Task<Order?> GetByIdWithItemsAsync(int id, CancellationToken cancellationToken);
}
