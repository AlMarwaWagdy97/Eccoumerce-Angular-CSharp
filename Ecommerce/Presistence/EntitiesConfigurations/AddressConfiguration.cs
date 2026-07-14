namespace Ecommerce.Presistence.EntitiesConfigurations
{
    public class AddressConfiguration : IEntityTypeConfiguration<Address>
    {
        public void Configure(EntityTypeBuilder<Address> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedOnAdd();

            builder.Property(x => x.FullName).HasMaxLength(200).IsRequired();
            builder.Property(x => x.Phone).HasMaxLength(30).IsRequired();
            builder.Property(x => x.Line1).HasMaxLength(300).IsRequired();
            builder.Property(x => x.Line2).HasMaxLength(300);
            builder.Property(x => x.City).HasMaxLength(100).IsRequired();
            builder.Property(x => x.State).HasMaxLength(100).IsRequired();
            builder.Property(x => x.Country).HasMaxLength(100).IsRequired();
            builder.Property(x => x.PostalCode).HasMaxLength(20);

            builder.HasOne(x => x.User)
                   .WithMany()
                   .HasForeignKey(x => x.UserId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
