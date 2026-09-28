using System.ComponentModel.DataAnnotations;

namespace THAN_NONG_SHOP.Models;

public sealed class ChatKnowledge
{
    public int Id { get; set; }
    [Required, StringLength(120)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(4000)] public string Content { get; set; } = string.Empty;
    [StringLength(80)] public string Category { get; set; } = "Chung";
    public bool IsActive { get; set; } = true;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class ChatConversation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [StringLength(100)] public string? UserName { get; set; }
    [StringLength(100)] public string? VisitorId { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;
    public bool NeedsHuman { get; set; }
    [StringLength(2000)] public string? HandoffSummary { get; set; }
    public ICollection<ChatStoredMessage> Messages { get; set; } = [];
}

public sealed class ChatStoredMessage
{
    public int Id { get; set; }
    public Guid ConversationId { get; set; }
    public ChatConversation? Conversation { get; set; }
    [Required, StringLength(16)] public string Role { get; set; } = string.Empty;
    [Required, StringLength(4000)] public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool? Helpful { get; set; }
    public bool IsAi { get; set; }
}

public sealed class ChatAdminDashboard
{
    public int ConversationsToday { get; set; }
    public int NeedsHuman { get; set; }
    public int Helpful { get; set; }
    public int NotHelpful { get; set; }
    public List<ChatConversation> Conversations { get; set; } = [];
    public ChatConversation? Selected { get; set; }
}
