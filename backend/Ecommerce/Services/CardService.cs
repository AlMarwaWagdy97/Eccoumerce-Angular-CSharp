using Microsoft.EntityFrameworkCore;
using Ecommerce.Contracts.Cards;

namespace Ecommerce.Services;

public class CardService(ApplicationDbContext context) : ICardService
{
    private readonly ApplicationDbContext _context = context;

    public async Task<Result<IEnumerable<CardResponse>>> GetAllAsync(string userId, CancellationToken cancellationToken = default)
    {
        var cards = await _context.Cards
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.IsDefault)
            .ToListAsync(cancellationToken);

        return Result.Success<IEnumerable<CardResponse>>(cards.Select(MapCard).ToList());
    }

    public async Task<Result<CardResponse>> AddAsync(string userId, CardRequest request, CancellationToken cancellationToken = default)
    {
        var isFirstCard = !await _context.Cards.AnyAsync(x => x.UserId == userId, cancellationToken);
        var makeDefault = request.IsDefault || isFirstCard;

        if (makeDefault)
            await ClearDefaultAsync(userId, cancellationToken);

        var card = new Card
        {
            UserId = userId,
            CardholderName = request.CardholderName,
            Brand = request.Brand,
            Last4 = request.Last4,
            ExpiryMonth = request.ExpiryMonth,
            ExpiryYear = request.ExpiryYear,
            IsDefault = makeDefault
        };

        _context.Cards.Add(card);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(MapCard(card));
    }

    public async Task<Result> DeleteAsync(string userId, long id, CancellationToken cancellationToken = default)
    {
        var card = await _context.Cards.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);
        if (card is null)
            return Result.Failure(CardErrors.CardNotFound);

        _context.Cards.Remove(card);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result> SetDefaultAsync(string userId, long id, CancellationToken cancellationToken = default)
    {
        var card = await _context.Cards.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);
        if (card is null)
            return Result.Failure(CardErrors.CardNotFound);

        await ClearDefaultAsync(userId, cancellationToken);
        card.IsDefault = true;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task ClearDefaultAsync(string userId, CancellationToken cancellationToken)
    {
        var currentDefaults = await _context.Cards
            .Where(x => x.UserId == userId && x.IsDefault)
            .ToListAsync(cancellationToken);

        foreach (var card in currentDefaults)
            card.IsDefault = false;
    }

    private static CardResponse MapCard(Card x) => new(
        x.Id, x.CardholderName, x.Brand, x.Last4, x.ExpiryMonth, x.ExpiryYear, x.IsDefault);
}
