namespace Ecommerce.Presistence.EntitiesConfigurations
{
    public class OrderConfiguration : IEntityTypeConfiguration<Order>
    {
        public void Configure(EntityTypeBuilder<Order> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedOnAdd();

            builder.HasIndex(x => x.OrderNumber).IsUnique().HasFilter("[IsDeleted] = 0");
            builder.Property(x => x.OrderNumber).HasMaxLength(50).IsRequired();

            // Store enums as strings for readability in the DB
            builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            builder.Property(x => x.PaymentMethod).HasConversion<string>().HasMaxLength(20);
            builder.Property(x => x.PaymentStatus).HasConversion<string>().HasMaxLength(20);

            builder.Property(x => x.ShipToName).HasMaxLength(200).IsRequired();
            builder.Property(x => x.ShipToPhone).HasMaxLength(30).IsRequired();
            builder.Property(x => x.ShipToLine1).HasMaxLength(300).IsRequired();
            builder.Property(x => x.ShipToLine2).HasMaxLength(300);
            builder.Property(x => x.ShipToCity).HasMaxLength(100).IsRequired();
            builder.Property(x => x.ShipToState).HasMaxLength(100).IsRequired();
            builder.Property(x => x.ShipToCountry).HasMaxLength(100).IsRequired();
            builder.Property(x => x.ShipToPostalCode).HasMaxLength(20);

            builder.HasOne(x => x.User)
                   .WithMany()
                   .HasForeignKey(x => x.UserId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
    {
        public void Configure(EntityTypeBuilder<OrderItem> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedOnAdd();

            builder.Property(x => x.ProductTitle).HasMaxLength(255).IsRequired();
            builder.Property(x => x.Sku).HasMaxLength(100).IsRequired();

            builder.HasOne(x => x.Order)
                   .WithMany(x => x.Items)
                   .HasForeignKey(x => x.OrderId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.Product)
                   .WithMany()
                   .HasForeignKey(x => x.ProductId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
