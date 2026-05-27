using Microsoft.EntityFrameworkCore;
using RentHubPro.Data;
using RentHubPro.Data.Entities;

namespace RentHubPro.Services;

public class ChatService
{
    private readonly IDbContextFactory<ApplicationDbContext> _factory;
    public ChatService(IDbContextFactory<ApplicationDbContext> factory) => _factory = factory;

    public async Task<ChatMessage> SaveAsync(string senderId, string recipientId, string text)
    {
        await using var db = await _factory.CreateDbContextAsync();

        var msg = new ChatMessage
        {
            SenderId = senderId,
            RecipientId = recipientId,
            Text = text.Trim(),
            SentAt = DateTime.UtcNow,
            IsRead = false
        };
        db.Messages.Add(msg);
        await db.SaveChangesAsync();
        return msg;
    }

    public async Task<List<ChatMessage>> GetConversationAsync(string userA, string userB)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Messages
            .Where(m => (m.SenderId == userA && m.RecipientId == userB) ||
                        (m.SenderId == userB && m.RecipientId == userA))
            .OrderBy(m => m.SentAt)
            .ToListAsync();
    }

    public async Task MarkReadAsync(string ownerId, string otherId)
    {
        await using var db = await _factory.CreateDbContextAsync();

        var unread = await db.Messages
            .Where(m => m.RecipientId == ownerId && m.SenderId == otherId && !m.IsRead)
            .ToListAsync();
        foreach (var m in unread) m.IsRead = true;
        if (unread.Count > 0) await db.SaveChangesAsync();
    }

    public async Task<List<ChatContact>> GetContactsAsync(string userId)
    {
        await using var db = await _factory.CreateDbContextAsync();

        var related = await db.Messages
            .Where(m => m.SenderId == userId || m.RecipientId == userId)
            .OrderByDescending(m => m.SentAt)
            .Include(m => m.Sender)
            .Include(m => m.Recipient)
            .ToListAsync();

        var contacts = new Dictionary<string, ChatContact>();
        foreach (var m in related)
        {
            var other = m.SenderId == userId ? m.Recipient : m.Sender;
            var otherId = m.SenderId == userId ? m.RecipientId : m.SenderId;
            if (other is null || contacts.ContainsKey(otherId)) continue;

            var unread = related.Count(x => x.SenderId == otherId && x.RecipientId == userId && !x.IsRead);
            contacts[otherId] = new ChatContact(otherId, other.FullName, m.Text, m.SentAt, unread);
        }
        return contacts.Values.OrderByDescending(c => c.LastAt).ToList();
    }
}

public record ChatContact(string UserId, string Name, string LastMessage, DateTime LastAt, int Unread);
