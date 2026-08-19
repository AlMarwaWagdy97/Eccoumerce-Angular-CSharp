namespace Ecommerce.Presistence.EntitiesConfigurations
{
    public class CategoryConfiguration : IEntityTypeConfiguration<Category>
    {
        public void Configure(EntityTypeBuilder<Category> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedOnAdd();

            builder.Property(x => x.Title).HasMaxLength(255).IsRequired();

            // Filtered so a soft-deleted category releases its slug — see ProductConfiguration.
            builder.HasIndex(x => x.Slug).IsUnique().HasFilter("[IsDeleted] = 0");
            builder.Property(x => x.Slug).HasMaxLength(255).IsRequired();

            builder.Property(x => x.Status).HasDefaultValue(true);
            builder.Property(x => x.Feature).HasDefaultValue(false);

            builder.HasOne(x => x.Parent)
                   .WithMany(x => x.SubCategories)
                   .HasForeignKey(x => x.ParentId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}