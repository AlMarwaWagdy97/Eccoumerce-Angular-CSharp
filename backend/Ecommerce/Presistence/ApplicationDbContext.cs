using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Claims;


namespace Ecommerce.Presistence

{

    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        // Written out rather than as a primary constructor (the convention elsewhere) because
        // ChangeTracker only exists on an instance and the timing below has to be in place
        // before the first Remove().
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IHttpContextAccessor httpContextAccessor)
            : base(options)
        {
            _httpContextAccessor = httpContextAccessor;

            // Removing a soft-deletable entity threw immediately: EF severed the required foreign
            // key of every dependent it already had tracked, before SaveChanges ever ran. Deferring
            // the cascade to save time lets ApplyAuditRules rewrite the delete into an update first,
            // so the principal is no longer Deleted by the time EF looks at its dependents and
            // nothing gets severed.
            ChangeTracker.CascadeDeleteTiming = CascadeTiming.OnSaveChanges;
        }

        public DbSet<Category> Categories { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<ProductImage> ProductImages { get; set; }
        public DbSet<Review> Reviews { get; set; }
        public DbSet<Cart> Carts { get; set; }
        public DbSet<CartItem> CartItems { get; set; }
        public DbSet<Address> Addresses { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<Favorite> Favorites { get; set; }
        public DbSet<Card> Cards { get; set; }
        public DbSet<NewsletterSubscription> NewsletterSubscriptions { get; set; }
        public DbSet<Admin> Admins { get; set; }
        public DbSet<AdminRole> AdminRoles { get; set; }
        public DbSet<Permission> Permissions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

            // The three audit navigations all point at Admin and have no inverse collection, so EF
            // cannot pair them by convention — on Admin itself they are three self-references and
            // model building fails outright. Configure them explicitly for every IAuditable type.
            var auditableTypes = modelBuilder.Model
                    .GetEntityTypes()
                    .Where(t => typeof(IAuditable).IsAssignableFrom(t.ClrType))
                    .Select(t => t.ClrType)
                    .ToList();

            foreach (var clrType in auditableTypes)
            {
                modelBuilder.Entity(clrType, b =>
                {
                    b.HasOne(typeof(Admin), nameof(IAuditable.CreatedBy))
                        .WithMany()
                        .HasForeignKey(nameof(IAuditable.CreatedById))
                        .OnDelete(DeleteBehavior.Restrict);

                    b.HasOne(typeof(Admin), nameof(IAuditable.UpdatedBy))
                        .WithMany()
                        .HasForeignKey(nameof(IAuditable.UpdatedById))
                        .OnDelete(DeleteBehavior.Restrict);

                    b.HasOne(typeof(Admin), nameof(IAuditable.DeletedBy))
                        .WithMany()
                        .HasForeignKey(nameof(IAuditable.DeletedById))
                        .OnDelete(DeleteBehavior.Restrict);
                });
            }

            var cascadeFKs = modelBuilder.Model
                    .GetEntityTypes()
                    .SelectMany(t => t.GetForeignKeys())
                    .Where(fk => fk.DeleteBehavior == DeleteBehavior.Cascade && !fk.IsOwnership);

            foreach (var fk in cascadeFKs)
                fk.DeleteBehavior = DeleteBehavior.Restrict;

            base.OnModelCreating(modelBuilder);

            // Soft delete: hide IsDeleted rows from every query. This runs AFTER base.OnModelCreating
            // so the Identity entity types (ApplicationUser) are already registered and get filtered too.
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (!typeof(IAuditable).IsAssignableFrom(entityType.ClrType))
                    continue;

                var parameter = Expression.Parameter(entityType.ClrType, "e");
                var body = Expression.Not(Expression.Property(parameter, nameof(IAuditable.IsDeleted)));
                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(Expression.Lambda(body, parameter));
            }
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            ApplyAuditRules();
            return base.SaveChangesAsync(cancellationToken);
        }

        // Overridden as well so synchronous saves cannot bypass auditing or soft delete.
        public override int SaveChanges()
        {
            ApplyAuditRules();
            return base.SaveChanges();
        }

        private void ApplyAuditRules()
        {
            var adminId = CurrentAdminId();
            var now = DateTime.UtcNow;

            // Materialised first: flipping an entry's State mutates the change tracker,
            // which would invalidate a live enumeration.
            foreach (var entry in ChangeTracker.Entries<IAuditable>().ToList())
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        entry.Entity.CreatedById = adminId;
                        break;

                    case EntityState.Modified:
                        entry.Entity.UpdatedById = adminId;
                        entry.Entity.UpdatedOn = now;
                        break;

                    case EntityState.Deleted:
                        // Soft delete: rewrite the delete into an update. Every existing
                        // Remove() call in every service becomes a soft delete for free.
                        entry.State = EntityState.Modified;
                        entry.Entity.IsDeleted = true;
                        entry.Entity.DeletedOn = now;
                        entry.Entity.DeletedById = adminId;
                        break;
                }
            }
        }

        // The admin JWT carries the Admin's numeric id in `sub` (mapped to NameIdentifier);
        // the customer JWT carries a GUID there. Only a long is a real Admin id, so a failed
        // parse means "not an admin request" and the audit columns stay null.
        private long? CurrentAdminId()
        {
            var value = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return long.TryParse(value, out var adminId) ? adminId : null;
        }
    }

    
}
