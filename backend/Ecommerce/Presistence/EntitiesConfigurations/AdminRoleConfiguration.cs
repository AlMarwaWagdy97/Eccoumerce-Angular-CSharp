namespace Ecommerce.Presistence.EntitiesConfigurations;

public class AdminRoleConfiguration : IEntityTypeConfiguration<AdminRole>
{
    public void Configure(EntityTypeBuilder<AdminRole> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => x.Name).IsUnique();
        builder.Property(x => x.Description).HasMaxLength(500);

        builder.HasMany(x => x.Permissions)
               .WithMany(x => x.AdminRoles)
               .UsingEntity<AdminRolePermission>(
                   j => j.HasOne(x => x.Permission).WithMany().HasForeignKey(x => x.PermissionId),
                   j => j.HasOne(x => x.AdminRole).WithMany().HasForeignKey(x => x.AdminRoleId),
                   j =>
                   {
                       j.ToTable("AdminRolePermissions");
                       j.HasKey(x => new { x.AdminRoleId, x.PermissionId });
                   });
    }
}
