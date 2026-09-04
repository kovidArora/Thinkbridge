using Ordering.Domain;

namespace Ordering.Application;

public record PlaceOrderCommand(Guid CustomerId, IReadOnlyList<PlaceOrderLine> Lines);
public record PlaceOrderLine(string ProductSku, int Quantity, decimal UnitPrice);

public class PlaceOrderCommandHandler(IOrderRepository repository, IUnitOfWork unitOfWork)
{
    public async Task<Guid> HandleAsync(PlaceOrderCommand command, CancellationToken cancellationToken)
    {
        var lines = command.Lines.Select(l => OrderLine.Create(l.ProductSku, l.Quantity, l.UnitPrice));
        var order = Order.Place(command.CustomerId, lines);

        repository.Add(order);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return order.Id;
    }
}
