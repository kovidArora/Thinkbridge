using OrderSystem.Domain.Entities;

namespace OrderSystem.Application.Interfaces;

public interface IProductRepository
{
    /// <summary>
    /// Loads all requested products in a single query and returns the
    /// tracked entities keyed by id. Missing ids are simply absent from
    /// the result — callers detect "not found" via TryGetValue.
    /// </summary>
    Task<Dictionary<int, Product>> GetByIdsAsync(IEnumerable<int> ids, CancellationToken cancellationToken);
}
