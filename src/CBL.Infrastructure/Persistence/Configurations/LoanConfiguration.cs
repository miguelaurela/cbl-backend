using CBL.Domain.Loans;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBL.Infrastructure.Persistence.Configurations;

internal sealed class LoanConfiguration : IEntityTypeConfiguration<Loan>
{
    public void Configure(EntityTypeBuilder<Loan> builder)
    {
        builder.ToTable("Loans");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();

        builder.Property(l => l.Amount).IsRequired().HasPrecision(18, 2);
        builder.Property(l => l.TermMonths).IsRequired();
        builder.Property(l => l.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(l => l.CreatedAt).IsRequired().HasColumnType("datetime2(0)");
        builder.Property(l => l.DecidedAt).HasColumnType("datetime2(0)");

        // Concurrencia optimista con columna propia (no rowversion): también la comprueba el proveedor InMemory.
        builder.Property(l => l.Version).IsRequired().IsConcurrencyToken().ValueGeneratedNever();

        builder.HasOne(l => l.Borrower)
            .WithMany()
            .HasForeignKey(l => l.BorrowerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.DecidedBy)
            .WithMany()
            .HasForeignKey(l => l.DecidedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => new { l.BorrowerId, l.Status });
        builder.HasIndex(l => new { l.Status, l.CreatedAt });
    }
}
