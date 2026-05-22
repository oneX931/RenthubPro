using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RentHubPro.Data.Entities;

namespace RentHubPro.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Premise> Premises => Set<Premise>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<ChatMessage> Messages => Set<ChatMessage>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Premise>(e =>
        {
            e.Property(p => p.PricePerSquareMeter).HasPrecision(18, 2);
            e.HasOne(p => p.Landlord)
                .WithMany(u => u.Premises)
                .HasForeignKey(p => p.LandlordId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(p => p.Status);
        });

        builder.Entity<Contract>(e =>
        {
            e.Property(c => c.MonthlyAmount).HasPrecision(18, 2);
            e.HasOne(c => c.Premise)
                .WithMany(p => p.Contracts)
                .HasForeignKey(c => c.PremiseId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(c => c.Tenant)
                .WithMany(u => u.Contracts)
                .HasForeignKey(c => c.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Invoice>(e =>
        {
            e.Property(i => i.Amount).HasPrecision(18, 2);
            e.HasOne(i => i.Contract)
                .WithMany(c => c.Invoices)
                .HasForeignKey(i => i.ContractId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ChatMessage>(e =>
        {
            e.HasOne(m => m.Sender)
                .WithMany()
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(m => m.Recipient)
                .WithMany()
                .HasForeignKey(m => m.RecipientId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(m => new { m.SenderId, m.RecipientId });
            e.HasIndex(m => m.SentAt);
        });
    }
}
