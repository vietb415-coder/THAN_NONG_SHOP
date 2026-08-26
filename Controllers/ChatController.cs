using Microsoft.AspNetCore.Mvc;
using THAN_NONG_SHOP.Models;
using THAN_NONG_SHOP.Services;
using Microsoft.AspNetCore.RateLimiting;

namespace THAN_NONG_SHOP.Controllers;

[ApiController]
[Route("api/chat")]
[EnableRateLimiting("chat")]
public sealed class ChatController(IChatbotService chatbot) : ControllerBase
{
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<ChatResponse>> Send([FromBody] ChatRequest request, CancellationToken cancellationToken)
    {
        request.Message = request.Message?.Trim() ?? string.Empty;
        if (request.Message.Length is < 1 or > 1000)
            return BadRequest(new { message = "Tin nhắn phải có từ 1 đến 1.000 ký tự." });
        if (request.History.Count > 20)
            request.History = request.History.TakeLast(20).ToList();
        return Ok(await chatbot.ReplyAsync(request, cancellationToken));
    }

    [HttpPost("feedback")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Feedback([FromBody] ChatFeedbackRequest request, CancellationToken cancellationToken)
        => await chatbot.SetFeedbackAsync(request, cancellationToken) ? NoContent() : NotFound();

    [HttpPost("handoff/{conversationId:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Handoff(Guid conversationId, CancellationToken cancellationToken)
        => await chatbot.RequestHumanAsync(conversationId, cancellationToken) ? NoContent() : NotFound();
}
