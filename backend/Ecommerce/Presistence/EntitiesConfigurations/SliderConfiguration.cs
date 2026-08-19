namespace Ecommerce.Presistence.EntitiesConfigurations;

public class SliderConfiguration : IEntityTypeConfiguration<Slider>
{
    public void Configure(EntityTypeBuilder<Slider> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.Title).HasMaxLength(255).IsRequired();
        builder.Property(x => x.Image).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Link).HasMaxLength(500);

        builder.HasIndex(x => x.Sort);

        // The CreatedBy/UpdatedBy/DeletedBy FKs to Admin come from AuditableEntity
        // and are discovered by convention; OnModelCreating already rewrites every
        // cascade FK to Restrict, and the !IsDeleted query filter is applied by the
        // reflection loop over IAuditable — nothing to configure here.
    }
}
