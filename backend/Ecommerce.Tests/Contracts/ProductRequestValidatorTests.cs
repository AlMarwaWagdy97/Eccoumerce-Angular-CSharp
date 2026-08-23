using Ecommerce.Contracts.Products;
using FluentValidation;

namespace Ecommerce.Tests.Contracts;

public class ProductRequestValidatorTests
{
    private static readonly ProductRequestValidator Validator = new();

    private static ProductRequest Request(
        long categoryId = 1,
        string title = "Runner",
        string slug = "runner",
        string sku = "SKU-1",
        double price = 50,
        string? description = "A comfortable running shoe.") =>
        new(categoryId, title, slug, sku, price, description, null, null, null, null, 1, true, false, null, null);

    [Fact]
    public void A_fully_populated_request_is_valid()
    {
        var result = Validator.Validate(Request());

        Assert.True(result.IsValid);
    }

    [Fact]
    public void A_blank_title_is_invalid()
    {
        var result = Validator.Validate(Request(title: ""));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void A_zero_price_is_invalid()
    {
        var result = Validator.Validate(Request(price: 0));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void A_negative_price_is_invalid()
    {
        var result = Validator.Validate(Request(price: -10));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void A_description_over_2000_characters_is_invalid()
    {
        var result = Validator.Validate(Request(description: new string('a', 2001)));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void A_blank_slug_is_invalid()
    {
        var result = Validator.Validate(Request(slug: ""));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void A_blank_sku_is_invalid()
    {
        var result = Validator.Validate(Request(sku: ""));

        Assert.False(result.IsValid);
    }
}
