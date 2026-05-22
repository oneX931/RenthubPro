using Microsoft.AspNetCore.Identity;

namespace RentHubPro.Data.Entities;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;

    public AppTheme Theme { get; set; } = AppTheme.Light;

    public bool IsBlocked { get; set; }

    [System.ComponentModel.DataAnnotations.StringLength(500)]
    public string? BlockReason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Premise> Premises { get; set; } = new List<Premise>();
    public ICollection<Contract> Contracts { get; set; } = new List<Contract>();
}
