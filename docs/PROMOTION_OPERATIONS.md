# Vận hành và kiểm thử khuyến mãi

## Kiểm thử tự động

```powershell
dotnet run --project tests/PromotionEvals/PromotionEvals.csproj -c Release
./scripts/promotion-integration-test.ps1 -Username test_user -Password (Read-Host -AsSecureString)
```

Script tích hợp yêu cầu ứng dụng đang chạy, SQL Server đã áp migration và một tài khoản User thử nghiệm. Script kiểm tra đăng nhập, cấp voucher thật trong database, ràng buộc một lượt quay/ngày và ví voucher. Không truyền mật khẩu hoặc khóa PayOS dưới dạng chuỗi trong lịch sử terminal.

## Kiểm thử PayOS sandbox/thật

1. Dùng một tài khoản User và sản phẩm tồn kho thử nghiệm.
2. Áp voucher, chọn PayOS và xác nhận rằng đơn lưu `Subtotal`, `ShippingFee`, `DiscountAmount`, `TotalPrice` đúng.
3. Trước thanh toán, voucher phải ở trạng thái `Reserved`/“Đang giữ cho đơn”.
4. Thanh toán thành công và chờ webhook: đơn thành “Đã thanh toán”, voucher có sự kiện `Used`.
5. Tạo đơn thứ hai rồi hủy link PayOS: tồn kho sản phẩm và voucher phải được hoàn, có sự kiện `Released`.
6. Kiểm tra số tiền trên link PayOS bằng đúng `TotalPrice` đã giảm.

Không dùng đơn hàng thật để chạy kiểm thử phá hủy. Quản trị viên có thể theo dõi toàn bộ sự kiện tại `/Admin/Promotions`.
