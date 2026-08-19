namespace Ecommerce.Entities;

// Audit + soft-delete contract. This is an interface (not just a base class) because
// ApplicationUser already inherits IdentityUser and C# allows only one base class —
// a base-class-only design could not cover customer accounts at all.
// CreatedBy/UpdatedBy/DeletedBy point at Admin, not ApplicationUser: this is an
// admin-action audit trail, so customer self-service writes leave them null.
public interface IAuditable
{
    long? CreatedById { get; set; }
    DateTime CreatedOn { get; set; }
    long? UpdatedById { get; set; }
    DateTime? UpdatedOn { get; set; }

    bool IsDeleted { get; set; }
    DateTime? DeletedOn { get; set; }
    long? DeletedById { get; set; }

    Admin? CreatedBy { get; set; }
    Admin? UpdatedBy { get; set; }
    Admin? DeletedBy { get; set; }
}

public abstract class AuditableEntity : IAuditable
{
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
