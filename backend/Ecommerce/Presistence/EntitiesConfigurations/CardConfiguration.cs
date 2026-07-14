namespace Ecommerce.Presistence.EntitiesConfigurations
{
    public class CardConfiguration : IEntityTypeConfiguration<Card>
    {
        public void Configure(EntityTypeBuilder<Card> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedOnAdd();

            builder.Property(x => x.CardholderName).HasMaxLength(200).IsRequired();
            builder.Property(x => x.Brand).HasMaxLength(30).IsRequired();
            builder.Property(x => x.Last4).HasMaxLength(4).IsFixedLength().IsRequired();

            builder.HasOne(x => x.User)
                   .WithMany()
                   .HasForeignKey(x => x.UserId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
