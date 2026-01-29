using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Travel.WebApi.Domain.Entities;

namespace Travel.WebApi.Data.Configurations;

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("OrderItems");

        builder.HasKey(x => new { x.OrderId, x.ItemId });

        builder.HasOne(x => x.Order)
            .WithMany(o => o.Items)
            .HasForeignKey(x => x.OrderId);

        builder.HasOne(x => x.Item)
            .WithMany(i => i.OrderItems)
            .HasForeignKey(x => x.ItemId);

        builder.Property(i => i.Price)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(i => i.Quantity)
            .IsRequired();
    }
}
