using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PayOS;
using PayOS.Models.V2.PaymentRequests;
using THAN_NONG_SHOP.Models;

namespace THAN_NONG_SHOP.Services;

public sealed record PaymentProbe(bool Known,bool Paid,decimal Amount);
public sealed class PaymentGateways(IConfiguration config,IHttpClientFactory clients)
{
    public string Setting(string key,string? environment=null) => !string.IsNullOrWhiteSpace(config[key]) ? config[key]! : environment==null?"":Environment.GetEnvironmentVariable(environment)??"";
    public bool IsConfigured(string method)=>method switch {
        "cod"=>true,"payos"=>new[]{Setting("PayOS:ClientId","PAYOS_CLIENT_ID"),Setting("PayOS:ApiKey","PAYOS_API_KEY"),Setting("PayOS:ChecksumKey","PAYOS_CHECKSUM_KEY")}.All(x=>!string.IsNullOrWhiteSpace(x)),
        "vnpay"=>new[]{"TmnCode","HashSecret"}.All(k=>!string.IsNullOrWhiteSpace(Setting("VNPay:"+k))),_=>false};
    public PayOSClient PayOS()=>new(Setting("PayOS:ClientId","PAYOS_CLIENT_ID"),Setting("PayOS:ApiKey","PAYOS_API_KEY"),Setting("PayOS:ChecksumKey","PAYOS_CHECKSUM_KEY"));
    public static string Hmac(string data,string key,bool sha512=false)=>Convert.ToHexString(sha512?HMACSHA512.HashData(Encoding.UTF8.GetBytes(key),Encoding.UTF8.GetBytes(data)):HMACSHA256.HashData(Encoding.UTF8.GetBytes(key),Encoding.UTF8.GetBytes(data))).ToLowerInvariant();
    public static bool Matches(string actual,string expected)
    {try {return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(actual),Convert.FromHexString(expected));}catch{return false;}}
    public static string VnpData(IEnumerable<KeyValuePair<string,string>> values)=>string.Join("&",values.Where(k=>k.Key.StartsWith("vnp_",StringComparison.Ordinal)&&k.Key is not ("vnp_SecureHash" or "vnp_SecureHashType")&&!string.IsNullOrEmpty(k.Value)).OrderBy(k=>k.Key,StringComparer.Ordinal).Select(k=>$"{WebUtility.UrlEncode(k.Key)}={WebUtility.UrlEncode(k.Value)}"));
    public bool VerifyVnp(Dictionary<string,string> p)=>IsConfigured("vnpay") && p.GetValueOrDefault("vnp_TmnCode")==Setting("VNPay:TmnCode") && Matches(p.GetValueOrDefault("vnp_SecureHash")??"",Hmac(VnpData(p),Setting("VNPay:HashSecret"),true));
    public static string Value(JsonElement p,string name)=>p.TryGetProperty(name,out var v)?v.ToString():"";
    public async Task<string> CreateAsync(Oder o,string ip,CancellationToken ct)
    {
        var site=Setting("Site:PublicBaseUrl").TrimEnd('/');if(!Uri.TryCreate(site,UriKind.Absolute,out _))throw new InvalidOperationException("Thiếu địa chỉ website.");
        var amount=decimal.ToInt64(o.TotalPrice);
        if(o.TotalPrice!=amount)throw new InvalidOperationException("Cổng thanh toán chỉ nhận số tiền VND nguyên.");
        if(o.PaymentMethod=="payos") {
            var result=await PayOS().PaymentRequests.CreateAsync(new CreatePaymentLinkRequest {OrderCode=o.PayOSOrderCode!.Value,Amount=checked((int)amount),Description=$"Don hang {o.Id}",ReturnUrl=site+"/Payment/Return",CancelUrl=site+"/Payment/Cancel",ExpiredAt=new DateTimeOffset(DateTime.SpecifyKind(o.PaymentExpiresAt!.Value,DateTimeKind.Utc)).ToUnixTimeSeconds()});
            return result.CheckoutUrl;
        }
        if(o.PaymentMethod=="vnpay") {
            var now=new DateTimeOffset(DateTime.SpecifyKind(o.PaymentExpiresAt!.Value.AddMinutes(-15),DateTimeKind.Utc)).ToOffset(TimeSpan.FromHours(7));
            var p=new Dictionary<string,string> { ["vnp_Version"]="2.1.0",["vnp_Command"]="pay",["vnp_TmnCode"]=Setting("VNPay:TmnCode"),["vnp_Amount"]=(amount*100).ToString(CultureInfo.InvariantCulture),["vnp_CurrCode"]="VND",["vnp_TxnRef"]=o.PaymentReference!,["vnp_OrderInfo"]=$"Thanh toan don {o.Id}",["vnp_OrderType"]="other",["vnp_Locale"]="vn",["vnp_ReturnUrl"]=site+"/Payment/VNPayReturn",["vnp_IpAddr"]=ip,["vnp_CreateDate"]=now.ToString("yyyyMMddHHmmss"),["vnp_ExpireDate"]=now.AddMinutes(15).ToString("yyyyMMddHHmmss") };
            var query=VnpData(p);return (config["VNPay:PaymentUrl"]??"https://sandbox.vnpayment.vn/paymentv2/vpcpay.html")+"?"+query+"&vnp_SecureHash="+Hmac(query,Setting("VNPay:HashSecret"),true);
        }
        throw new InvalidOperationException("Phương thức thanh toán không được hỗ trợ.");
    }
    private async Task<JsonElement> PostAsync(string url,object body,CancellationToken ct)
    {
        using var response=await clients.CreateClient("Payments").PostAsJsonAsync(url,body,ct);response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken:ct);
    }
    public async Task<PaymentProbe> QueryAsync(Oder o,CancellationToken ct)
    {
        if(!IsConfigured(o.PaymentMethod))return new(false,false,0);
        if(o.PaymentMethod=="payos") {
            var result=await PayOS().PaymentRequests.GetAsync(o.PayOSOrderCode!.Value);
            return new(true,result.Status.ToString().Equals("PAID",StringComparison.OrdinalIgnoreCase),result.Amount);
        }
        if(o.PaymentMethod=="vnpay") {
            var request=Guid.NewGuid().ToString("N");var now=DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).ToString("yyyyMMddHHmmss");
            // OrderDate is created by the app in its local timezone. Persisted UTC deadline is authoritative for the create time.
            var created=new DateTimeOffset(DateTime.SpecifyKind(o.PaymentExpiresAt!.Value.AddMinutes(-15),DateTimeKind.Utc)).ToOffset(TimeSpan.FromHours(7)).ToString("yyyyMMddHHmmss");
            var merchant=Setting("VNPay:TmnCode");var info=$"Truy van don {o.Id}";var ip=config["VNPay:ServerIp"]??"127.0.0.1";
            var raw=$"{request}|2.1.0|querydr|{merchant}|{o.PaymentReference}|{created}|{now}|{ip}|{info}";
            var r=await PostAsync(config["VNPay:QueryUrl"]??"https://sandbox.vnpayment.vn/merchant_webapi/api/transaction",new {vnp_RequestId=request,vnp_Version="2.1.0",vnp_Command="querydr",vnp_TmnCode=merchant,vnp_TxnRef=o.PaymentReference,vnp_TransactionDate=created,vnp_CreateDate=now,vnp_IpAddr=ip,vnp_OrderInfo=info,vnp_SecureHash=Hmac(raw,Setting("VNPay:HashSecret"),true)},ct);
            var keys=new[]{"vnp_ResponseId","vnp_Command","vnp_ResponseCode","vnp_Message","vnp_TmnCode","vnp_TxnRef","vnp_Amount","vnp_BankCode","vnp_PayDate","vnp_TransactionNo","vnp_TransactionType","vnp_TransactionStatus","vnp_OrderInfo","vnp_PromotionCode","vnp_PromotionAmount"};
            var valid=Matches(Value(r,"vnp_SecureHash"),Hmac(string.Join("|",keys.Select(k=>Value(r,k))),Setting("VNPay:HashSecret"),true)) && Value(r,"vnp_TmnCode")==merchant && Value(r,"vnp_TxnRef")==o.PaymentReference && Value(r,"vnp_ResponseCode")=="00";
            return new(valid,valid && Value(r,"vnp_TransactionStatus")=="00",decimal.TryParse(Value(r,"vnp_Amount"),out var amount)?amount/100:0);
        }
        return new(false,false,0);
    }
}
