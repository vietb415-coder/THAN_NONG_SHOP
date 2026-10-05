# Kiểm tra các case Fail trong TEST_CASE_APP_THANNONG.xlsx — 05/10/2026

Đọc trực tiếp file Excel người dùng cung cấp: sheet1 có 18 dòng Fail, sheet2 có 69 dòng Fail; tổng 87 dòng tương ứng 70 mã case khác nhau. Giữ nguyên kết quả trong Excel gốc. Nhiều dòng được ghi Fail do tài khoản chưa kích hoạt nên chưa thực hiện được các bước phía sau.

## Lỗi tái hiện trước khi sửa

| Case | Trước sửa | Sau sửa |
|---|---|---|
| TC_78 | Controller từ chối hủy đơn Đang đóng gói; không hoàn tồn. | Cho hủy khi chưa có dòng hàng Đang giao/Đã giao; hoàn đúng lô, không hoàn hai lần; đơn online được đánh dấu đối soát hoàn tiền. |
| TC_85 | Server đảo khoảng giá nhưng ô nhập trên trang vẫn giữ Từ > Đến. | Đảo giá trị ngay trong ô nhập trước khi gọi AJAX; kiểm tra tính hợp lệ của form. |
| TC_116 | Edge ở chiều rộng 390px xác nhận nút chat chồng lên điều khiển checkout. | Nội dung trang cuộn trong vùng phía trên dải 80px dành cho nút chat; không chồng lên điều khiển ở 390px và 1280px. |

Bằng chứng trước sửa: [controller](test-results/workbook-before.txt), [trình duyệt](test-results/browser-before.txt). Log trình duyệt trước sửa ghi nhận hai Fail giao diện rồi dừng vì selector header của bộ test không duy nhất; selector đã được sửa, lần chạy cuối hoàn tất toàn bộ luồng. Các case đang đạt được giữ nguyên logic.

## Kiểm tra sau sửa

| Bộ kiểm tra | Số kiểm tra đạt | Log |
|---|---:|---|
| Controller, tồn kho, tài khoản, callback/worker | 74 | [Log](test-results/workbook-after.txt) |
| Edge: AJAX, giỏ, COD, đăng nhập, phân quyền, chat | 20 | [Log](test-results/browser-after.txt) |
| Email, SMTP loopback, render HTTP | 24 | [Log](test-results/workbook-email.txt) |
| Voucher và công thức khuyến mãi | 15 | [Log](test-results/workbook-promotions.txt) |
| Tìm sản phẩm trong chatbot | 12 | [Log](test-results/workbook-chatbot.txt) |

Build thành công: [log build](test-results/workbook-build.txt). Ảnh checkout: [điện thoại 390px](test-results/checkout-390.png), [desktop 1280px](test-results/checkout-1280.png).

## Phạm vi và giới hạn

Test dùng database InMemory riêng, bỏ các worker gửi email tự động ở test HTTP, dùng khóa bảo vệ tạm trong bộ nhớ. Test trình duyệt chặn yêu cầu ra website ngoài. Không gửi thư tới khách, không tạo giao dịch payOS/VNPay thật, không sửa database đang dùng hoặc các khóa cấu hình người dùng.

TC_03 đạt tạo token, gửi local pickup và SMTP loopback; chưa xác minh Gmail nhận thư thực tế. Callback VNPay được ký bằng khóa giả trong test, chưa xác minh thanh toán thật. InMemory không chứng minh transaction rollback, unique index hoặc khóa đồng thời của SQL Server. Upload dùng fixture ảnh/video tổng hợp, chưa thay thế kiểm thử thủ công với file thực tế. Những giới hạn này không được ghi thành Pass tích hợp trong Excel.

## Đối chiếu 70 mã case từng Fail

