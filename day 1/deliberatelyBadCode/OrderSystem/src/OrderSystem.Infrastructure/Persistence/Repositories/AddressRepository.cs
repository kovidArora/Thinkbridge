using Microsoft.EntityFrameworkCore;
using OrderSystem.Application.Interfaces;
using OrderSystem.Domain.Entities;

namespace OrderSystem.Infrastructure.Persistence.Repositories;

public class AddressRepository : IAddressRepository
{
    private readonly AppDbContext _context;

    public AddressRepository(AppDbContext context) => _context = context;

    public Task<Address?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        _context.Addresses.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
}
