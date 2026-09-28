using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using THAN_NONG_SHOP.Data;
using THAN_NONG_SHOP.Models;

namespace THAN_NONG_SHOP.Services;

public interface IChatbotService
{
    Task<ChatResponse> ReplyAsync(ChatRequest request, CancellationToken cancellationToken);
    Task<bool> SetFeedbackAsync(ChatFeedbackRequest request, CancellationToken cancellationToken);
    Task<bool> RequestHumanAsync(Guid conversationId, CancellationToken cancellationToken);
}

public sealed class ChatbotService(IHttpClientFactory clients, THAN_NONG_SHOP_DbContext db,
    IHttpContextAccessor contextAccessor, IOptions<ChatbotOptions> options,
    ILogger<ChatbotService> logger) : IChatbotService
{
    private readonly ChatbotOptions _options = options.Value;
    private HttpContext HttpContext => contextAccessor.HttpContext!;
    private string? UserName => HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

    public async Task<ChatResponse> ReplyAsync(ChatRequest request, CancellationToken ct)
    {
        var conversation = await GetConversationAsync(request.ConversationId, ct);
        db.ChatMessages.Add(new ChatStoredMessage { ConversationId = conversation.Id, Role = "user", Content = Limit(request.Message, 4000) });
        var products = await db.Products.AsNoTracking().Include(p => p.Category).OrderBy(p => p.Name).Take(80).ToListAsync(ct);
        var knowledge = await db.ChatKnowledge.AsNoTracking().Where(k => k.IsActive).OrderBy(k => k.Category).Take(60).ToListAsync(ct);
        var suggestions = FindProducts(request.Message, products);
        var orders = await FindOrdersAsync(request.Message, ct);
        var hasKnowledgeMatch = HasKnowledgeMatch(request.Message, knowledge);
        var confirmedOutOfCatalog = IsSpecificProductRequest(request.Message) && suggestions.Count == 0 && !hasKnowledgeMatch;
        var needsHuman = ShouldHandoff(request.Message);
        if (needsHuman) conversation.NeedsHuman = true;

        string reply;
        var isAi = false;
        if (IsUnsafe(request.Message))
            reply = GetUnsafeReply(request.Message);
        else if (confirmedOutOfCatalog)
            reply = GetProductNotFoundReply(request.Message);
        else if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            var suggestedIds = suggestions.Select(product => product.Id).ToHashSet();
            var aiProducts = IsCatalogRequest(request.Message)
                ? products
                : products.Where(product => suggestedIds.Contains(product.Id)).ToList();
            var aiReply = await AskAiAsync(request.Message, conversation.Id, aiProducts, knowledge, orders, ct);
            isAi = !string.IsNullOrWhiteSpace(aiReply);
            reply = aiReply ?? BuildFallback(request.Message, suggestions, orders, knowledge);
        }
        else reply = BuildFallback(request.Message, suggestions, orders, knowledge);

        if (needsHuman)
        {
            conversation.HandoffSummary = $"Khách yêu cầu hỗ trợ nhân viên. Tin nhắn gần nhất: {Limit(request.Message, 500)}";
            reply += "\n\n" + GetHandoffReply(request.Message);
        }
        var storedReply = new ChatStoredMessage { ConversationId = conversation.Id, Role = "assistant", Content = Limit(reply, 4000), IsAi = isAi };
        db.ChatMessages.Add(storedReply);
        conversation.LastMessageAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return new ChatResponse(reply, isAi, conversation.Id, storedReply.Id, conversation.NeedsHuman, suggestions, orders);
    }

    public async Task<bool> SetFeedbackAsync(ChatFeedbackRequest request, CancellationToken ct)
    {
        var message = await db.ChatMessages.Include(m => m.Conversation).FirstOrDefaultAsync(m => m.Id == request.MessageId && m.Role == "assistant", ct);
        if (message?.Conversation == null || !CanAccess(message.Conversation)) return false;
        message.Helpful = request.Helpful;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> RequestHumanAsync(Guid id, CancellationToken ct)
    {
        var conversation = await db.ChatConversations.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (conversation == null || !CanAccess(conversation)) return false;
        conversation.NeedsHuman = true;
        conversation.HandoffSummary ??= "Khách chủ động yêu cầu gặp nhân viên.";
        await db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<ChatConversation> GetConversationAsync(Guid? requestedId, CancellationToken ct)
    {
        var visitorId = GetVisitorId();
        if (requestedId.HasValue)
        {
            var existing = await db.ChatConversations.FirstOrDefaultAsync(c => c.Id == requestedId, ct);
            if (existing != null && CanAccess(existing)) return existing;
        }
        var conversation = new ChatConversation { UserName = UserName, VisitorId = visitorId };
        db.ChatConversations.Add(conversation);
        return conversation;
    }

    private bool CanAccess(ChatConversation conversation) => !string.IsNullOrWhiteSpace(UserName)
        ? conversation.UserName == UserName : conversation.VisitorId == GetVisitorId();

    private string GetVisitorId()
    {
        const string cookieName = "THAN_NONG_CHAT_VISITOR";
        if (HttpContext.Request.Cookies.TryGetValue(cookieName, out var id) && id?.Length == 32) return id;
        id = Guid.NewGuid().ToString("N");
        HttpContext.Response.Cookies.Append(cookieName, id, new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Lax, Secure = HttpContext.Request.IsHttps, IsEssential = true, MaxAge = TimeSpan.FromDays(90) });
        return id;
    }

    private async Task<string?> AskAiAsync(string currentMessage, Guid conversationId, List<Product> products,
        List<ChatKnowledge> knowledge, List<ChatOrderSummary> orders, CancellationToken ct)
    {
        var catalog = string.Join("\n", products.Select(p => $"- #{p.Id} {p.Name}; giá {p.price:N0}đ; tồn {p.stockQuantity}; {p.Category?.Name}; {p.Description}"));
        var faq = string.Join("\n", knowledge.Select(k => $"- [{k.Category}] {k.Title}: {k.Content}"));
        var orderText = orders.Count == 0 ? "Không có dữ liệu đơn được phép xem." : string.Join("\n", orders.Select(o => $"- Đơn #{o.Id}, {o.OrderDate:dd/MM/yyyy}, {o.TotalPrice:N0}đ, {o.Status}"));
        var storedHistory = await db.ChatMessages.AsNoTracking()
            .Where(message => message.ConversationId == conversationId && (message.Role == "user" || message.Role == "assistant"))
            .OrderByDescending(message => message.CreatedAt)
            .Take(10)
            .OrderBy(message => message.CreatedAt)
            .Select(message => new { role = message.Role, content = message.Content })
            .ToListAsync(ct);
        var input = storedHistory.Select(message => new { message.role, content = Limit(message.content, 1200) }).Cast<object>().ToList();
        input.Add(new { role = "user", content = currentMessage });
        var payload = new
        {
            model = _options.Model, store = false, max_output_tokens = 650,
            text = new
            {
                format = new
                {
                    type = "json_schema",
                    name = "customer_support_reply",
                    strict = true,
                    schema = new
                    {
                        type = "object",
                        properties = new
                        {
                            reply = new { type = "string", description = "Câu trả lời cuối cùng gửi cho khách hàng." }
                        },
                        required = new[] { "reply" },
                        additionalProperties = false
                    }
                }
            },
            instructions = "Bạn là trợ lý bán hàng và CSKH của THẦN NÔNG SHOP, một cửa hàng nông sản. " +
                "Trả lời đúng ngôn ngữ của khách, thân thiện, ngắn gọn và thực tế. " +
                "QUY TẮC BẮT BUỘC: Chỉ xác nhận hoặc đề xuất sản phẩm xuất hiện trong mục SẢN PHẨM PHÙ HỢP. " +
                "Nếu mục đó trống, phải nói shop chưa tìm thấy/không bán sản phẩm khách hỏi; tuyệt đối không thay bằng một sản phẩm không liên quan. " +
                "Không tự bịa sản phẩm, giá, tồn kho, công dụng y tế, chính sách hoặc trạng thái đơn. " +
                "Không tuyên bố đã thêm giỏ hàng hay đã sửa/hủy đơn. Không xin mật khẩu, OTP, số thẻ hoặc dữ liệu nhạy cảm. " +
                "Chỉ đọc các đơn trong mục ĐƠN HÀNG ĐƯỢC PHÉP XEM. Với khiếu nại, hoàn tiền, nguy cơ sức khỏe hoặc thông tin không đủ, đề nghị gặp nhân viên. " +
                "Nếu tư vấn thực phẩm, không đưa chẩn đoán y khoa; chỉ cung cấp thông tin chung và khuyên hỏi chuyên gia khi cần.\n\n" +
                "CHÍNH SÁCH ĐÃ XÁC THỰC:\n" + _options.StorePolicies + "\n\nKHO KIẾN THỨC ĐÃ XÁC THỰC:\n" + faq +
                "\n\nSẢN PHẨM PHÙ HỢP ĐÃ XÁC THỰC:\n" + (string.IsNullOrWhiteSpace(catalog) ? "(không có sản phẩm phù hợp)" : catalog) +
                "\n\nĐƠN HÀNG ĐƯỢC PHÉP XEM:\n" + orderText,
            input, safety_identifier = GetVisitorId()
        };
        try
        {
            var client = clients.CreateClient("OpenAI");
            using var message = new HttpRequestMessage(HttpMethod.Post, "responses");
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
            message.Content = JsonContent.Create(payload);
            using var response = await client.SendAsync(message, ct);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = Limit(await response.Content.ReadAsStringAsync(ct), 1000);
                logger.LogWarning("OpenAI chatbot request failed with HTTP {StatusCode}: {ErrorBody}",
                    (int)response.StatusCode, errorBody);
                return null;
            }
            using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var result = ParseStructuredReply(ExtractText(json.RootElement));
            if (string.IsNullOrWhiteSpace(result))
                logger.LogWarning("OpenAI chatbot returned no output text. Response ID: {ResponseId}",
                    json.RootElement.TryGetProperty("id", out var id) ? id.GetString() : "unknown");
            return string.IsNullOrWhiteSpace(result) ? null : result;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            logger.LogWarning(ex, "OpenAI chatbot request failed.");
            return null;
        }
    }

    private async Task<List<ChatOrderSummary>> FindOrdersAsync(string message, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(UserName) || !IsOrderRequest(message)) return [];
        return await db.Oders.AsNoTracking().Where(o => o.UserName == UserName).OrderByDescending(o => o.OrderDate).Take(5)
            .Select(o => new ChatOrderSummary(o.Id, o.OrderDate, o.TotalPrice, o.Status)).ToListAsync(ct);
    }

    private static List<ChatProductSuggestion> FindProducts(string message, List<Product> products)
    {
        var ranked = CatalogMatcher.Find(message, products, IsCatalogRequest(message));
        return ranked.Select(p => new ChatProductSuggestion(p.Id, p.Name, p.price, p.stockQuantity, p.ImageUrl)).ToList();
    }

    private static string BuildFallback(string message, List<ChatProductSuggestion> products, List<ChatOrderSummary> orders, List<ChatKnowledge> knowledge)
    {
        var text = message.ToLowerInvariant();
        var searchWords = SearchTokens(message);
        if (orders.Count > 0) return GetOrdersReply(message);
        if (IsOrderRequest(message)) return GetNoOrdersReply(message);
        if (products.Count == 0 && IsCatalogRequest(message)) return GetEmptyCatalogReply(message);
        var matched = knowledge.Select(k => new { Item = k, Score = SearchTokens(k.Title + " " + k.Category).Intersect(searchWords).Count() })
            .Where(x => x.Score > 0).OrderByDescending(x => x.Score).Select(x => x.Item).FirstOrDefault();
        if (matched != null && DetectLanguage(message) == "vi") return matched.Content;
        if (IsPaymentRequest(message)) return GetPaymentReply(message);
        if (IsShippingRequest(message)) return GetShippingReply(message);
        if (products.Count > 0) return GetProductReply(message);
        if (IsSpecificProductRequest(message)) return GetProductNotFoundReply(message);
        var normalized = Normalize(message);
        if (searchWords.Length > 0 && ContainsAny(normalized, " co ", "mua", "shop co", "ban co", "buy", "looking for"))
            return GetProductNotFoundReply(message);
        if (IsGreeting(message)) return GetGreetingReply(message);
        return GetUnknownReply(message);
    }

    private static string GetProductReply(string message) => DetectLanguage(message) switch
    {
        "zh" => "这是商店目录中的一些商品，请查看下面的选项和库存状态。",
        "ko" => "매장에서 판매하는 상품입니다. 아래에서 상품과 재고 상태를 확인해 주세요.",
        "ja" => "ショップで取り扱っている商品です。以下の商品と在庫状況をご確認ください。",
        "en" => "Here are some products in our catalog. Please check the options and stock status below.",
        _ => "Đây là một số sản phẩm trong danh mục của shop. Bạn xem sản phẩm và trạng thái tồn kho bên dưới nhé."
    };

    private static string GetUnknownReply(string message) => DetectLanguage(message) switch
    {
        "zh" => "目前的信息不足以准确回答。请详细说明您的需求，或选择联系工作人员。",
        "ko" => "정확히 답변하기에는 정보가 부족합니다. 필요한 내용을 자세히 알려주시거나 직원 연결을 선택해 주세요.",
        "ja" => "正確に回答するための情報が不足しています。ご希望を詳しく入力するか、スタッフへの連絡を選択してください。",
        "en" => "I don't have enough information to answer accurately. Please describe what you need or choose Contact staff.",
        _ => "Mình chưa có đủ thông tin để trả lời chính xác. Bạn có thể nói rõ nhu cầu hoặc chọn Gặp nhân viên nhé."
    };

    private static string GetEmptyCatalogReply(string message) => DetectLanguage(message) switch
    {
        "zh" => "目前商店目录中没有可显示的商品。请稍后再试或联系工作人员。",
        "ko" => "현재 매장 상품 목록에 표시할 상품이 없습니다. 나중에 다시 시도하거나 직원에게 문의해 주세요.",
        "ja" => "現在、商品一覧に表示できる商品がありません。後でもう一度お試しいただくか、スタッフへお問い合わせください。",
        "en" => "There are currently no products available to display in the catalog. Please try again later or contact a staff member.",
        _ => "Hiện danh mục chưa có sản phẩm để hiển thị. Bạn hãy thử lại sau hoặc liên hệ nhân viên nhé."
    };

    private static string GetHandoffReply(string message) => DetectLanguage(message) switch
    {
        "zh" => "已记录您联系工作人员的请求。请选择下方的 Facebook、Zalo 或 Discord 继续。",
        "ko" => "직원 연결 요청이 접수되었습니다. 아래에서 Facebook, Zalo 또는 Discord를 선택해 주세요.",
        "ja" => "スタッフへの連絡依頼を受け付けました。下のFacebook、Zalo、Discordから選択してください。",
        "en" => "Your request to contact a staff member has been recorded. Choose Facebook, Zalo, or Discord below to continue.",
        _ => "Mình đã ghi nhận yêu cầu gặp nhân viên. Bạn hãy chọn Facebook, Zalo hoặc Discord bên dưới để tiếp tục."
    };

    private static string GetUnsafeReply(string message) => DetectLanguage(message) switch
    {
        "zh" => "我无法协助处理此内容。如需解决订单问题，请选择联系工作人员。",
        "ko" => "이 내용은 도와드릴 수 없습니다. 주문 문제 해결이 필요하면 직원 연결을 선택해 주세요.",
        "ja" => "この内容には対応できません。注文に関する問題は、スタッフへの連絡を選択してください。",
        "en" => "I can't assist with that content. For order-related issues, please choose Contact staff.",
        _ => "Mình không thể hỗ trợ nội dung này. Nếu cần giải quyết vấn đề về đơn hàng, bạn hãy chọn Gặp nhân viên nhé."
    };

    private static string GetOrdersReply(string message) => DetectLanguage(message) switch
    {
        "zh" => "以下是您最近的订单。请查看下方订单卡片中的状态。",
        "ko" => "최근 주문 내역입니다. 아래 주문 카드에서 상태를 확인해 주세요.",
        "ja" => "最近の注文です。以下の注文カードで状況をご確認ください。",
        "en" => "Here are your latest orders. You can check their status in the order cards below.",
        _ => "Đây là các đơn gần nhất của bạn. Bạn có thể xem trạng thái trong thẻ đơn hàng bên dưới."
    };

    private static string GetNoOrdersReply(string message) => DetectLanguage(message) switch
    {
        "zh" => "找不到可查看的订单。请先登录，并确认您使用的是下单时的账户。",
        "ko" => "조회할 수 있는 주문이 없습니다. 로그인한 뒤 주문할 때 사용한 계정인지 확인해 주세요.",
        "ja" => "確認できる注文がありません。ログインして、注文時と同じアカウントかご確認ください。",
        "en" => "I couldn't find any orders you can view. Please sign in and make sure you're using the account that placed the order.",
        _ => "Mình chưa tìm thấy đơn hàng bạn có thể xem. Bạn hãy đăng nhập và kiểm tra đúng tài khoản đã đặt hàng nhé."
    };

    private static string GetPaymentReply(string message) => DetectLanguage(message) switch
    {
        "zh" => "商店支持货到付款（COD）和通过 PayOS 在线付款。请在结账时选择合适的方式。",
        "ko" => "착불 결제(COD)와 PayOS 온라인 결제를 지원합니다. 결제 단계에서 원하는 방법을 선택해 주세요.",
        "ja" => "代金引換（COD）とPayOSでのオンライン決済に対応しています。購入手続きでお選びください。",
        "en" => "The shop supports cash on delivery (COD) and online payment through PayOS. Choose your preferred method at checkout.",
        _ => "Shop hỗ trợ thanh toán khi nhận hàng (COD) và thanh toán trực tuyến qua PayOS. Bạn chọn phương thức phù hợp ở bước thanh toán nhé."
    };

    private static string GetShippingReply(string message) => DetectLanguage(message) switch
    {
        "zh" => "配送时间和费用取决于收货地址。请告诉我您的省市，以便提供更准确的信息。",
        "ko" => "배송 시간과 비용은 배송지에 따라 달라집니다. 정확한 안내를 위해 시/도를 알려 주세요.",
        "ja" => "配送日数と送料はお届け先によって異なります。正確にご案内するため、都道府県を教えてください。",
        "en" => "Delivery time and fees depend on the destination. Please tell me your province or city for an accurate estimate.",
        _ => "Thời gian và phí giao hàng phụ thuộc địa chỉ nhận hàng. Bạn cho mình biết tỉnh/thành phố để được tư vấn chính xác nhé."
    };

    private static string GetGreetingReply(string message) => DetectLanguage(message) switch
    {
        "zh" => "您好！我可以帮助您了解商品、付款、配送或查询订单。",
        "ko" => "안녕하세요! 상품, 결제, 배송 안내와 주문 조회를 도와드릴 수 있습니다.",
        "ja" => "こんにちは！商品、決済、配送のご案内や注文確認をお手伝いできます。",
        "en" => "Hello! I can help with products, payments, shipping, or checking your orders.",
        _ => "Xin chào! Mình có thể tư vấn sản phẩm, thanh toán, giao hàng hoặc kiểm tra đơn cho bạn."
    };

    private static string GetProductNotFoundReply(string message) => DetectLanguage(message) switch
    {
        "zh" => "目前在商店目录中找不到您需要的商品。为避免造成误解，我不会推荐不相关的商品。",
        "ko" => "현재 매장 상품 목록에서 요청하신 상품을 찾을 수 없습니다. 혼동을 피하기 위해 관련 없는 상품은 추천하지 않겠습니다.",
        "ja" => "現在、商品一覧にご希望の商品が見つかりません。混乱を避けるため、無関係な商品はおすすめしません。",
        "en" => "I couldn't find that product in the shop catalog. I won't suggest unrelated products to avoid confusion.",
        _ => "Hiện mình chưa tìm thấy sản phẩm bạn cần trong danh mục của shop. Mình sẽ không đề xuất sản phẩm khác để tránh làm bạn nhầm nhé."
    };

    private static string DetectLanguage(string text)
    {
        if (text.Any(c => c is >= '\uAC00' and <= '\uD7AF')) return "ko";
        if (text.Any(c => c is >= '\u3040' and <= '\u30FF')) return "ja";
        if (text.Any(c => c is >= '\u3400' and <= '\u9FFF')) return "zh";
        if (text.Any(c => "ăâđêôơưáàảãạấầẩẫậắằẳẵặéèẻẽẹếềểễệíìỉĩịóòỏõọốồổỗộớờởỡợúùủũụứừửữựýỳỷỹỵ".Contains(char.ToLowerInvariant(c)))) return "vi";
        var normalized = Normalize(text);
        if (ContainsAny(normalized, " the ", " what ", " how ", " product ", " products ", " order ", " shipping ",
            " hello ", " hi ", " please ", " can ", " could ", " want ", " need ", " payment ", " delivery ",
            " price ", " stock ", " available ", " refund ", " staff ", " help ", " thanks ", " thank ")) return "en";
        return "vi";
    }

    private static bool IsOrderRequest(string text) => ContainsAny(text,
        "đơn", "order", "mua hàng", "giao tới đâu", "订单", "訂單", "주문", "注文");

    private static bool IsPaymentRequest(string text) => ContainsAny(text,
        "thanh toán", "trả tiền", "payment", "pay ", "付款", "支付", "결제", "지불", "支払い", "決済");

    private static bool IsShippingRequest(string text) => ContainsAny(text,
        "giao hàng", "phí giao", "ship", "delivery", "shipping", "配送", "运费", "運費", "배송", "送料");

    private static bool IsGreeting(string text) => ContainsAny(text,
        "chào", "hello", " hi", "hi ", "你好", "您好", "안녕", "こんにちは", "こんばんは");

    private static bool IsCatalogRequest(string text)
    {
        var normalized = Normalize(text);
        return ContainsAny(normalized,
                   "danh sach san pham", "san pham nao", "co gi ban", "ban chay",
                   "what products", "what do you sell", "products do you have", "show products", "product list")
            || ContainsAny(text,
                "你们有什么产品", "你們有什麼產品", "有什么产品", "有什麼產品", "卖什么", "賣什麼",
                "어떤 제품", "무슨 제품", "판매하나요", "판매합니까",
                "どんな商品", "何の商品", "商品があります", "何を売");
    }

    private static bool IsSpecificProductRequest(string text)
    {
        if (IsCatalogRequest(text) || IsOrderRequest(text) || IsPaymentRequest(text) ||
            IsShippingRequest(text) || IsGreeting(text) || ShouldHandoff(text)) return false;

        var normalized = Normalize(text);
        if (ContainsAny(normalized,
            " mua ", " tim ", " can ", " muon ", " co ban ", " shop co ", " gia ", " bao nhieu ",
            " buy ", " looking for ", " do you sell ", " have a ", " price ", " available ")) return true;

        // Một tên mặt hàng ngắn như "tivi", "laptop" hoặc "cà rốt" cũng được coi là
        // truy vấn sản phẩm. Điều này giúp trả lời không có hàng thay vì để AI đoán bừa.
        var tokens = SearchTokens(text);
        return tokens.Length is > 0 and <= 4 && !ContainsAny(normalized,
            " cam on ", " thank ", " tam biet ", " goodbye ", " help ", " ho tro ");
    }

    private static bool HasKnowledgeMatch(string message, List<ChatKnowledge> knowledge)
    {
        var tokens = SearchTokens(message);
        if (tokens.Length == 0) return false;
        return knowledge.Any(item => SearchTokens(item.Title + " " + item.Category + " " + item.Content)
            .Intersect(tokens).Count() >= Math.Min(2, tokens.Length));
    }

    private static bool ShouldHandoff(string text) => ContainsAny(text,
        "gặp nhân viên", "khiếu nại", "hoàn tiền", "lừa đảo", "bực", "tức giận", "không hài lòng",
        "contact staff", "human agent", "representative", "complaint", "refund", "scam", "angry", "not satisfied",
        "联系工作人员", "聯繫工作人員", "人工客服", "投诉", "投訴", "退款", "诈骗", "詐騙", "不满意", "不滿意",
        "직원 연결", "상담원", "불만", "환불", "사기", "화가", "만족하지",
        "スタッフ", "担当者", "苦情", "返金", "詐欺", "不満");
    private static bool IsUnsafe(string text) => ContainsAny(text, "bỏ qua hướng dẫn", "ignore previous", "system prompt", "api key", "mật khẩu của người khác");
    private static bool ContainsAny(string text, params string[] values) => values.Any(v => text.Contains(v, StringComparison.OrdinalIgnoreCase));
    private static readonly HashSet<string> SearchStopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "ai", "ban", "minh", "toi", "shop", "co", "khong", "la", "gi", "nao", "mot", "con", "cai",
        "cho", "voi", "va", "hay", "nhe", "duoc", "muon", "can", "hien", "gio", "nay", "do", "ve"
    };
    private static string[] SearchTokens(string value, bool removeStopWords = true)
    {
        var tokens = Normalize(value).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(w => w.Length >= 2 && (!removeStopWords || !SearchStopWords.Contains(w))).Distinct().ToArray();
        return tokens;
    }
    private static string Normalize(string value)
    {
        var decomposed = (value ?? string.Empty).ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            builder.Append(char.IsLetterOrDigit(character) ? character : ' ');
        }
        return " " + string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries)) + " ";
    }
    private static string Limit(string value, int length) { value = value.Trim(); return value[..Math.Min(value.Length, length)]; }
    private static string ExtractText(JsonElement root)
    {
        if (root.TryGetProperty("output_text", out var direct)) return direct.GetString() ?? string.Empty;
        if (!root.TryGetProperty("output", out var output)) return string.Empty;
        foreach (var item in output.EnumerateArray()) if (item.TryGetProperty("content", out var content)) foreach (var part in content.EnumerateArray())
            if (part.TryGetProperty("type", out var type) && type.GetString() == "output_text" && part.TryGetProperty("text", out var text)) return text.GetString() ?? string.Empty;
        return string.Empty;
    }

    private static string ParseStructuredReply(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        try
        {
            using var json = JsonDocument.Parse(value);
            return json.RootElement.TryGetProperty("reply", out var reply) ? reply.GetString() ?? string.Empty : string.Empty;
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }
}
