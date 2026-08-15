namespace Ecommerce.Presistence.EntitiesConfigurations
{
    public class ProductConfiguration : IEntityTypeConfiguration<Product>
    {
        public void Configure(EntityTypeBuilder<Product> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedOnAdd();

            builder.Property(x => x.Title).HasMaxLength(255).IsRequired();

            // Filtered so a soft-deleted product releases its slug/SKU. Without the filter the
            // deleted row keeps its slot in the index while the service's uniqueness check —
            // which is query-filtered — cannot see it, turning a reuse into an opaque 500.
            builder.HasIndex(x => x.Slug).IsUnique().HasFilter("[IsDeleted] = 0");
            builder.Property(x => x.Slug).HasMaxLength(255).IsRequired();

            builder.HasIndex(x => x.Sku).IsUnique().HasFilter("[IsDeleted] = 0");
            builder.Property(x => x.Sku).HasMaxLength(100).IsRequired();

            builder.Property(x => x.Price).IsRequired();

            builder.Property(x => x.Status).HasDefaultValue(true);
            builder.Property(x => x.Feature).HasDefaultValue(false);

            builder.HasOne(x => x.Category)
                   .WithMany(x => x.Products)
                   .HasForeignKey(x => x.CategoryId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
