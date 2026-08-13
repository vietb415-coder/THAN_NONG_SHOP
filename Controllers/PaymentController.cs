using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PayOS;
using PayOS.Models.Webhooks;
using THAN_NONG_SHOP.Data;

namespace THAN_NONG_SHOP.Controllers;

public class PaymentController : Controller
{
    private const string CartSessionKey = "CartItems";
    private readonly THAN_NONG_SHOP_DbContext _context;
    private readonly IConfiguration _configuration;

    public PaymentController(THAN_NONG_SHOP_DbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Return(long orderCode, string? status)
    {
        if (string.Equals(status, "PAID", StringComparison.OrdinalIgnoreCase))
        {
            Response.Cookies.Delete(CartSessionKey);
        }

        ViewBag.OrderCode = orderCode;
        ViewBag.PaymentStatus = status;
        return View("Result");
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Cancel(long orderCode)
    {
        var order = await _context.Oders.FindAsync((int)orderCode);
        if (order != null && order.Status == "Chờ thanh toán PayOS")
        {
            order.Status = "Đã hủy thanh toán";
            await _context.SaveChangesAsync();
        }

        ViewBag.OrderCode = orderCode;
        ViewBag.PaymentStatus = "CANCELLED";
        return View("Result");
    }

    [AllowAnonymous]
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Webhook([FromBody] Webhook webhookData)
    {
        try
        {
            var payOS = CreateClient();
            var verifiedData = await payOS.Webhooks.VerifyAsync(webhookData);
            var order = await _context.Oders.FindAsync((int)verifiedData.OrderCode);

            if (order != null && verifiedData.Code == "00")
            {
                order.Status = "Đã thanh toán";
                await _context.SaveChangesAsync();
            }

            return Ok(new { success = true });
        }
        catch
        {
            return BadRequest(new { success = false, message = "Invalid webhook" });
        }
    }

    private PayOSClient CreateClient()
    {
        var clientId = _configuration["PayOS:ClientId"];
        var apiKey = _configuration["PayOS:ApiKey"];
        var checksumKey = _configuration["PayOS:ChecksumKey"];
        if (string.IsNullOrWhiteSpace(clientId)) clientId = Environment.GetEnvironmentVariable("PAYOS_CLIENT_ID");
        if (string.IsNullOrWhiteSpace(apiKey)) apiKey = Environment.GetEnvironmentVariable("PAYOS_API_KEY");
        if (string.IsNullOrWhiteSpace(checksumKey)) checksumKey = Environment.GetEnvironmentVariable("PAYOS_CHECKSUM_KEY");

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(checksumKey))
        {
            throw new InvalidOperationException("PayOS is not configured.");
        }

        return new PayOSClient(clientId, apiKey, checksumKey);
    }
}
