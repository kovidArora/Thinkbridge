using Microsoft.EntityFrameworkCore;
using OrderSystem.Application.Interfaces;
using OrderSystem.Domain.Entities;

namespace OrderSystem.Infrastructure.Persistence.Repositories;

public class CouponRepository : ICouponRepository
{
    private readonly AppDbContext _context;

    public CouponRepository(AppDbContext context) => _context = context;

    public Task<Coupon?> GetActiveByCodeAsync(string code, CancellationToken cancellationToken) =>
        _context.Coupons.FirstOrDefaultAsync(c => c.Code == code && c.IsActive, cancellationToken);
}
