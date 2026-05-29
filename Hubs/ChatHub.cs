using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using RentHubPro.Services;

namespace RentHubPro.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly ChatService _chat;

    public ChatHub(ChatService chat) => _chat = chat;

    public async Task SendMessage(string recipientId, string text)
    {
        var senderId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(senderId) || string.IsNullOrWhiteSpace(text)) return;
        if (text.Length > 2000) text = text[..2000];

        var saved = await _chat.SaveAsync(senderId, recipientId, text);

        var payload = new
        {
            id = saved.Id,
            senderId = saved.SenderId,
            recipientId = saved.RecipientId,
            text = saved.Text,
            sentAt = saved.SentAt
        };

        await Clients.User(recipientId).SendAsync("ReceiveMessage", payload);
        await Clients.User(senderId).SendAsync("ReceiveMessage", payload);
    }
}
