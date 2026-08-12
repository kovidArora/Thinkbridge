using OrderSystem.Domain.Entities;

namespace OrderSystem.Application.Interfaces;

public interface IAddressRepository
{
    Task<Address?> GetByIdAsync(int id, CancellationToken cancellationToken);
}
