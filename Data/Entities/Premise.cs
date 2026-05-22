using System.ComponentModel.DataAnnotations;

namespace RentHubPro.Data.Entities;

public class Premise
{
    public int Id { get; set; }

    [Required, StringLength(150)]
    public string Title { get; set; } = string.Empty;

    public PremiseType Type { get; set; } = PremiseType.Office;

    [Range(1, 1_000_000)]
    public double Area { get; set; }

    public int Floor { get; set; }

    [Range(0.01, 1_000_000)]
    public decimal PricePerSquareMeter { get; set; }

    [StringLength(4000)]
    public string Description { get; set; } = string.Empty;

    [StringLength(300)]
    public string Address { get; set; } = string.Empty;

    public string PhotoPaths { get; set; } = string.Empty;

    public PremiseStatus Status { get; set; } = PremiseStatus.Available;

    public bool IsRemovedByAdmin { get; set; }

    [StringLength(500)]
    public string? AdminRemovalReason { get; set; }

    public DateTime? RemovedAt { get; set; }

    public bool EditedAfterRemoval { get; set; }

    public string LandlordId { get; set; } = string.Empty;
    public ApplicationUser? Landlord { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Contract> Contracts { get; set; } = new List<Contract>();

    public IEnumerable<string> Photos =>
        string.IsNullOrWhiteSpace(PhotoPaths)
            ? Array.Empty<string>()
            : PhotoPaths.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
