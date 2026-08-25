using Ecommerce.Contracts.Orders;
using Ecommerce.Entities;

namespace Ecommerce.Tests.Contracts;

public class UpdateOrderStatusRequestValidatorTests
{
    private readonly UpdateOrderStatusRequestValidator _validator = new();

    [Fact]
    public void A_valid_request_passes()
    {
        var result = _validator.Validate(new UpdateOrderStatusRequest(OrderStatus.Shipped, PaymentStatus.Paid));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void An_undefined_OrderStatus_value_fails()
    {
        var result = _validator.Validate(new UpdateOrderStatusRequest((OrderStatus)99, PaymentStatus.Paid));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void An_undefined_PaymentStatus_value_fails()
    {
        var result = _validator.Validate(new UpdateOrderStatusRequest(OrderStatus.Shipped, (PaymentStatus)42));

        Assert.False(result.IsValid);
    }
}
