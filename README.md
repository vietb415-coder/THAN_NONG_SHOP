# THẦN NÔNG SHOP

Ứng dụng thương mại điện tử nông sản xây dựng bằng ASP.NET Core MVC, Entity Framework Core và SQL Server.

Các chức năng chính gồm catalog, giỏ hàng, COD/PayOS, quản trị, chatbot CSKH đa ngôn ngữ và đánh giá từ người mua đã xác minh.

## Yêu cầu

- .NET SDK 10
- SQL Server hoặc SQL Server Express
- Tài khoản PayOS nếu kiểm thử thanh toán trực tuyến
- OpenAI API key nếu kiểm thử chatbot AI

## Cấu hình Development

Sao chép `appsettings.Development.example.json` thành `appsettings.Development.json`, sau đó điền connection string và các khóa dùng riêng trên máy. File `appsettings.Development.json` đã được `.gitignore` loại trừ và không được gửi cho tester hoặc commit vào Git.

Các tên cấu hình chính:

```json
{
  "ConnectionStrings": {
    "THAN_NONG_SHOP_ConnectionString": "YOUR_SQL_SERVER_CONNECTION_STRING"
  },
  "Chatbot": {
    "ApiKey": "YOUR_OPENAI_API_KEY"
  },
  "PayOS": {
    "ClientId": "YOUR_PAYOS_CLIENT_ID",
    "ApiKey": "YOUR_PAYOS_API_KEY",
    "ChecksumKey": "YOUR_PAYOS_CHECKSUM_KEY"
  }
}
```

## Cấu hình Production

Không lưu credential trong source. Thiết lập các biến môi trường sau tại máy chủ:

```text
ConnectionStrings__THAN_NONG_SHOP_ConnectionString
Chatbot__ApiKey
PAYOS_CLIENT_ID
PAYOS_API_KEY
PAYOS_CHECKSUM_KEY
```

Nếu thiếu connection string, ứng dụng chủ động từ chối khởi động. OpenAI và PayOS là tùy chọn; chức năng tương ứng sẽ không hoạt động nếu thiếu khóa.

PayOS tự đối soát đơn chờ thanh toán theo hai cấu hình trong `appsettings.json`:

- `PendingOrderTimeoutMinutes`: thời gian tối đa chờ thanh toán, mặc định 30 phút.
- `ReconciliationIntervalMinutes`: chu kỳ đối soát, mặc định 5 phút.

## Khởi tạo và chạy

```powershell
dotnet tool restore
dotnet restore
dotnet build
dotnet run
```

Khi khởi động, ứng dụng tự áp dụng EF Core migrations và seed dữ liệu mẫu. SQL Server phải sẵn sàng trước khi chạy ứng dụng.

## Đánh giá sản phẩm

- Chỉ tài khoản có đơn `Đã hoàn thành` chứa sản phẩm mới được gửi đánh giá.
- Mỗi tài khoản có một đánh giá trên mỗi sản phẩm và có thể cập nhật hoặc xóa.
- Điểm trung bình và nhãn đã mua hàng hiển thị công khai trên trang chi tiết.

## Nguyên tắc chatbot

Chatbot tìm sản phẩm trong database trước khi gọi AI. Với yêu cầu cụ thể không khớp catalog (ví dụ hỏi mua TV), chatbot trả lời shop không bán và không đề xuất nông sản không liên quan. AI chỉ nhận danh sách sản phẩm đã được bộ tìm kiếm xác thực, không có quyền tự tạo sản phẩm, giá, tồn kho hoặc trạng thái đơn.

Địa chỉ mặc định:

- HTTP: `http://localhost:5114`
- HTTPS: `https://localhost:7034`

## Chạy smoke test

Khởi động ứng dụng trong terminal thứ nhất:

```powershell
dotnet run --launch-profile https
```

Trong terminal thứ hai:

```powershell
.\scripts\smoke-test.ps1
```

Script kiểm tra build, credential trong cấu hình chia sẻ, các trang công khai, phân quyền admin/checkout và anti-forgery của logout. Checklist nghiệp vụ đầy đủ nằm tại `docs/TEST_CHECKLIST.md`.

Chạy bộ eval riêng cho khả năng tìm kiếm catalog của chatbot:

```powershell
dotnet run --project tests\ChatbotEvals\ChatbotEvals.csproj
```

Bộ eval bắt buộc các truy vấn TV, laptop và điện thoại không được trả về nông sản; đồng thời kiểm tra tiếng Việt không dấu, viết liền, lỗi chính tả nhẹ và synonym tiếng Anh.

## Bàn giao cho tester

Không gửi các thư mục/file sau:

```text
.vs/
bin/
obj/
.tmp/
appsettings.Development.json
```

Gửi riêng cho tester URL môi trường test và tài khoản test qua kênh bảo mật. Không sử dụng credential production cho môi trường test.
