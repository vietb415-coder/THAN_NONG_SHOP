using System.Data;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PayOS.Models.Webhooks;
using THAN_NONG_SHOP.Data;
using THAN_NONG_SHOP.Models;
using THAN_NONG_SHOP.Services;

namespace THAN_NONG_SHOP.Controllers;

public class PaymentController(THAN_NONG_SHOP_DbContext db,PaymentGateways gateways,OrderLifecycle lifecycle,ILogger<PaymentController> log):Controller
{
    [HttpGet]
    public Task<IActionResult> Return(long orderCode)=>Result(orderCode,null);
    [HttpGet]
    public Task<IActionResult> Cancel(long orderCode)=>Result(orderCode,null);
    [HttpGet]
    public Task<IActionResult> MoMoReturn(string orderId)=>Result(null,orderId);
    [HttpGet]
    public Task<IActionResult> VNPayReturn(string vnp_TxnRef)=>Result(null,vnp_TxnRef);
    private async Task<IActionResult> Result(long? code,string? reference)
    {
        var name=User.FindFirstValue(ClaimTypes.NameIdentifier);
        var order=await db.Oders.AsNoTracking().FirstOrDefaultAsync(o=>((code!=null && o.PayOSOrderCode==code)||(reference!=null && o.PaymentReference==reference)));
        if(order==null || (name!=null?order.UserName!=name:order.UserName!=null || !GuestOrderAccess.Contains(HttpContext,order.Id)))return NotFound();ViewBag.OrderId=order.Id;ViewBag.PaymentStatus=order.Status;return View("Result");
    }
    [AllowAnonymous,HttpPost,IgnoreAntiforgeryToken]
    public async Task<IActionResult> Webhook([FromBody]Webhook body,CancellationToken ct)
    {
        try {
            if(!gateways.IsConfigured("payos"))return BadRequest();
            var verified=await gateways.PayOS().Webhooks.VerifyAsync(body);
            return await RecordAsync("payos",null,verified.OrderCode,verified.Amount,verified.Code=="00",ct)?Ok(new {success=true}):BadRequest();
        } catch(Exception ex){log.LogWarning(ex,"PayOS webhook bị từ chối.");return BadRequest();}
    }
    [AllowAnonymous,HttpPost,IgnoreAntiforgeryToken]
    public async Task<IActionResult> MoMoIpn([FromBody]JsonElement body,CancellationToken ct)
    {
        if(!gateways.VerifyMomo(body) || PaymentGateways.Value(body,"requestId")!=PaymentGateways.Value(body,"orderId") || !decimal.TryParse(PaymentGateways.Value(body,"amount"),out var amount))return BadRequest();
        return await RecordAsync("momo",PaymentGateways.Value(body,"orderId"),null,amount,PaymentGateways.Value(body,"resultCode")=="0",ct)?NoContent():BadRequest();
    }
    [AllowAnonymous,HttpGet]
    public async Task<IActionResult> VNPayIpn(CancellationToken ct)
    {
        if(Request.Query.Any(p=>p.Value.Count!=1))return Json(new {RspCode="97",Message="Invalid signature"});
        var p=Request.Query.ToDictionary(p=>p.Key,p=>p.Value.ToString());
        if(!gateways.VerifyVnp(p))return Json(new {RspCode="97",Message="Invalid signature"});
        if(!decimal.TryParse(p.GetValueOrDefault("vnp_Amount"),out var amount))return Json(new {RspCode="04",Message="Invalid amount"});
        var ok=await RecordAsync("vnpay",p.GetValueOrDefault("vnp_TxnRef"),null,amount/100,p.GetValueOrDefault("vnp_ResponseCode")=="00" && p.GetValueOrDefault("vnp_TransactionStatus")=="00",ct);
        return Json(new {RspCode=ok?"00":"04",Message=ok?"Confirm success":"Invalid order or amount"});
    }
    private async Task<bool> RecordAsync(string method,string? reference,long? code,decimal amount,bool paid,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var o=await db.Oders.FirstOrDefaultAsync(o=>o.PaymentMethod==method && ((reference!=null && o.PaymentReference==reference)||(code!=null && o.PayOSOrderCode==code)),ct);
        if(o==null || o.TotalPrice!=amount)return false;
        if(paid)await lifecycle.PaidAsync(o,amount,ct);
        // Failed/cancelled online attempts retain the reservation until its 15-minute deadline.
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return true;
    }
}
