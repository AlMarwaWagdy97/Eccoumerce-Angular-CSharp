using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;
using System.Reflection;
using System.Security.Claims;


namespace Ecommerce.Presistence

{

    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IHttpContextAccessor httpContextAccessor) :
    IdentityDbContext<ApplicationUser>(options)
    {
        private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

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
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var entries = ChangeTracker.Entries<AuditableEntity>();

            foreach (var entityEntry in entries)
            {
                var claim = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
                long? currentUserId = long.TryParse(claim, out var adminId) ? adminId : null;

                if (entityEntry.State == EntityState.Added)
                {
                    entityEntry.Property(x => x.CreatedById).CurrentValue = currentUserId;
                }
                else if (entityEntry.State == EntityState.Modified)
                {
                    entityEntry.Property(x => x.UpdatedById).CurrentValue = currentUserId;
                    entityEntry.Property(x => x.UpdatedOn).CurrentValue = DateTime.UtcNow;
                }
            }

            return base.SaveChangesAsync(cancellationToken);
        }
    }

    
}