| Case | Chức năng | Bằng chứng hiện tại và phạm vi |
|---|---|---|
| TC_03 | Email xác nhận | Đạt logic token, pickup, SMTP loopback; chưa xác minh hộp thư thật. |
| TC_04 | Đăng ký khi đã đăng nhập | Đạt kiểm tra tài khoản/validation/controller; đăng nhập SĐT, header, phân quyền và đăng xuất được kiểm tra thêm trên Edge. Unique index SQL Server chưa được kiểm thử. |
| TC_05 | Đăng nhập thành công | Đạt kiểm tra tài khoản/validation/controller; đăng nhập SĐT, header, phân quyền và đăng xuất được kiểm tra thêm trên Edge. Unique index SQL Server chưa được kiểm thử. |
| TC_06 | Tài khoản chưa kích hoạt | Đạt kiểm tra tài khoản/validation/controller; đăng nhập SĐT, header, phân quyền và đăng xuất được kiểm tra thêm trên Edge. Unique index SQL Server chưa được kiểm thử. |
| TC_07 | Đăng nhập bằng SĐT | Đạt kiểm tra tài khoản/validation/controller; đăng nhập SĐT, header, phân quyền và đăng xuất được kiểm tra thêm trên Edge. Unique index SQL Server chưa được kiểm thử. |
| TC_08 | Truy cập chức năng theo Role | Đạt kiểm tra tài khoản/validation/controller; đăng nhập SĐT, header, phân quyền và đăng xuất được kiểm tra thêm trên Edge. Unique index SQL Server chưa được kiểm thử. |
| TC_17 | Đơn thanh toán thành công | Đạt callback/worker giả lập; chưa kiểm tra cổng thật và transaction SQL Server. |
| TC_18 | Đơn thanh toán thất bại | Đạt callback/worker giả lập; chưa kiểm tra cổng thật và transaction SQL Server. |
| TC_19 | Tìm kiếm theo tên | Đạt bộ lọc/tìm kiếm controller; AJAX và reset trên Edge; không sửa lại logic đang đạt. |
| TC_20 | Không có kết quả | Đạt bộ lọc/tìm kiếm controller; AJAX và reset trên Edge; không sửa lại logic đang đạt. |
| TC_21 | Tìm theo mô tả | Đạt bộ lọc/tìm kiếm controller; AJAX và reset trên Edge; không sửa lại logic đang đạt. |
| TC_22 | Kết hợp nhiều bộ lọc | Đạt bộ lọc/tìm kiếm controller; AJAX và reset trên Edge; không sửa lại logic đang đạt. |
| TC_23 | Xóa bộ lọc | Đạt bộ lọc/tìm kiếm controller; AJAX và reset trên Edge; không sửa lại logic đang đạt. |
| TC_24 | Cập nhật không reload toàn trang | Đạt bộ lọc/tìm kiếm controller; AJAX và reset trên Edge; không sửa lại logic đang đạt. |
| TC_25 | Guest thêm giỏ | Đạt giỏ hàng, số lượng, tồn kho hoặc validation trên dữ liệu test; đồng bộ dùng InMemory. |
| TC_26 | Cập nhật số lượng | Đạt giỏ hàng, số lượng, tồn kho hoặc validation trên dữ liệu test; đồng bộ dùng InMemory. |
| TC_27 | Xóa sản phẩm | Đạt giỏ hàng, số lượng, tồn kho hoặc validation trên dữ liệu test; đồng bộ dùng InMemory. |
| TC_28 | Số lượng âm | Đạt giỏ hàng, số lượng, tồn kho hoặc validation trên dữ liệu test; đồng bộ dùng InMemory. |
| TC_29 | Guest refresh trình duyệt | Đạt giỏ hàng, số lượng, tồn kho hoặc validation trên dữ liệu test; đồng bộ dùng InMemory. |
| TC_30 | Customer đồng bộ DB | Đạt giỏ hàng, số lượng, tồn kho hoặc validation trên dữ liệu test; đồng bộ dùng InMemory. |
| TC_31 | Email/SĐT trùng | Đạt kiểm tra tài khoản/validation/controller; đăng nhập SĐT, header, phân quyền và đăng xuất được kiểm tra thêm trên Edge. Unique index SQL Server chưa được kiểm thử. |
| TC_32 | Mật khẩu yếu | Đạt kiểm tra tài khoản/validation/controller; đăng nhập SĐT, header, phân quyền và đăng xuất được kiểm tra thêm trên Edge. Unique index SQL Server chưa được kiểm thử. |
| TC_33 | Mật khẩu nhập lại sai | Đạt kiểm tra tài khoản/validation/controller; đăng nhập SĐT, header, phân quyền và đăng xuất được kiểm tra thêm trên Edge. Unique index SQL Server chưa được kiểm thử. |
| TC_34 | Email không hợp lệ | Đạt kiểm tra tài khoản/validation/controller; đăng nhập SĐT, header, phân quyền và đăng xuất được kiểm tra thêm trên Edge. Unique index SQL Server chưa được kiểm thử. |
| TC_35 | SĐT không hợp lệ | Đạt kiểm tra tài khoản/validation/controller; đăng nhập SĐT, header, phân quyền và đăng xuất được kiểm tra thêm trên Edge. Unique index SQL Server chưa được kiểm thử. |
| TC_36 | Khoảng trắng đầu/cuối | Đạt kiểm tra tài khoản/validation/controller; đăng nhập SĐT, header, phân quyền và đăng xuất được kiểm tra thêm trên Edge. Unique index SQL Server chưa được kiểm thử. |
| TC_37 | Email phân biệt hoa thường | Đạt kiểm tra tài khoản/validation/controller; đăng nhập SĐT, header, phân quyền và đăng xuất được kiểm tra thêm trên Edge. Unique index SQL Server chưa được kiểm thử. |
| TC_38 | Sai mật khẩu | Đạt kiểm tra tài khoản/validation/controller; đăng nhập SĐT, header, phân quyền và đăng xuất được kiểm tra thêm trên Edge. Unique index SQL Server chưa được kiểm thử. |
| TC_39 | Sai email/SĐT | Đạt kiểm tra tài khoản/validation/controller; đăng nhập SĐT, header, phân quyền và đăng xuất được kiểm tra thêm trên Edge. Unique index SQL Server chưa được kiểm thử. |
| TC_40 | Tài khoản bị khóa | Đạt kiểm tra tài khoản/validation/controller; đăng nhập SĐT, header, phân quyền và đăng xuất được kiểm tra thêm trên Edge. Unique index SQL Server chưa được kiểm thử. |
| TC_41 | Hạn sử dụng <= ngày thu hoạch | Đạt kiểm tra quy tắc ngày lô/cận hạn; chưa thực hiện UI quản trị với SQL Server thật. |
| TC_44 | Lô còn 3 ngày | Đạt kiểm tra quy tắc ngày lô/cận hạn; chưa thực hiện UI quản trị với SQL Server thật. |
| TC_45 | Không đủ tồn kho | Đạt giỏ hàng, số lượng, tồn kho hoặc validation trên dữ liệu test; đồng bộ dùng InMemory. |
| TC_46 | Lọc khoảng giá | Đạt bộ lọc/tìm kiếm controller; AJAX và reset trên Edge; không sửa lại logic đang đạt. |
| TC_47 | Từ khóa có khoảng trắng | Đạt bộ lọc/tìm kiếm controller; AJAX và reset trên Edge; không sửa lại logic đang đạt. |
| TC_48 | Vượt tồn kho | Đạt giỏ hàng, số lượng, tồn kho hoặc validation trên dữ liệu test; đồng bộ dùng InMemory. |
| TC_49 | Số lượng = 0 | Đạt giỏ hàng, số lượng, tồn kho hoặc validation trên dữ liệu test; đồng bộ dùng InMemory. |
| TC_50 | Voucher không hợp lệ/hết lượt | Đạt từ chối voucher sai và công thức khuyến mãi; chưa chạy E2E voucher tài khoản thật. |
| TC_51 | Tổng tiền voucher | Đạt từ chối voucher sai và công thức khuyến mãi; chưa chạy E2E voucher tài khoản thật. |
| TC_52 | Hủy thanh toán trong 15 phút | Đạt callback/worker giả lập; chưa kiểm tra cổng thật và transaction SQL Server. |
| TC_53 | Auto cancel sau 15 phút | Đạt callback/worker giả lập; chưa kiểm tra cổng thật và transaction SQL Server. |
| TC_58 | 1 sao | Đạt controller đánh giá và giới hạn upload với fixture tổng hợp. |
| TC_59 | 5 sao | Đạt controller đánh giá và giới hạn upload với fixture tổng hợp. |
| TC_60 | 3 hình ảnh | Đạt controller đánh giá và giới hạn upload với fixture tổng hợp. |
| TC_61 | 4 hình ảnh | Đạt controller đánh giá và giới hạn upload với fixture tổng hợp. |
| TC_62 | 1 video | Đạt controller đánh giá và giới hạn upload với fixture tổng hợp. |
| TC_63 | 2 video | Đạt controller đánh giá và giới hạn upload với fixture tổng hợp. |
| TC_64 | Ảnh/video sai định dạng | Đạt controller đánh giá và giới hạn upload với fixture tổng hợp. |
| TC_65 | Lô đúng 3 ngày | Đạt kiểm tra quy tắc ngày lô/cận hạn; chưa thực hiện UI quản trị với SQL Server thật. |
| TC_78 | Hoàn tồn khi customer hủy | Đã tái hiện và sửa; hủy khi đóng gói, hoàn tồn, chặn dòng đã giao, đối soát khoản online. |
| TC_79 | Tìm kiếm sản phẩm theo tên | Đạt bộ lọc/tìm kiếm controller; AJAX và reset trên Edge; không sửa lại logic đang đạt. |
| TC_80 | Tìm kiếm với từ khóa không tồn tại | Đạt bộ lọc/tìm kiếm controller; AJAX và reset trên Edge; không sửa lại logic đang đạt. |
| TC_81 | Lọc sản phẩm theo Danh mục | Đạt bộ lọc/tìm kiếm controller; AJAX và reset trên Edge; không sửa lại logic đang đạt. |
| TC_82 | Lọc sản phẩm theo khoảng giá | Đạt bộ lọc/tìm kiếm controller; AJAX và reset trên Edge; không sửa lại logic đang đạt. |
| TC_83 | Kết hợp nhiều điều kiện lọc (Tên + Danh mục + Giá) | Đạt bộ lọc/tìm kiếm controller; AJAX và reset trên Edge; không sửa lại logic đang đạt. |
| TC_84 | Xóa bộ lọc / Reset về mặc định | Đạt bộ lọc/tìm kiếm controller; AJAX và reset trên Edge; không sửa lại logic đang đạt. |
| TC_85 | Nhập khoảng giá không hợp lệ (Từ > Đến) | Đã tái hiện và sửa; controller và ô nhập trên Edge đều chuẩn hóa khoảng giá. |
| TC_86 | Nhập giá trị âm hoặc chữ vào ô khoảng giá | Đạt bộ lọc/tìm kiếm controller; AJAX và reset trên Edge; không sửa lại logic đang đạt. |
| TC_87 | Thêm sản phẩm vào giỏ hàng thành công | Đạt giỏ hàng, số lượng, tồn kho hoặc validation trên dữ liệu test; đồng bộ dùng InMemory. |
| TC_88 | Xem giỏ hàng | Đạt luồng giỏ hàng/checkout/COD trong Edge với database test riêng. |
| TC_89 | Cập nhật số lượng sản phẩm trong giỏ | Đạt giỏ hàng, số lượng, tồn kho hoặc validation trên dữ liệu test; đồng bộ dùng InMemory. |
| TC_90 | Xóa sản phẩm khỏi giỏ hàng | Đạt giỏ hàng, số lượng, tồn kho hoặc validation trên dữ liệu test; đồng bộ dùng InMemory. |
| TC_91 | Chuyển sang trang thanh toán | Đạt luồng giỏ hàng/checkout/COD trong Edge với database test riêng. |
| TC_92 | Bỏ trống thông tin bắt buộc khi đặt hàng | Đạt giỏ hàng, số lượng, tồn kho hoặc validation trên dữ liệu test; đồng bộ dùng InMemory. |
| TC_93 | Đặt hàng thành công với thông tin hợp lệ | Đạt luồng giỏ hàng/checkout/COD trong Edge với database test riêng. |
| TC_94 | Kiểm tra khi sản phẩm hết hàng trong lúc thanh toán | Đạt từ chối reserve khi lô vừa hết hàng; chưa chứng minh tranh chấp đồng thời/rollback SQL Server. |
| TC_95 | Chọn quá số lượng sản phẩm trong giỏ | Đạt giỏ hàng, số lượng, tồn kho hoặc validation trên dữ liệu test; đồng bộ dùng InMemory. |
| TC_107 | Thông tin tài khoản | Đạt kiểm tra tài khoản/validation/controller; đăng nhập SĐT, header, phân quyền và đăng xuất được kiểm tra thêm trên Edge. Unique index SQL Server chưa được kiểm thử. |
| TC_109 | Đăng xuất | Đạt kiểm tra tài khoản/validation/controller; đăng nhập SĐT, header, phân quyền và đăng xuất được kiểm tra thêm trên Edge. Unique index SQL Server chưa được kiểm thử. |
| TC_116 | Vị trí Tư vấn AI | Đã tái hiện và sửa; Edge 390px/1280px, mở/đóng chat, ảnh kết quả. |

## Chạy lại

Cần .NET 10 SDK; test trình duyệt cần Microsoft Edge. Chạy từ thư mục gốc dự án:

```powershell
dotnet run --project tests/TestcaseEvals/TestcaseEvals.csproj
dotnet run --project tests/BrowserEvals/BrowserEvals.csproj
dotnet run --project tests/EmailEvals/EmailEvals.csproj
dotnet run --project tests/PromotionEvals/PromotionEvals.csproj
dotnet run --project tests/ChatbotEvals/ChatbotEvals.csproj
dotnet build THAN_NONG_SHOP.csproj
```
