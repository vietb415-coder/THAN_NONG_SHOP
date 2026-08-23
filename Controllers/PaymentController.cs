using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PayOS;
using PayOS.Models.Webhooks;
using THAN_NONG_SHOP.Data;
using THAN_NONG_SHOP.Models;
using System.Data;

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
    public async Task<IActionResult> Return(long orderCode)
    {
        var order = await _context.Oders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderCode);
        var verifiedStatus = order?.Status;
        if (string.Equals(verifiedStatus, OrderStatus.Paid, StringComparison.OrdinalIgnoreCase))
        {
            Response.Cookies.Delete(CartSessionKey);
        }

        ViewBag.OrderCode = orderCode;
        ViewBag.PaymentStatus = verifiedStatus ?? "Không tìm thấy đơn hàng";
        return View("Result");
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Cancel(long orderCode)
    {
        ViewBag.OrderCode = orderCode;
        // URL quay về từ trình duyệt không có chữ ký xác thực, nên không được dùng để đổi đơn hoặc hoàn kho.
        // Việc hủy chính thức chỉ được xử lý bởi webhook PayOS đã xác thực hoặc quản trị viên.
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
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var order = await _context.Oders.FirstOrDefaultAsync(o => o.Id == verifiedData.OrderCode);

            if (order == null)
            {
                await transaction.CommitAsync();
                return Ok(new { success = true });
            }

            if (verifiedData.Code == "00")
            {
                // Webhook có thể được PayOS gửi lại nhiều lần; đơn đã thanh toán không cần xử lý lại.
                if (order.Status == OrderStatus.AwaitingPayment)
                {
                    order.Status = OrderStatus.Paid;
                }
            }
            else if (order.Status == OrderStatus.AwaitingPayment)
            {
                await RestoreInventoryAsync(order.Id);
                order.Status = OrderStatus.Cancelled;
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return Ok(new { success = true });
        }
        catch
        {
            return BadRequest(new { success = false, message = "Invalid webhook" });
        }
    }

    private async Task RestoreInventoryAsync(int orderId)
    {
        var quantities = await _context.OderDetails
            .Where(detail => detail.OderId == orderId)
            .GroupBy(detail => detail.ProductId)
            .Select(group => new { ProductId = group.Key, Quantity = group.Sum(detail => detail.Quantity) })
            .ToListAsync();

        var productIds = quantities.Select(item => item.ProductId).ToArray();
        var products = await _context.Products.Where(product => productIds.Contains(product.Id)).ToListAsync();
        foreach (var item in quantities)
        {
            var product = products.FirstOrDefault(candidate => candidate.Id == item.ProductId);
            if (product != null) product.stockQuantity += item.Quantity;
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
