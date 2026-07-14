using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Ecommerce.Presistence;
using Ecommerce.Entities;
using Ecommerce.Contracts.Cart;
using Ecommerce.Errors;
using Ecommerce.Abstractions;

namespace Ecommerce.Services;

public class CartService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor) : ICartService
{
    private readonly ApplicationDbContext _context = context;
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    public async Task<Result<CartResponse>> GetAsync(CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Result.Failure<CartResponse>(CartErrors.UserNotAuthenticated);

        var cart = await LoadCartAsync(userId, cancellationToken);
        return Result.Success(MapCart(cart));
    }

    public async Task<Result<CartCountResponse>> GetCountAsync(CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Result.Failure<CartCountResponse>(CartErrors.UserNotAuthenticated);

        var count = await _context.CartItems
            .AsNoTracking()
            .Where(x => x.Cart.UserId == userId)
            .SumAsync(x => (int?)x.Quantity, cancellationToken) ?? 0;

        return Result.Success(new CartCountResponse(count));
    }

    public async Task<Result<CartResponse>> AddItemAsync(AddToCartRequest request, CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Result.Failure<CartResponse>(CartErrors.UserNotAuthenticated);

        if (request.Quantity <= 0)
            return Result.Failure<CartResponse>(CartErrors.InvalidQuantity);

        var product = await _context.Products.FindAsync(new object[] { request.ProductId }, cancellationToken);
        if (product is null)
            return Result.Failure<CartResponse>(ProductErrors.ProductNotFound);

        var cart = await GetOrCreateCartAsync(userId, cancellationToken);

        var item = cart.Items.FirstOrDefault(x => x.ProductId == request.ProductId);
        var newQuantity = (item?.Quantity ?? 0) + request.Quantity;

        if (newQuantity > product.StockQuantity)
            return Result.Failure<CartResponse>(CartErrors.InsufficientStock);

        if (item is null)
        {
            item = new CartItem
            {
                CartId = cart.Id,
                ProductId = request.ProductId,
                Quantity = request.Quantity,
                Product = product
            };
            cart.Items.Add(item);
        }
        else
        {
            item.Quantity = newQuantity;
        }

        await _context.SaveChangesAsync(cancellationToken);

        var cartWithItems = await LoadCartAsync(userId, cancellationToken);
        return Result.Success(MapCart(cartWithItems));
    }

    public async Task<Result<CartResponse>> UpdateItemAsync(long productId, UpdateCartItemRequest request, CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Result.Failure<CartResponse>(CartErrors.UserNotAuthenticated);

        if (request.Quantity <= 0)
            return Result.Failure<CartResponse>(CartErrors.InvalidQuantity);

        var item = await _context.CartItems
            .Include(x => x.Product)
            .FirstOrDefaultAsync(x => x.ProductId == productId && x.Cart.UserId == userId, cancellationToken);

        if (item is null)
            return Result.Failure<CartResponse>(CartErrors.CartItemNotFound);

        if (request.Quantity > item.Product.StockQuantity)
            return Result.Failure<CartResponse>(CartErrors.InsufficientStock);

        item.Quantity = request.Quantity;
        await _context.SaveChangesAsync(cancellationToken);

        var cart = await LoadCartAsync(userId, cancellationToken);
        return Result.Success(MapCart(cart));
    }

    public async Task<Result> RemoveItemAsync(long productId, CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Result.Failure(CartErrors.UserNotAuthenticated);

        var item = await _context.CartItems
            .FirstOrDefaultAsync(x => x.ProductId == productId && x.Cart.UserId == userId, cancellationToken);

        if (item is null)
            return Result.Failure(CartErrors.CartItemNotFound);

        _context.CartItems.Remove(item);
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> ClearAsync(CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Result.Failure(CartErrors.UserNotAuthenticated);

        var items = await _context.CartItems
            .Where(x => x.Cart.UserId == userId)
            .ToListAsync(cancellationToken);

        if (items.Count == 0)
            return Result.Success();

        _context.CartItems.RemoveRange(items);
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private string? GetCurrentUserId() =>
        _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);

    private async Task<Cart> GetOrCreateCartAsync(string userId, CancellationToken cancellationToken)
    {
        var cart = await _context.Carts
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

        if (cart is not null)
            return cart;

        cart = new Cart { UserId = userId };
        await _context.Carts.AddAsync(cart, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return cart;
    }

    private async Task<Cart?> LoadCartAsync(string userId, CancellationToken cancellationToken) =>
        await _context.Carts
            .AsNoTracking()
            .Include(x => x.Items)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

    private static CartResponse MapCart(Cart? cart)
    {
        if (cart is null)
            return new CartResponse(0, Array.Empty<CartItemResponse>(), 0, 0);

        var items = cart.Items
            .Select(i =>
            {
                var unitPrice = i.Product.PriceAfterSale ?? i.Product.Price;
                return new CartItemResponse(
                    i.Id,
                    i.ProductId,
                    i.Product.Title,
                    i.Product.Image,
                    unitPrice,
                    i.Quantity,
                    unitPrice * i.Quantity);
            })
            .ToList();

        return new CartResponse(
            cart.Id,
            items,
            items.Sum(x => x.Quantity),
            items.Sum(x => x.LineTotal));
    }
}
