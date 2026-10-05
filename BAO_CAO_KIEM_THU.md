# Báo cáo kiểm thử — 04/10/2026

## Kết quả

- .NET SDK 10.0.401; `dotnet build`: 0 lỗi, 12 cảnh báo có sẵn về nullable/tên class.
- `dotnet publish -c Release`: thành công; xác nhận output có appsettings.Production.json.
- EmailEvals: 24 kiểm tra PASS, gồm validation cấu hình, tạo token, pickup file, SMTP loopback, retry, chặn đăng ký/gửi lại khi cấu hình thiếu, chặn cấp thưởng hết hạn và HTTP smoke tests.
- PromotionEvals: 15 kiểm tra/nhóm kiểm tra PASS, gồm 12 trường hợp tính tiền, 1.000 mã ngẫu nhiên, cấu hình reward động, ranh giới hết hạn theo giờ Việt Nam.
- ChatbotEvals: 12 kiểm tra PASS.
- JavaScript promotions.js: `node --check` thành công.
- ZIP: kiểm tra CRC và JSON thành công.

Tổng: 51 dòng PASS trong ba bộ kiểm thử. Đây không phải độ bao phủ toàn bộ chức năng.

## Cách chạy lại

Từ thư mục source có file .csproj, với .NET 10 SDK:

```sh
dotnet build
dotnet run --project tests/EmailEvals/EmailEvals.csproj
dotnet run --project tests/PromotionEvals/PromotionEvals.csproj
dotnet run --project tests/ChatbotEvals/ChatbotEvals.csproj
dotnet publish THAN_NONG_SHOP.csproj -c Release
```

EmailEvals dùng database InMemory, SMTP loopback và thư mục tạm; không dùng mật khẩu thật, không gửi email Internet hoặc sửa database host. HTTP test thay DbContext bằng InMemory và tắt các background worker trong test host.

## Vấn đề đã xác định

Cả hai file cấu hình đính kèm chứa mật khẩu mẫu. Cấu hình SMTP phải được hoàn tất trên host mới có thể thử Gmail thật. Trước đây code lưu yêu cầu vào hàng đợi rồi báo xác nhận dù SMTP chưa sẵn sàng.

Kiểm thử khuyến mãi ban đầu thất bại 9 trường hợp do dựa vào ngày hệ thống sau hạn chiến dịch. Source cũng cấp mã hết hạn ngay khi phát hành. Bản sửa giữ nguyên hạn chiến dịch, chặn cấp mã/quay mới sau hạn và bổ sung kiểm thử thời điểm.

## Chưa kiểm chứng

- Xác thực Gmail thật, nhận thư trong Inbox/Spam, chính sách mạng SMTP của hosting.
- SQL Server thật, migration, đồng thời nhiều tiến trình gửi mail, vòng đời đơn hàng và thanh toán thật.
- Không truy cập hosting hoặc thay đổi database thật.

HTTP smoke thử ban đầu với SQL Server giả không có kết nối làm trang khuyến mãi trả 500 do layout truy vấn DB; sau khi dùng database InMemory trong test host, trang trả 200. Không coi lỗi do fixture này là lỗi production.

## Kết quả chi tiết

### email-tests-final

```text
PASS Reject placeholder password
PASS Normalize grouped Gmail app password
PASS Accept complete Gmail configuration without claiming authentication
PASS Reject implicit SSL port for Gmail SmtpClient
PASS Invalid port is reported without throwing
PASS Reject malformed confirmation base URL
PASS Reject pickup mode in Production
PASS Confirmation token matches hash and queued link
PASS Confirmation token expires after approximately 24 hours
PASS Worker delivers queued email to local pickup file
PASS Worker does not resend delivered messages
PASS SMTP dialogue succeeds with configured EnableSsl=false on local server
PASS Connection failure schedules retry without marking sent
PASS Admin status reports delivery failure
PASS Retry backoff is respected
PASS Block confirmation without queueing a misleading email
PASS Registration does not create a locked account when SMTP configuration is missing
PASS Resend reports unavailable configuration rather than claiming delivery
PASS Expired campaign cannot issue a voucher
PASS Expired campaign cannot consume a spin or reward stock
PASS HTTP render /Account/Login with isolated in-memory database
PASS HTTP render /Account/Register with isolated in-memory database
PASS HTTP render /Home/Promotions with isolated in-memory database
PASS Anonymous visitor cannot read admin email diagnostics
Failures: 0
```

### promotion-tests-final

```text
PASS  THANNONG15 đúng ngưỡng: giảm 45,000đ, tổng 285,000đ
PASS  THANNONG15 dưới ngưỡng: giảm 0đ, tổng 328,999đ
PASS  THANNONG15 giới hạn giảm: giảm 150,000đ, tổng 1,880,000đ
PASS  MUAVANG50: giảm 50,000đ, tổng 479,000đ
PASS  FREESHIP: giảm 30,000đ, tổng 199,000đ
PASS  LUCKY10: giảm 20,000đ, tổng 210,000đ
PASS  LUCKY30K: giảm 30,000đ, tổng 299,000đ
PASS  Mật ong đủ điều kiện: giảm 0đ, tổng 529,000đ
PASS  Mật ong dưới điều kiện: giảm 0đ, tổng 528,999đ
PASS  Giỏ quà 500K: giảm 0đ, tổng 130,000đ
PASS  Giỏ quà đặc biệt: giảm 0đ, tổng 130,000đ
PASS  Mã không tồn tại: giảm 0đ, tổng 130,000đ
Hoàn tất: 12/12 kiểm thử đạt.
PASS  1.000 mã ngẫu nhiên không trùng, băm và chuẩn hóa an toàn.
PASS  Cấu hình động: phần trăm, mức tối đa và trạng thái bật/tắt.
PASS  Expiry boundary in Vietnam timezone, independent of test execution date.
```

### chat-tests-final

```text
PASS  TV must not map to food
PASS  TV phrased naturally must not match passion fruit
PASS  Laptop must not map to food
PASS  Phone must not map to food
PASS  Vietnamese exact product
PASS  Missing accents
PASS  Joined product name
PASS  Minor typo
PASS  English synonym
PASS  Category query
PASS  Out-of-stock product is hidden
PASS  Broad catalog returns only in-stock products
All chatbot catalog evals passed.
```

