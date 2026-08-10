using QuotesApi.Models;

namespace QuotesApi.Repositories;

public interface ICollectionRepository
{
    Task<Collection?> GetById(int id);
    Task Add(Collection collection);
    Task Update(Collection collection);
    Task Delete(Collection collection);
}