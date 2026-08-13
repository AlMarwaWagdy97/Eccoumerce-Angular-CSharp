using Ecommerce.Entities;

namespace Ecommerce.Tests.Entities;

public class AuditableEntityTests
{
    public static TheoryData<Type> AuditedTypes =>
    [
        typeof(Category), typeof(Product), typeof(ProductImage),
        typeof(Order), typeof(OrderItem), typeof(Address), typeof(Card),
        typeof(Review), typeof(Admin), typeof(AdminRole), typeof(ApplicationUser),
    ];

    [Theory]
    [MemberData(nameof(AuditedTypes))]
    public void Audited_entities_implement_IAuditable(Type type)
    {
        Assert.True(typeof(IAuditable).IsAssignableFrom(type), $"{type.Name} must implement IAuditable");
    }

    [Fact]
    public void ApplicationUser_implements_the_interface_without_the_base_class()
    {
        // ApplicationUser already inherits IdentityUser, so it can only implement the interface.
        Assert.True(typeof(IAuditable).IsAssignableFrom(typeof(ApplicationUser)));
        Assert.False(typeof(AuditableEntity).IsAssignableFrom(typeof(ApplicationUser)));
    }

    [Fact]
    public void A_new_auditable_entity_defaults_to_not_deleted_with_a_creation_timestamp()
    {
        var category = new Category { Title = "Shoes", Slug = "shoes" };

        Assert.False(category.IsDeleted);
        Assert.Null(category.DeletedOn);
        Assert.Null(category.CreatedById);
        Assert.NotEqual(default, category.CreatedOn);
    }
}
