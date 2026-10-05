# Đối chiếu 38 case Fail trong TEST_CASE_APP_THANNONG.xlsx

Excel đầu tiên cũng được đối chiếu các mục giao hàng, đánh giá, báo cáo và thông tin lô. Hai ZIP đính kèm có nội dung giống nhau. Bộ mới là bản chính; riêng TC_74 mới cho phép Guest checkout, khác yêu cầu đăng nhập ở bộ đầu.

| Case | Chức năng | Xử lý và phạm vi kiểm tra |
|---|---|---|
| TC_03 | Email xác nhận | Thử gửi xác nhận ngay sau đăng ký; SMTP lỗi thông báo thật và retry; cần kiểm tra Gmail trên hosting. |
| TC_05 | Đăng nhập thành công | Xác nhận token kích hoạt; không tắt điều kiện xác nhận email. Kiểm tra kích hoạt local và đăng nhập theo role. |
| TC_07 | Đăng nhập bằng SĐT | Đăng nhập bằng SĐT được kiểm thử cho User/Seller/Admin. |
| TC_08 | Truy cập chức năng theo Role | Role vẫn theo tài khoản đã duyệt; kiểm thử đăng nhập cả ba quyền. Không cấp Seller khi chưa duyệt. |
| TC_24 | Cập nhật không reload toàn trang | Thêm debounce input tìm kiếm, AJAX thay danh sách và xóa bộ lọc không reload. |
| TC_37 | Email phân biệt hoa thường | Giữ NormalizedEmail + unique index; kiểm thử email viết hoa không tạo bản ghi trùng. |
| TC_38 | Sai mật khẩu | Thông báo đúng Thông tin đăng nhập không chính xác; không cấp quyền. |
| TC_39 | Sai email/SĐT | Email/SĐT không tồn tại bị từ chối; kiểm thử. |
| TC_40 | Tài khoản bị khóa | Thông báo tài khoản tạm khóa khi mật khẩu đúng; không đăng nhập. |
| TC_41 | Hạn sử dụng <= ngày thu hoạch | Chặn hạn dùng <= thu hoạch; báo lỗi rõ; thêm cập nhật ngày lô cũ. |
| TC_44 | Lô còn 3 ngày | Ngưỡng đúng: 3 ngày không cận hạn, 0–2 ngày cảnh báo; có màn hình lô và báo cáo. |
| TC_45 | Không đủ tồn kho | Kiểm thử ReserveAsync từ chối số lượng vượt tồn, không thay đổi stock. |
| TC_46 | Lọc khoảng giá | Giữ bộ lọc giá kết hợp danh mục trong ProductsController; không bắt buộc đăng nhập để tìm. |
| TC_47 | Từ khóa có khoảng trắng | Trim từ khóa phía server; tìm kiếm AJAX theo input. |
| TC_48 | Vượt tồn kho | Giữ chặn số lượng vượt tồn phía server và giới hạn theo kho trên input. |
| TC_49 | Số lượng = 0 | Thêm giỏ không nhận <=0; cập nhật giỏ với 0 xóa mặt hàng theo quy tắc hiện có. |
| TC_50 | Voucher không hợp lệ/hết lượt | Ô voucher hiện ở giỏ và checkout; mã không hợp lệ/hết hạn không giảm tiền. |
| TC_51 | Tổng tiền voucher | Giữ tính tiền voucher phía server; PromotionEvals đã kiểm tra công thức. |
| TC_52 | Hủy thanh toán trong 15 phút | Giữ thanh toán hủy/thất bại ở Chờ thanh toán cho đến hạn 15 phút; không tin tham số return để ghi Paid. |
| TC_53 | Auto cancel sau 15 phút | Giữ worker hủy khi quá hạn và phục hồi tồn; sửa stock/marker hoàn tồn lưu cùng giao dịch. Cần kiểm thử SQL/worker trên hosting. |
| TC_58 | 1 sao | Có nút đánh giá từ lịch sử Đã giao; 1 sao được lưu, điểm tính từ các đánh giá. |
| TC_59 | 5 sao | 5 sao được lưu; không tự chọn 5 sao cho review mới. |
| TC_60 | 3 hình ảnh | Chuyển input media từ form giỏ sang form đánh giá; 3 ảnh được kiểm thử. |
| TC_61 | 4 hình ảnh | 4 ảnh bị từ chối, không thay thế dữ liệu review/media cũ. |
| TC_62 | 1 video | 1 MP4 được kiểm thử theo giới hạn định dạng/dung lượng. |
| TC_63 | 2 video | 2 video bị từ chối, giữ dữ liệu cũ. |
| TC_64 | Ảnh/video sai định dạng | Kiểm tra extension/MIME/signature/dung lượng; từ chối file sai/EXE. |
| TC_65 | Lô đúng 3 ngày | Mục Thống kê doanh thu hiển thị báo cáo/lô cận hạn; ngưỡng <3 ngày được kiểm thử. |
| TC_66 | Thanh toán | Có lựa chọn COD/PayOS/VNPay; QR online do cổng cung cấp sau khi cấu hình khóa thật. Chưa có khóa trong ZIP. |
| TC_67 | Cổng thanh toán đã gỡ | Không còn áp dụng; website hiện hỗ trợ COD, PayOS và VNPay. |
| TC_68 | VNPay thanh toán | VNPay có IPN kiểm tra chữ ký và số tiền; cần merchant keys để test tích hợp thật. |
| TC_69 | Đặt hàng COD | Kiểm thử Guest COD tạo đơn Chờ xác nhận; trừ kho đúng. |
| TC_70 | Giao nhanh | Giao nhanh 50.000đ, lưu ShippingMethod trên đơn; kiểm thử. |
| TC_71 | Giao Thường | Giao thường 30.000đ, lưu ShippingMethod trên đơn; kiểm thử. |
| TC_72 | Giao lạnh | Giao lạnh 70.000đ, lưu ShippingMethod trên đơn; kiểm thử. |
| TC_73 | giỏ hàng rỗng | Giỏ rỗng chặn checkout và có thông báo; kiểm thử. |
| TC_74 | GUEST đặt hàng | Cho Guest checkout theo bộ Excel mới; bảo vệ xem đơn bằng session, không mở quyền đánh giá/báo cáo. |
| TC_75 | Đặt hàng khi tồn thay đổi | Giữ transaction Serializable, reserve theo lô và cập nhật stock; kiểm thử logic tuần tự, chưa kiểm thử tải SQL đồng thời. |

Chi tiết chạy/cấu hình/giới hạn kiểm thử ở HUONG_DAN_BAN_SUA_TESTCASE.txt. Các case phụ thuộc dịch vụ thật chưa được đổi thành Pass.
