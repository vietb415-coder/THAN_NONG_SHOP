using THAN_NONG_SHOP.Models;

var cases = new[]
{
    new { Name = "THANNONG15 đúng ngưỡng", Code = "THANNONG15", Subtotal = 300_000m, Valid = true, Discount = 45_000m, Total = 285_000m },
    new { Name = "THANNONG15 dưới ngưỡng", Code = "THANNONG15", Subtotal = 298_999m, Valid = false, Discount = 0m, Total = 328_999m },
    new { Name = "THANNONG15 giới hạn giảm", Code = "THANNONG15", Subtotal = 2_000_000m, Valid = true, Discount = 150_000m, Total = 1_880_000m },
    new { Name = "MUAVANG50", Code = "MUAVANG50", Subtotal = 499_000m, Valid = true, Discount = 50_000m, Total = 479_000m },
    new { Name = "FREESHIP", Code = "FREESHIP", Subtotal = 199_000m, Valid = true, Discount = 30_000m, Total = 199_000m },
    new { Name = "LUCKY10", Code = "lucky10", Subtotal = 200_000m, Valid = true, Discount = 20_000m, Total = 210_000m },
    new { Name = "LUCKY30K", Code = "LUCKY30K", Subtotal = 299_000m, Valid = true, Discount = 30_000m, Total = 299_000m },
    new { Name = "Mật ong đủ điều kiện", Code = "MATONG0D", Subtotal = 499_000m, Valid = true, Discount = 0m, Total = 529_000m },
    new { Name = "Mật ong dưới điều kiện", Code = "MATONG0D", Subtotal = 498_999m, Valid = false, Discount = 0m, Total = 528_999m },
    new { Name = "Giỏ quà 500K", Code = "QUA500K", Subtotal = 100_000m, Valid = true, Discount = 0m, Total = 130_000m },
    new { Name = "Giỏ quà đặc biệt", Code = "DACBIET1TR", Subtotal = 100_000m, Valid = true, Discount = 0m, Total = 130_000m },
    new { Name = "Mã không tồn tại", Code = "KHONGCO", Subtotal = 100_000m, Valid = false, Discount = 0m, Total = 130_000m }
};

var failures = 0;
foreach (var test in cases)
{
    var actual = PromotionCatalog.Calculate(test.Code, test.Subtotal);
    var passed = actual.IsValid == test.Valid && actual.DiscountAmount == test.Discount && actual.FinalTotal == test.Total;
    Console.WriteLine($"{(passed ? "PASS" : "FAIL")}  {test.Name}: giảm {actual.DiscountAmount:N0}đ, tổng {actual.FinalTotal:N0}đ");
    if (!passed) failures++;
}

if (failures > 0) throw new Exception($"Có {failures} kiểm thử khuyến mãi thất bại.");
Console.WriteLine($"Hoàn tất: {cases.Length}/{cases.Length} kiểm thử đạt.");

var generatedCodes = Enumerable.Range(0, 1_000).Select(_ => VoucherSecurity.GenerateCode()).ToArray();
if (generatedCodes.Distinct().Count() != generatedCodes.Length || generatedCodes.Any(code => code.Length != 14 || !code.StartsWith("TN")))
    throw new Exception("Sinh mã voucher không đạt yêu cầu duy nhất/định dạng.");
if (VoucherSecurity.HashCode(" tnabc234 ") != VoucherSecurity.HashCode("TNABC234"))
    throw new Exception("Chuẩn hóa mã trước khi băm không nhất quán.");
if (VoucherSecurity.HashCode(generatedCodes[0]).Contains(generatedCodes[0], StringComparison.Ordinal))
    throw new Exception("Bản băm làm lộ mã gốc.");
Console.WriteLine("PASS  1.000 mã ngẫu nhiên không trùng, băm và chuẩn hóa an toàn.");

var configuredReward = new PromotionReward
{
    TemplateCode="DBCONFIG", Title="Configured", Description="Configured", BenefitMessage="Giảm theo cấu hình DB",
    MinimumSubtotal=200_000m, PercentageDiscount=.12m, MaximumDiscount=80_000m, WheelWeight=7, IsActive=true
};
var configuredResult = PromotionCatalog.Calculate(configuredReward, 500_000m);
if (!configuredResult.IsValid || configuredResult.DiscountAmount != 60_000m || configuredResult.FinalTotal != 470_000m)
    throw new Exception("Tính khuyến mãi từ cấu hình database không chính xác.");
configuredReward.IsActive=false;
if (PromotionCatalog.Calculate(configuredReward, 500_000m).IsValid)
    throw new Exception("Phần thưởng đã tắt vẫn được áp dụng.");
Console.WriteLine("PASS  Cấu hình động: phần trăm, mức tối đa và trạng thái bật/tắt.");
