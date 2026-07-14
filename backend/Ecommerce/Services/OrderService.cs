using Microsoft.EntityFrameworkCore;
using Ecommerce.Contracts.Orders;

namespace Ecommerce.Services;

public class OrderService(ApplicationDbContext context) : IOrderService
{
    private const double FreeShippingThreshold = 50;
    private const double StandardShippingCost = 5.99;

    private readonly ApplicationDbContext _context = context;

    public async Task<Result<OrderResponse>> CreateAsync(string userId, CreateOrderRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Items.Count == 0)
            return Result.Failure<OrderResponse>(OrderErrors.EmptyOrderItems);

        var address = await _context.Addresses.FirstOrDefaultAsync(x => x.Id == request.AddressId && x.UserId == userId, cancellationToken);
        if (address is null)
            return Result.Failure<OrderResponse>(OrderErrors.AddressNotFound);

        var productIds = request.Items.Select(x => x.ProductId).Distinct().ToList();
        var products = await _context.Products
            .Where(x => productIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        if (products.Count != productIds.Count)
            return Result.Failure<OrderResponse>(OrderErrors.ProductNotFound);

        foreach (var line in request.Items)
        {
            if (line.Quantity > products[line.ProductId].StockQuantity)
                return Result.Failure<OrderResponse>(OrderErrors.InsufficientStock);
        }

        var orderItems = request.Items.Select(line =>
        {
            var product = products[line.ProductId];
            var unitPrice = product.PriceAfterSale ?? product.Price;

            product.StockQuantity -= line.Quantity;

            return new OrderItem
            {
                ProductId = product.Id,
                ProductTitle = product.Title,
                Sku = product.Sku,
                UnitPrice = unitPrice,
                Quantity = line.Quantity,
                LineTotal = unitPrice * line.Quantity
            };
        }).ToList();

        var subTotal = orderItems.Sum(x => x.LineTotal);
        var shippingCost = subTotal >= FreeShippingThreshold ? 0 : StandardShippingCost;

        var order = new Order
        {
            OrderNumber = GenerateOrderNumber(),
            UserId = userId,
            Status = OrderStatus.Pending,
            PaymentMethod = PaymentMethod.CashOnDelivery,
            PaymentStatus = PaymentStatus.Pending,
            SubTotal = subTotal,
            ShippingCost = shippingCost,
            Total = subTotal + shippingCost,
            ShipToName = address.FullName,
            ShipToPhone = address.Phone,
            ShipToLine1 = address.Line1,
            ShipToLine2 = address.Line2,
            ShipToCity = address.City,
            ShipToState = address.State,
            ShipToCountry = address.Country,
            ShipToPostalCode = address.PostalCode,
            Items = orderItems
        };

        _context.Orders.Add(order);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(MapOrder(order, orderItems.Select(i => (i, products[i.ProductId].Image))));
    }

    public async Task<Result<IEnumerable<OrderSummaryResponse>>> GetAllAsync(string userId, CancellationToken cancellationToken = default)
    {
        var orders = await _context.Orders
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedOn)
            .Select(x => new OrderSummaryResponse(x.Id, x.OrderNumber, x.Status.ToString(), x.Total, x.CreatedOn))
            .ToListAsync(cancellationToken);

        return Result.Success<IEnumerable<OrderSummaryResponse>>(orders);
    }

    public async Task<Result<OrderResponse>> GetAsync(string userId, string orderNumber, CancellationToken cancellationToken = default)
    {
        var order = await _context.Orders
            .AsNoTracking()
            .Include(x => x.Items)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(x => x.UserId == userId && x.OrderNumber == orderNumber, cancellationToken);

        if (order is null)
            return Result.Failure<OrderResponse>(OrderErrors.OrderNotFound);

        return Result.Success(MapOrder(order, order.Items.Select(i => (i, i.Product?.Image))));
    }

    public async Task<Result<OrderTrackingResponse>> GetTrackingAsync(string userId, string orderNumber, CancellationToken cancellationToken = default)
    {
        var order = await _context.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.OrderNumber == orderNumber, cancellationToken);

        if (order is null)
            return Result.Failure<OrderTrackingResponse>(OrderErrors.OrderNotFound);

        return Result.Success(BuildTracking(order));
    }

    private static string GenerateOrderNumber() =>
        $"ORD-{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}";

    private static OrderResponse MapOrder(Order order, IEnumerable<(OrderItem Item, string? Image)> items)
    {
        var itemResponses = items
            .Select(x => new OrderItemResponse(x.Item.ProductId, x.Item.ProductTitle, x.Image, x.Item.UnitPrice, x.Item.Quantity, x.Item.LineTotal))
            .ToList();

        return new OrderResponse(
            order.Id,
            order.OrderNumber,
            order.Status.ToString(),
            order.PaymentMethod.ToString(),
            order.PaymentStatus.ToString(),
            order.SubTotal,
            order.ShippingCost,
            order.Total,
            order.ShipToName,
            order.ShipToPhone,
            order.ShipToLine1,
            order.ShipToLine2,
            order.ShipToCity,
            order.ShipToState,
            order.ShipToCountry,
            order.ShipToPostalCode,
            order.CreatedOn,
            itemResponses);
    }

    private static OrderTrackingResponse BuildTracking(Order order)
    {
        if (order.Status == OrderStatus.Cancelled)
        {
            var cancelledSteps = new[]
            {
                new OrderTrackingStep(OrderStatus.Pending.ToString(), "Order placed", true, false, order.CreatedOn),
                new OrderTrackingStep(OrderStatus.Cancelled.ToString(), "Order cancelled", true, true, null)
            };
            return new OrderTrackingResponse(order.OrderNumber, order.Status.ToString(), order.CreatedOn, cancelledSteps);
        }

        var progression = new[]
        {
            (Status: OrderStatus.Pending, Label: "Order placed"),
            (Status: OrderStatus.Paid, Label: "Payment confirmed"),
            (Status: OrderStatus.Shipped, Label: "Shipped"),
            (Status: OrderStatus.Delivered, Label: "Delivered")
        };

        var steps = progression.Select(step => new OrderTrackingStep(
            step.Status.ToString(),
            step.Label,
            order.Status >= step.Status,
            order.Status == step.Status,
            step.Status == OrderStatus.Pending ? order.CreatedOn : null
        )).ToList();

        return new OrderTrackingResponse(order.OrderNumber, order.Status.ToString(), order.CreatedOn, steps);
    }
}
