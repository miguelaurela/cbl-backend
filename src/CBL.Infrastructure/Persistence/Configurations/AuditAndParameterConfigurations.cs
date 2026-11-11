using CBL.Domain.Loans;
using CBL.Domain.Parameters;
using CBL.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CBL.Infrastructure.Persistence.Configurations;

internal sealed class LoanDecisionAuditConfiguration : IEntityTypeConfiguration<LoanDecisionAudit>
{
    public void Configure(EntityTypeBuilder<LoanDecisionAudit> builder)
    {
        builder.ToTable("LoanDecisionAudit");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.Decision).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.PreviousStatus).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.DecidedAt).IsRequired().HasColumnType("datetime2(0)");
        builder.Property(a => a.CorrelationId).HasMaxLength(LoanDecisionAudit.CorrelationIdMaxLength);

        builder.HasOne<Loan>().WithMany().HasForeignKey(a => a.LoanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(a => a.AdminUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => a.LoanId);
    }
}

internal sealed class SystemParameterConfiguration : IEntityTypeConfiguration<SystemParameter>
{
    public void Configure(EntityTypeBuilder<SystemParameter> builder)
    {
        builder.ToTable("SystemParameters");
        builder.HasKey(p => p.Key);
        builder.Property(p => p.Key).IsRequired().HasMaxLength(100);
        builder.Property(p => p.Value).IsRequired().HasMaxLength(500);
        builder.Property(p => p.UpdatedAt).IsRequired().HasColumnType("datetime2(0)");

        builder.HasOne<User>().WithMany().HasForeignKey(p => p.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
