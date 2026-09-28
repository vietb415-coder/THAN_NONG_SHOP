# Checklist kiểm thử THẦN NÔNG SHOP

## Chuẩn bị

- Dùng database và PayOS sandbox/test riêng.
- Chuẩn bị một tài khoản User, một tài khoản Admin và một tài khoản User bị khóa.
- Ghi lại tồn kho ban đầu của sản phẩm dùng để đặt hàng.

## Tài khoản và phân quyền

- Đăng ký với dữ liệu hợp lệ và đăng nhập thành công.
- Từ chối username dưới 3 ký tự, email sai, số điện thoại sai và mật khẩu dưới 8 ký tự.
- Từ chối username trùng.
- Tài khoản bị khóa không đăng nhập được.
- User không truy cập được `/Admin` và các controller con.
- Admin truy cập được quản lý sản phẩm, danh mục, đơn hàng, người dùng và chatbot.
- Logout bằng nút trên giao diện thành công; GET trực tiếp `/Account/Logout` không thực hiện đăng xuất.

## Sản phẩm và giỏ hàng

- Tìm kiếm, lọc danh mục và khoảng giá hoạt động.
- Xem chi tiết sản phẩm tồn tại; ID không tồn tại trả về 404.
- Không thêm số lượng âm, bằng 0 hoặc vượt tồn kho.
- Cập nhật và xóa sản phẩm trong giỏ hoạt động.
- Giá và tổng tiền checkout lấy theo giá hiện tại trong database, không tin dữ liệu phía trình duyệt.

## Checkout COD

- Từ chối tên người nhận ngoài 2–100 ký tự.
- Chỉ nhận số điện thoại Việt Nam dạng `0` và 9 chữ số tiếp theo.
- Từ chối địa chỉ ngoài 5–500 ký tự và phương thức thanh toán giả mạo.
- Đặt đơn thành công làm giảm tồn kho đúng số lượng.
- Hai người cùng mua sản phẩm gần hết hàng không làm tồn kho âm.
- Admin hủy đơn chỉ hoàn kho một lần.

## PayOS

- Tạo link PayOS thành công, đúng số tiền và đúng mã đơn.
- Callback trình duyệt không tự đánh dấu đã thanh toán nếu chưa có xác minh PayOS.
- Webhook hợp lệ cập nhật đơn thành `Đã thanh toán`.
- Webhook sai chữ ký bị từ chối HTTP 400.
- Gửi lặp webhook thành công/hủy không cập nhật hoặc hoàn kho hai lần.
- Hủy link PayOS làm đơn chuyển `Đã hủy` và hoàn kho đúng một lần.
- Đơn quá thời gian cấu hình được worker đối soát, hủy link và hoàn kho.
- Đơn đã thanh toán nhưng webhook đến chậm được worker cập nhật `Đã thanh toán`.
- `/Payment/Return?orderCode=<ID đơn COD>` không tiết lộ trạng thái đơn COD.

## Chatbot

- Không có OpenAI key: chatbot vẫn trả lời từ knowledge base/fallback và ứng dụng không crash.
- Có OpenAI key: hội thoại hoạt động và không hiển thị key ở response/log.
- User chỉ xem được thông tin đơn của chính mình.
- Visitor không đọc được hội thoại của visitor khác.
- Rate limit chặn gửi quá nhiều yêu cầu chat trong một phút.
- Admin xem, xử lý handoff và quản lý knowledge base được.
- Hỏi “Tôi muốn mua TV/tivi” phải trả lời shop không bán và không hiển thị thẻ đậu, rau hoặc sản phẩm khác.
- Hỏi tên sản phẩm đang có phải trả đúng giá và tồn kho từ database.
- Hỏi một sản phẩm không tồn tại không được làm AI tự bịa sản phẩm thay thế.
- Prompt yêu cầu bỏ qua hướng dẫn hoặc tiết lộ API key bị từ chối.

## Đánh giá sản phẩm

- Khách chưa đăng nhập không thể gửi đánh giá.
- User chưa có đơn hoàn thành chứa sản phẩm không thể đánh giá.
- User đã mua và hoàn thành đơn gửi được đánh giá 1–5 sao.
- Từ chối nội dung dưới 3 hoặc trên 1000 ký tự và số sao ngoài 1–5.
- Một user không tạo được hai đánh giá cho cùng sản phẩm; lần gửi sau cập nhật bản cũ.
- User chỉ sửa/xóa được đánh giá của chính mình.
- Điểm trung bình và tổng số đánh giá hiển thị đúng.

## Quản trị và file ảnh

- CRUD danh mục và sản phẩm hoạt động, có anti-forgery.
- Từ chối file không phải ảnh, file quá dung lượng và file giả phần mở rộng.
- Xóa/sửa ảnh không thể tác động file ngoài thư mục sản phẩm.
- Không thể khóa hoặc hạ quyền tài khoản admin đang đăng nhập.

## Hoàn tất

- Chạy `dotnet build` không có error.
- Chạy `scripts/smoke-test.ps1` và tất cả kiểm tra đều PASS.
- Không có credential thật trong `appsettings.json`, `appsettings.Production.json` hoặc file example.
- Không đóng gói `.vs`, `bin`, `obj`, `.tmp` hoặc `appsettings.Development.json`.
