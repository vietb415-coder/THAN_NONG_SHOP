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
    private readonly ILogger<PaymentController> _logger;

    public PaymentController(THAN_NONG_SHOP_DbContext context, IConfiguration configuration, ILogger<PaymentController> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Return(long orderCode)
    {
        var order = await FindOrderAsync(orderCode, asTracking: false);
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
    public async Task<IActionResult> Cancel(long orderCode)
    {
        ViewBag.OrderCode = orderCode;

        try
        {
            // Không tin trực tiếp query string từ trình duyệt. Hỏi lại API PayOS
            // để chắc chắn link thực sự đã bị hủy rồi mới hoàn kho.
            var payOS = CreateClient();
            var paymentLink = await payOS.PaymentRequests.GetAsync(orderCode);
            var payOSStatus = paymentLink.Status.ToString();

            if (string.Equals(payOSStatus, "CANCELLED", StringComparison.OrdinalIgnoreCase))
            {
                await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
                var order = await FindOrderAsync(orderCode, asTracking: true);

                // Chỉ hoàn đơn đang chờ thanh toán để callback hoặc webhook
                // gửi lại cũng không thể cộng tồn kho hai lần.
                if (order != null && order.Status == OrderStatus.AwaitingPayment)
                {
                    await RestoreInventoryAsync(order.Id);
                    order.Status = OrderStatus.Cancelled;
                    await _context.SaveChangesAsync();
                }

                await transaction.CommitAsync();
                ViewBag.PaymentStatus = OrderStatus.Cancelled;
            }
            else
            {
                ViewBag.PaymentStatus = payOSStatus;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không thể xác minh trạng thái hủy PayOS cho orderCode {OrderCode}.", orderCode);
            var order = await FindOrderAsync(orderCode, asTracking: false);
            ViewBag.PaymentStatus = order?.Status ?? "Không thể xác minh trạng thái thanh toán";
        }

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
            var order = await FindOrderAsync(verifiedData.OrderCode, asTracking: true);

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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Webhook PayOS không hợp lệ hoặc không xử lý được.");
            return BadRequest(new { success = false, message = "Invalid webhook" });
        }
    }

    private Task<Oder?> FindOrderAsync(long orderCode, bool asTracking)
    {
        var orders = asTracking ? _context.Oders.AsQueryable() : _context.Oders.AsNoTracking();
        return orders.FirstOrDefaultAsync(order =>
            order.PayOSOrderCode == orderCode ||
            (order.PayOSOrderCode == null && order.Id == orderCode));
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
