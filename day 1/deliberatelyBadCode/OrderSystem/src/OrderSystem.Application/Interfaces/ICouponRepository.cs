using OrderSystem.Domain.Entities;

namespace OrderSystem.Application.Interfaces;

public interface ICouponRepository
{
    Task<Coupon?> GetActiveByCodeAsync(string code, CancellationToken cancellationToken);
}
