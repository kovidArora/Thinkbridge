using OrderSystem.Domain.Entities;

namespace OrderSystem.Application.Interfaces;

public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(int id, CancellationToken cancellationToken);
}
