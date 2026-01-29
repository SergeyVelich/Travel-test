using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Travel.WebApi.Domain.Entities;

namespace Travel.WebApi.Data.Configurations;

public class InventoryConfiguration : IEntityTypeConfiguration<Inventory>
{
    public void Configure(EntityTypeBuilder<Inventory> builder)
    {
        builder.ToTable("Inventory");

        builder.HasKey(x => x.ItemId);

        builder.HasOne(x => x.Item)
            .WithOne(p => p.Inventory)
            .HasForeignKey<Inventory>(x => x.ItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(x => x.InStock).IsRequired();
        builder.Property(x => x.Reserved).IsRequired();
        builder.Property(x => x.InTransit).IsRequired();

        builder.Ignore(x => x.Available);
    }
}
