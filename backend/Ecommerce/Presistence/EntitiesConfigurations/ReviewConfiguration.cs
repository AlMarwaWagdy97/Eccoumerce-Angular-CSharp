namespace Ecommerce.Presistence.EntitiesConfigurations
{
    public class ReviewConfiguration : IEntityTypeConfiguration<Review>
    {
        public void Configure(EntityTypeBuilder<Review> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedOnAdd();

            builder.Property(x => x.Comment).HasMaxLength(2000);

            // One review per user per product
            // Filtered so deleting a review lets the same user review the product again.
            builder.HasIndex(x => new { x.ProductId, x.UserId }).IsUnique().HasFilter("[IsDeleted] = 0");

            builder.HasOne(x => x.Product)
                   .WithMany(x => x.Reviews)
                   .HasForeignKey(x => x.ProductId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.User)
                   .WithMany()
                   .HasForeignKey(x => x.UserId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
