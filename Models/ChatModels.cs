namespace THAN_NONG_SHOP.Models;

public sealed class ChatRequest
{
    public string Message { get; set; } = string.Empty;
    public List<ChatMessage> History { get; set; } = [];
    public Guid? ConversationId { get; set; }
}

public sealed class ChatMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public sealed record ChatResponse(string Reply, bool IsAi, Guid ConversationId, int MessageId,
    bool NeedsHuman, IReadOnlyList<ChatProductSuggestion> Products, IReadOnlyList<ChatOrderSummary> Orders);

public sealed record ChatProductSuggestion(int Id, string Name, decimal Price, int Stock, string? ImageUrl);
public sealed record ChatOrderSummary(int Id, DateTime OrderDate, decimal TotalPrice, string Status);

public sealed class ChatFeedbackRequest
{
    public int MessageId { get; set; }
    public bool Helpful { get; set; }
}

public sealed class ChatbotOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-5-mini";
    public string FacebookUrl { get; set; } = "#";
    public string ZaloUrl { get; set; } = "#";
    public string DiscordUrl { get; set; } = "#";
    public string StorePolicies { get; set; } = "Thanh toán: COD hoặc PayOS. Phí và thời gian giao hàng phụ thuộc địa chỉ nhận hàng.";
}
