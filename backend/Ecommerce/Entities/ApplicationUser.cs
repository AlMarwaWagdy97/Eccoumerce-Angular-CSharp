using Microsoft.AspNetCore.Identity;

namespace Ecommerce.Entities;

// Implements IAuditable rather than inheriting AuditableEntity: the single base-class
// slot is already taken by IdentityUser. The properties are duplicated deliberately.
public sealed class ApplicationUser : IdentityUser, IAuditable
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    public List<RefreshToken> RefreshTokens { get; set; } = [];

    public long? CreatedById { get; set; }
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    public long? UpdatedById { get; set; }
    public DateTime? UpdatedOn { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedOn { get; set; }
    public long? DeletedById { get; set; }

    public Admin? CreatedBy { get; set; }
    public Admin? UpdatedBy { get; set; }
    public Admin? DeletedBy { get; set; }
}
