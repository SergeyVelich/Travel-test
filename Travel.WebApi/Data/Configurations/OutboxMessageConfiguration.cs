using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Travel.WebApi.Domain.Entities;

namespace Travel.WebApi.Data.Configurations;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Type)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(x => x.Payload)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.ProcessedAt);

        builder.Property(x => x.Error)
            .HasMaxLength(2000);

        builder.Property(x => x.RetryCount)
            .IsRequired()
            .HasDefaultValue(0);

        // Optimized index for OutboxProcessorService query:
        // WHERE ProcessedAt IS NULL AND RetryCount < MaxRetryCount ORDER BY CreatedAt
        builder.HasIndex(x => new { x.ProcessedAt, x.RetryCount, x.CreatedAt })
            .HasDatabaseName("IX_OutboxMessages_Unprocessed_Query")
            .HasFilter("\"ProcessedAt\" IS NULL");
    }
}
