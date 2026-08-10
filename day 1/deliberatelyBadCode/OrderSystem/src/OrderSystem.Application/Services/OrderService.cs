using Microsoft.Extensions.Logging;
using OrderSystem.Application.Common;
using OrderSystem.Application.Dtos;
using OrderSystem.Application.Interfaces;
using OrderSystem.Application.Notifications;
using OrderSystem.Application.Payments;
using OrderSystem.Application.Pricing;
using OrderSystem.Domain.Entities;
using OrderSystem.Domain.Enums;

namespace OrderSystem.Application.Services;

public class OrderService : IOrderService
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IProductRepository _productRepository;
    private readonly ICouponRepository _couponRepository;
    private readonly IAddressRepository _addressRepository;
    private readonly IOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOrderPricingService _pricingService;
    private readonly IPaymentGateway _paymentGateway;
    private readonly IOrderNotificationService _notificationService;
    private readonly IOrderReferenceGenerator _referenceGenerator;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        ICustomerRepository customerRepository,
        IProductRepository productRepository,
        ICouponRepository couponRepository,
        IAddressRepository addressRepository,
        IOrderRepository orderRepository,
        IUnitOfWork unitOfWork,
        IOrderPricingService pricingService,
        IPaymentGateway paymentGateway,
        IOrderNotificationService notificationService,
        IOrderReferenceGenerator referenceGenerator,
        ILogger<OrderService> logger)
    {
        _customerRepository = customerRepository;
        _productRepository = productRepository;
        _couponRepository = couponRepository;
        _addressRepository = addressRepository;
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
        _pricingService = pricingService;
        _paymentGateway = paymentGateway;
        _notificationService = notificationService;
        _referenceGenerator = referenceGenerator;
        _logger = logger;
    }

    public async Task<Result<OrderResponse>> CreateOrderAsync(OrderRequest request, CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.GetByIdAsync(request.CustomerId, cancellationToken);
        if (customer is null)
        {
            return Result<OrderResponse>.Failure("Customer not found", ResultErrorType.NotFound);
        }

        if (!customer.IsActive)
        {
            return Result<OrderResponse>.Failure("Customer is inactive", ResultErrorType.Conflict);
        }

        var productIds = request.Items.Select(i => i.ProductId).Distinct();
        var products = await _productRepository.GetByIdsAsync(productIds, cancellationToken);

        var lines = new List<(Product Product, int Quantity)>();
        foreach (var item in request.Items)
        {
            if (!products.TryGetValue(item.ProductId, out var product))
            {
                return Result<OrderResponse>.Failure($"Product {item.ProductId} was not found", ResultErrorType.NotFound);
            }

            if (!product.IsActive)
            {
                return Result<OrderResponse>.Failure($"Product {product.Name} is inactive", ResultErrorType.Conflict);
            }

            if (product.Stock < item.Quantity)
            {
                return Result<OrderResponse>.Failure($"Not enough stock for {product.Name}", ResultErrorType.Conflict);
            }

            lines.Add((product, item.Quantity));
        }

        Coupon? coupon = null;
        if (!string.IsNullOrWhiteSpace(request.CouponCode))
        {
            coupon = await _couponRepository.GetActiveByCodeAsync(request.CouponCode, cancellationToken);
            if (coupon is null)
            {
                return Result<OrderResponse>.Failure("Invalid coupon", ResultErrorType.Validation);
            }

            if (coupon.ExpiresAt < DateTime.UtcNow)
            {
                return Result<OrderResponse>.Failure("Coupon has expired", ResultErrorType.Validation);
            }
        }

        var pricedLines = lines.Select(l => new PricedLine(l.Product.Id, l.Quantity, l.Product.Price)).ToList();
        var rawSubtotal = pricedLines.Sum(l => l.UnitPrice * l.Quantity);

        if (coupon is not null && coupon.MinimumOrderAmount > rawSubtotal)
        {
            return Result<OrderResponse>.Failure("Coupon minimum order amount not met", ResultErrorType.Validation);
        }

        var pricing = _pricingService.Calculate(pricedLines, customer.IsVip, coupon);

        Address? address = null;
        if (request.DeliveryAddressId is { } addressId)
        {
            address = await _addressRepository.GetByIdAsync(addressId, cancellationToken);
            if (address is null)
            {
                return Result<OrderResponse>.Failure("Delivery address not found", ResultErrorType.NotFound);
            }

            if (address.CustomerId != customer.Id)
            {
                return Result<OrderResponse>.Failure("Address does not belong to customer", ResultErrorType.Validation);
            }
        }

        var requiresReview = OrderReviewPolicy.RequiresManualReview(
            itemCount: request.Items.Count,
            isVip: customer.IsVip,
            total: pricing.Total,
            lineQuantities: lines.Select(l => l.Quantity));

        var order = new Order
        {
            CustomerId = customer.Id,
            CustomerName = customer.Name,
            CreatedAt = DateTime.UtcNow,
            Status = requiresReview ? OrderStatus.RequiresReview : OrderStatus.Pending,
            Priority = request.Priority ? OrderPriority.High : OrderPriority.Normal,
            Reference = _referenceGenerator.Generate(),
            Subtotal = pricing.Subtotal,
            Discount = pricing.Discount,
            Tax = pricing.Tax,
            Shipping = pricing.Shipping,
            Total = pricing.Total,
            Notes = Truncate(request.Notes?.Trim(), 500),
            Referrer = request.Referrer?.Trim(),
            DeliveryAddressId = address?.Id,
            Metadata = request.Metadata is { Count: > 0 }
                ? string.Join(";", request.Metadata.Select(kv => $"{kv.Key}={kv.Value}"))
                : null
        };

        foreach (var (product, quantity) in lines)
        {
            order.Items.Add(new OrderItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                Quantity = quantity,
                UnitPrice = product.Price
            });

            // Decrementing stock on the same tracked entity that gets
            // persisted in the single SaveChangesAsync call below means
            // the stock change and the order creation succeed or fail
            // together, instead of the original's untracked, unguarded
            // in-memory mutation.
            product.Stock -= quantity;
        }

        if (request.PaymentMethod == PaymentMethodType.Card)
        {
            PaymentResult paymentResult;
            try
            {
                paymentResult = await _paymentGateway.ChargeAsync(
                    new PaymentRequest(request.CardNumber!, pricing.Total),
                    cancellationToken);
            }
            catch (PaymentGatewayException ex)
            {
                // A genuine gateway fault (timeout, unreachable service) is
                // not a business-rule failure — log it and let it propagate
                // to the global exception handler, which returns a 500
                // instead of silently pretending the order succeeded.
                _logger.LogError(ex, "Payment gateway error while charging customer {CustomerId}", customer.Id);
                throw;
            }

            if (!paymentResult.Approved)
            {
                return Result<OrderResponse>.Failure(
                    paymentResult.FailureReason ?? "Payment declined",
                    ResultErrorType.PaymentDeclined);
            }

            order.PaymentStatus = PaymentStatus.Paid;
        }
        else
        {
            order.PaymentStatus = PaymentStatus.Pending;
        }

        if (coupon is not null)
        {
            coupon.UsageCount++;
        }

        _orderRepository.Add(order);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await SendBestEffortNotificationsAsync(order, customer, cancellationToken);

        return Result<OrderResponse>.Success(MapToResponse(order));
    }

    public async Task<OrderResponse?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdWithItemsAsync(id, cancellationToken);
        return order is null ? null : MapToResponse(order);
    }

    private async Task SendBestEffortNotificationsAsync(Order order, Customer customer, CancellationToken cancellationToken)
    {
        // The order is already committed at this point. A failed
        // confirmation email or review alert is logged, not rethrown:
        // rethrowing here would turn an already-successful order creation
        // into a 500 response, which is worse than the original bug of
        // silently swallowing it. This is a deliberate, narrow exception,
        // not a blanket catch-and-ignore.
        try
        {
            if (order.PaymentStatus == PaymentStatus.Paid)
            {
                await _notificationService.SendOrderConfirmationAsync(order, customer, cancellationToken);
            }

            if (order.Status == OrderStatus.RequiresReview)
            {
                await _notificationService.SendReviewRequiredAsync(order, cancellationToken);
            }
        }
        catch (NotificationException ex)
        {
            _logger.LogError(ex, "Failed to send notification(s) for order {OrderId}", order.Id);
        }
    }

    private static string? Truncate(string? value, int maxLength) =>
        string.IsNullOrEmpty(value) ? value : value[..Math.Min(value.Length, maxLength)];

    private static OrderResponse MapToResponse(Order order) => new(
        order.Id,
        order.Reference,
        order.CustomerId,
        order.CustomerName,
        order.Status.ToString(),
        order.PaymentStatus.ToString(),
        order.Subtotal,
        order.Discount,
        order.Tax,
        order.Shipping,
        order.Total,
        order.CreatedAt,
        order.Priority.ToString(),
        order.Items
            .Select(i => new OrderItemResponse(i.ProductId, i.ProductName, i.Quantity, i.UnitPrice, i.Quantity * i.UnitPrice))
            .ToList());
}
