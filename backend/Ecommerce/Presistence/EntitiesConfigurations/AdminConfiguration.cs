namespace Ecommerce.Presistence.EntitiesConfigurations;

public class AdminConfiguration : IEntityTypeConfiguration<Admin>
{
    public void Configure(EntityTypeBuilder<Admin> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.LastName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(256).IsRequired();
        builder.HasIndex(x => x.Email).IsUnique();
        builder.Property(x => x.PasswordHash).IsRequired();
        builder.Property(x => x.PhoneNumber).HasMaxLength(30);

        builder.HasOne(x => x.AdminRole)
               .WithMany(x => x.Admins)
               .HasForeignKey(x => x.AdminRoleId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.OwnsMany(x => x.RefreshTokens)
               .ToTable("AdminRefreshTokens")
               .WithOwner()
               .HasForeignKey("AdminId");

        builder.OwnsMany(x => x.PasswordResetTokens)
               .ToTable("AdminPasswordResetTokens")
               .WithOwner()
               .HasForeignKey("AdminId");
    }
}
