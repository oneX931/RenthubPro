using System.ComponentModel.DataAnnotations;

namespace RentHubPro.Data.Entities;

public class Contract
{
    public int Id { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    [Range(0, 100_000_000)]
    public decimal MonthlyAmount { get; set; }

    public int PremiseId { get; set; }
    public Premise? Premise { get; set; }

    public string TenantId { get; set; } = string.Empty;
    public ApplicationUser? Tenant { get; set; }

    public ContractStatus Status { get; set; } = ContractStatus.PendingSignature;

    public DateTime? SignedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();

    public bool IsActiveOn(DateTime date) =>
        Status == ContractStatus.Active && StartDate <= date && date <= EndDate;
}
