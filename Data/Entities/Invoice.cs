using System.ComponentModel.DataAnnotations;

namespace RentHubPro.Data.Entities;

public class Invoice
{
    public int Id { get; set; }

    public int ContractId { get; set; }
    public Contract? Contract { get; set; }

    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;

    public DateTime DueDate { get; set; }

    [Range(0, 100_000_000)]
    public decimal Amount { get; set; }

    public InvoiceStatus Status { get; set; } = InvoiceStatus.Pending;

    [StringLength(300)]
    public string Note { get; set; } = string.Empty;

    [StringLength(400)]
    public string? ReceiptPath { get; set; }

    public DateTime? ReceiptUploadedAt { get; set; }
}

public class ChatMessage
{
    public long Id { get; set; }

    public string SenderId { get; set; } = string.Empty;
    public ApplicationUser? Sender { get; set; }

    public string RecipientId { get; set; } = string.Empty;
    public ApplicationUser? Recipient { get; set; }

    [Required, StringLength(2000)]
    public string Text { get; set; } = string.Empty;

    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    public bool IsRead { get; set; }
}
