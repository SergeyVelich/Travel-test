using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Travel.WebApi.Domain.Entities;

namespace Travel.WebApi.Data.Configurations;

public class ProcessedMessageConfiguration : IEntityTypeConfiguration<ProcessedMessage>
{
    public void Configure(EntityTypeBuilder<ProcessedMessage> builder)
    {
        builder.ToTable("ProcessedMessages");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.EntityId)
            .IsRequired();

        builder.Property(x => x.MessageId)
            .IsRequired()
            .HasMaxLength(36);

        builder.Property(x => x.MessageType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.ProcessedAt)
            .IsRequired();

        // Unique index for idempotency check in OrderService.SubmitOrderAsync
        builder.HasIndex(x => new { x.MessageId, x.EntityId })
            .IsUnique()
            .HasDatabaseName("IX_ProcessedMessages_MessageId_EntityId_Unique");
    }
}
