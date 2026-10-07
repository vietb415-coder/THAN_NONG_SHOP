# Sửa các test case Fail — 07/10/2026

Đọc toàn bộ workbook: 162 dòng Fail, 145 mã test case khác nhau. Test lần 1: 18 Fail, Test lần 2: 81 Fail, Test BC: 63 Fail.

## Các thay đổi trong lần này

- Email: tạo tài khoản chờ xác nhận và lưu thư vào outbox ngay cả khi SMTP chưa hoạt động; thử gửi ngay, báo đúng khi gửi thất bại, giữ retry và ghi trạng thái để Admin kiểm tra. Không cho tài khoản chưa xác nhận đăng nhập.
- Voucher: nếu mã đã áp dụng hết hiệu lực trước lúc checkout, chặn đặt hàng trước khi tạo đơn/trừ kho; yêu cầu khách xem lại tổng tiền.
- Checkout: request có token đã tạo đơn trả về đơn cũ sau khi token trong session bị xóa; kiểm tra đúng chủ sở hữu, không trừ kho/tạo đơn lần hai.
- Phân quyền: thêm trang AccessDenied trả HTTP 403; cookie chuyển đúng trang khi truy cập vượt quyền.
- MoMo: bổ sung lựa chọn checkout, tạo liên kết, kiểm tra phản hồi tạo link, IPN có chữ ký/số tiền/requestId, trang trở về và truy vấn đối soát trong worker. IPN lỗi/hủy không ghi nhận Paid; thanh toán đến sau khi hủy được đánh dấu đối soát.
- Chatbot: khách chưa đăng nhập không được dùng cookie visitor để truy cập hội thoại thuộc tài khoản đã đăng xuất.
- Các phần đã có trong ZIP được giữ: đăng nhập SĐT/email, giỏ DB/cookie/localStorage, tìm kiếm AJAX, ngày thu hoạch/hạn dùng, ngưỡng cận hạn dưới 3 ngày, voucher, đánh giá/media, phân bổ lô, hoàn tồn và xử lý đơn.

## Kết quả chạy mới

| Bộ kiểm tra | Kết quả | Log |
|---|---|---|
| logic | 119 PASS, 0 FAIL | [Log](test-results/20261007/logic.txt) |
| email | 26 PASS, 0 FAIL | [Log](test-results/20261007/email.txt) |
| promotions | 15 PASS, 0 FAIL | [Log](test-results/20261007/promotions.txt) |
| chatbot | 12 PASS, 0 FAIL | [Log](test-results/20261007/chatbot.txt) |
| Build toàn bộ C# và Razor | 0 lỗi, 12 cảnh báo nullable có sẵn | [Log](test-results/20261007/build.txt) |

Tổng 172 kiểm tra tự động đạt. Đây là số assertion kiểm tra, không phải khẳng định 145 case tích hợp đã Pass.

## Phần cần kiểm tra trên máy/hosting

- Trình duyệt mới chưa chạy được vì môi trường chặn Unix socket khi Chrome khởi động. Log browser-blocked.txt ghi rõ lỗi; không dùng ảnh/log ngày 05/10 làm bằng chứng chạy mới.
- Gmail: chưa gửi thư đến người dùng thật. SMTP loopback và pickup đã đạt; cần kiểm tra mật khẩu ứng dụng, kết nối SMTP của hosting và thư đến Inbox/Spam.
- MoMo/PayOS/VNPay: chưa thanh toán thật. Test dùng cấu hình/khóa giả và HTTP giả lập, không tạo giao dịch hoặc webhook ngoài Internet.
- SQL Server: InMemory không chứng minh unique index, transaction rollback hay tranh chấp hai người đặt món cuối cùng (WB_27, WB_60). Cần chạy hai request đồng thời với database test SQL Server.
- WB_47 mô tả chỉ Pending được khách tự hủy; TC_78 yêu cầu cả Pending và Packing. Bản này giữ TC_78: chỉ hủy Pending/Packing khi chưa có dòng Shipping/Completed, từ chối Paid. Không đổi quy tắc này chỉ để khớp hai yêu cầu khác nhau.
- Chiến dịch cũ kết thúc 30/09/2026; mã cũ phải hết hiệu lực theo WB_59. Muốn thử voucher còn hạn, dùng phần thưởng/voucher test được cấu hình trong DB thay vì kéo dài ngày mã cũ.

## Đối chiếu từng mã từng bị Fail

Bảng sau chỉ ra nơi xử lý để chạy lại từng case. Những case đã có logic trong source không bị coi là lỗi mới chỉ vì workbook ghi Fail ở bước đăng nhập. Các trạng thái Excel gốc được giữ nguyên.

| Mã | Nội dung | Sheet | Nơi xử lý/kiểm tra |
|---|---|---|---|
| TC_03 | Email xác nhận | Test lần 1, Test lần 2 | AccountController, EmailDelivery, Program; Gmail cần xác minh hosting |
| TC_07 | Đăng nhập bằng SĐT | Test lần 1, Test lần 2 | AccountController, EmailDelivery, Program; Gmail cần xác minh hosting |
| TC_24 | Cập nhật không reload toàn trang | Test lần 1, Test lần 2 | ProductsController, product-filters.js, Products/_Results |
| TC_37 | Email phân biệt hoa thường | Test lần 1, Test lần 2 | AccountController, EmailDelivery, Program; Gmail cần xác minh hosting |
| TC_41 | Hạn sử dụng <= ngày thu hoạch | Test lần 1, Test lần 2 | ShopRules, InventoryService, ProductController, báo cáo lô |
| TC_44 | Lô còn 3 ngày | Test lần 1 | ShopRules, InventoryService, ProductController, báo cáo lô |
| TC_50 | Voucher không hợp lệ/hết lượt | Test lần 1, Test lần 2 | CartController, PromotionCatalog, PromotionVoucher |
| TC_51 | Tổng tiền voucher | Test lần 1, Test lần 2 | CartController, PromotionCatalog, PromotionVoucher |
| TC_58 | 1 sao | Test lần 1, Test lần 2 | ProductReviewsController, ReviewUploads, Products/Details |
| TC_59 | 5 sao | Test lần 1, Test lần 2 | ProductReviewsController, ReviewUploads, Products/Details |
| TC_60 | 3 hình ảnh | Test lần 1, Test lần 2 | ProductReviewsController, ReviewUploads, Products/Details |
| TC_61 | 4 hình ảnh | Test lần 1, Test lần 2 | ProductReviewsController, ReviewUploads, Products/Details |
| TC_62 | 1 video | Test lần 1, Test lần 2 | ProductReviewsController, ReviewUploads, Products/Details |
| TC_63 | 2 video | Test lần 1, Test lần 2 | ProductReviewsController, ReviewUploads, Products/Details |
| TC_64 | Ảnh/video sai định dạng | Test lần 1, Test lần 2 | ProductReviewsController, ReviewUploads, Products/Details |
| TC_65 | Lô đúng 3 ngày | Test lần 1, Test lần 2 | ShopRules, InventoryService, ProductController, báo cáo lô |
| TC_78 | Hoàn tồn khi customer hủy | Test lần 1, Test lần 2 | OrderStatus, OrdersController, AdminOrdersController, InventoryService |
| TC_116 | Vị trí Tư vấn AI | Test lần 1, Test lần 2 | site.css, Shared/_Layout; cần retest trình duyệt |
| TC_01 | Đăng ký tài khoản | Test lần 2 | AccountController, EmailDelivery, Program; Gmail cần xác minh hosting |
| TC_04 | Đăng ký khi đã đăng nhập | Test lần 2 | AccountController, EmailDelivery, Program; Gmail cần xác minh hosting |
| TC_05 | Đăng nhập thành công | Test lần 2 | AccountController, EmailDelivery, Program; Gmail cần xác minh hosting |
| TC_08 | Truy cập chức năng theo Role | Test lần 2 | AccountController, EmailDelivery, Program; Gmail cần xác minh hosting |
| TC_17 | Đơn thanh toán thành công | Test lần 2 | CartController, CartState, PaymentGateways, OrderLifecycle, CommerceMaintenance |
| TC_18 | Đơn thanh toán thất bại | Test lần 2 | CartController, CartState, PaymentGateways, OrderLifecycle, CommerceMaintenance |
| TC_19 | Tìm kiếm theo tên | Test lần 2 | ProductsController, product-filters.js, Products/_Results |
| TC_20 | Không có kết quả | Test lần 2 | ProductsController, product-filters.js, Products/_Results |
| TC_21 | Tìm theo mô tả | Test lần 2 | ProductsController, product-filters.js, Products/_Results |
| TC_22 | Kết hợp nhiều bộ lọc | Test lần 2 | ProductsController, product-filters.js, Products/_Results |
| TC_23 | Xóa bộ lọc | Test lần 2 | ProductsController, product-filters.js, Products/_Results |
| TC_25 | Guest thêm giỏ | Test lần 2 | CartController, CartState, PaymentGateways, OrderLifecycle, CommerceMaintenance |
| TC_26 | Cập nhật số lượng | Test lần 2 | CartController, CartState, PaymentGateways, OrderLifecycle, CommerceMaintenance |
| TC_27 | Xóa sản phẩm | Test lần 2 | CartController, CartState, PaymentGateways, OrderLifecycle, CommerceMaintenance |
| TC_28 | Số lượng âm | Test lần 2 | CartController, CartState, PaymentGateways, OrderLifecycle, CommerceMaintenance |
| TC_29 | Guest refresh trình duyệt | Test lần 2 | CartController, CartState, PaymentGateways, OrderLifecycle, CommerceMaintenance |
| TC_30 | Customer đồng bộ DB | Test lần 2 | CartController, CartState, PaymentGateways, OrderLifecycle, CommerceMaintenance |
| TC_31 | Email/SĐT trùng | Test lần 2 | AccountController, EmailDelivery, Program; Gmail cần xác minh hosting |
| TC_32 | Mật khẩu yếu | Test lần 2 | AccountController, EmailDelivery, Program; Gmail cần xác minh hosting |
| TC_33 | Mật khẩu nhập lại sai | Test lần 2 | AccountController, EmailDelivery, Program; Gmail cần xác minh hosting |
| TC_34 | Email không hợp lệ | Test lần 2 | AccountController, EmailDelivery, Program; Gmail cần xác minh hosting |
| TC_35 | SĐT không hợp lệ | Test lần 2 | AccountController, EmailDelivery, Program; Gmail cần xác minh hosting |
| TC_36 | Khoảng trắng đầu/cuối | Test lần 2 | AccountController, EmailDelivery, Program; Gmail cần xác minh hosting |
| TC_38 | Sai mật khẩu | Test lần 2 | AccountController, EmailDelivery, Program; Gmail cần xác minh hosting |
| TC_39 | Sai email/SĐT | Test lần 2 | AccountController, EmailDelivery, Program; Gmail cần xác minh hosting |
| TC_40 | Tài khoản bị khóa | Test lần 2 | AccountController, EmailDelivery, Program; Gmail cần xác minh hosting |
| TC_45 | Không đủ tồn kho | Test lần 2 | CartController, CartState, PaymentGateways, OrderLifecycle, CommerceMaintenance |
| TC_46 | Lọc khoảng giá | Test lần 2 | ProductsController, product-filters.js, Products/_Results |
| TC_47 | Từ khóa có khoảng trắng | Test lần 2 | ProductsController, product-filters.js, Products/_Results |
| TC_48 | Vượt tồn kho | Test lần 2 | CartController, CartState, PaymentGateways, OrderLifecycle, CommerceMaintenance |
| TC_49 | Số lượng = 0 | Test lần 2 | CartController, CartState, PaymentGateways, OrderLifecycle, CommerceMaintenance |
| TC_52 | Hủy thanh toán trong 15 phút | Test lần 2 | CartController, CartState, PaymentGateways, OrderLifecycle, CommerceMaintenance |
| TC_53 | Auto cancel sau 15 phút | Test lần 2 | CartController, CartState, PaymentGateways, OrderLifecycle, CommerceMaintenance |
| TC_66 | Thanh toán | Test lần 2 | CartController, CartState, PaymentGateways, OrderLifecycle, CommerceMaintenance |
| TC_67 | MoMo thất bại/hủy | Test lần 2 | CartController, CartState, PaymentGateways, OrderLifecycle, CommerceMaintenance |
| TC_68 | VNPay thanh toán | Test lần 2 | CartController, CartState, PaymentGateways, OrderLifecycle, CommerceMaintenance |
| TC_69 | Đặt hàng COD | Test lần 2 | CartController, CartState, PaymentGateways, OrderLifecycle, CommerceMaintenance |
| TC_70 | Giao nhanh | Test lần 2 | CartController, CartState, PaymentGateways, OrderLifecycle, CommerceMaintenance |
| TC_71 | Giao Thường | Test lần 2 | CartController, CartState, PaymentGateways, OrderLifecycle, CommerceMaintenance |
| TC_72 | Giao lạnh | Test lần 2 | CartController, CartState, PaymentGateways, OrderLifecycle, CommerceMaintenance |
| TC_73 | giỏ hàng rỗng | Test lần 2 | CartController, CartState, PaymentGateways, OrderLifecycle, CommerceMaintenance |
| TC_74 | GUEST đặt hàng | Test lần 2 | CartController, CartState, PaymentGateways, OrderLifecycle, CommerceMaintenance |
| TC_75 | Đặt hàng khi tồn thay đổi | Test lần 2 | CartController, CartState, PaymentGateways, OrderLifecycle, CommerceMaintenance |
| TC_76 | Đang đóng gói → Đang giao | Test lần 2 | OrderStatus, OrdersController, AdminOrdersController, InventoryService |
| TC_77 | Đang giao → Đã giao | Test lần 2 | OrderStatus, OrdersController, AdminOrdersController, InventoryService |
| TC_79 | Tìm kiếm sản phẩm theo tên | Test lần 2 | ProductsController, product-filters.js, Products/_Results |
| TC_80 | Tìm kiếm với từ khóa không tồn tại | Test lần 2 | ProductsController, product-filters.js, Products/_Results |
| TC_81 | Lọc sản phẩm theo Danh mục | Test lần 2 | ProductsController, product-filters.js, Products/_Results |
| TC_82 | Lọc sản phẩm theo khoảng giá | Test lần 2 | ProductsController, product-filters.js, Products/_Results |
| TC_83 | Kết hợp nhiều điều kiện lọc (Tên + Danh mục + Giá) | Test lần 2 | ProductsController, product-filters.js, Products/_Results |
| TC_84 | Xóa bộ lọc / Reset về mặc định | Test lần 2 | ProductsController, product-filters.js, Products/_Results |
| TC_85 | Nhập khoảng giá không hợp lệ (Từ > Đến) | Test lần 2 | ProductsController, product-filters.js, Products/_Results |
| TC_86 | Nhập giá trị âm hoặc chữ vào ô khoảng giá | Test lần 2 | ProductsController, product-filters.js, Products/_Results |
| TC_87 | Thêm sản phẩm vào giỏ hàng thành công | Test lần 2 | CartController, CartState, PaymentGateways, OrderLifecycle, CommerceMaintenance |
| TC_88 | Xem giỏ hàng | Test lần 2 | CartController, CartState, PaymentGateways, OrderLifecycle, CommerceMaintenance |
| TC_89 | Cập nhật số lượng sản phẩm trong giỏ | Test lần 2 | CartController, CartState, PaymentGateways, OrderLifecycle, CommerceMaintenance |
| TC_90 | Xóa sản phẩm khỏi giỏ hàng | Test lần 2 | CartController, CartState, PaymentGateways, OrderLifecycle, CommerceMaintenance |
| TC_91 | Chuyển sang trang thanh toán | Test lần 2 | CartController, CartState, PaymentGateways, OrderLifecycle, CommerceMaintenance |
| TC_92 | Bỏ trống thông tin bắt buộc khi đặt hàng | Test lần 2 | CartController, CartState, PaymentGateways, OrderLifecycle, CommerceMaintenance |
| TC_93 | Đặt hàng thành công với thông tin hợp lệ | Test lần 2 | CartController, CartState, PaymentGateways, OrderLifecycle, CommerceMaintenance |
| TC_94 | Kiểm tra khi sản phẩm hết hàng trong lúc thanh toán | Test lần 2 | CartController, CartState, PaymentGateways, OrderLifecycle, CommerceMaintenance |
| TC_95 | Chọn quá số lượng sản phẩm trong giỏ | Test lần 2 | CartController, CartState, PaymentGateways, OrderLifecycle, CommerceMaintenance |
| TC_107 | Thông tin tài khoản | Test lần 2 | AccountController, EmailDelivery, Program; Gmail cần xác minh hosting |
| TC_109 | Đăng xuất | Test lần 2 | AccountController, EmailDelivery, Program; Gmail cần xác minh hosting |
| TC_WB_1 | Định dạng mật khẩu | Test BC | AccountController, Program, EmailDelivery; cần retest SMTP/SQL theo điều kiện case |
| TC_WB_2 | Plaintext, sai mk | Test BC | AccountController, Program, EmailDelivery; cần retest SMTP/SQL theo điều kiện case |
| TC_WB_3 | Rehash lại bản hash cũ | Test BC | AccountController, Program, EmailDelivery; cần retest SMTP/SQL theo điều kiện case |
| TC_WB_4 | Đăng nhập trùng | Test BC | AccountController, Program, EmailDelivery; cần retest SMTP/SQL theo điều kiện case |
| TC_WB_5 | Account bị khóa | Test BC | AccountController, Program, EmailDelivery; cần retest SMTP/SQL theo điều kiện case |
| TC_WB_6 | Account chưa xác nhận gmail | Test BC | AccountController, Program, EmailDelivery; cần retest SMTP/SQL theo điều kiện case |
| TC_WB_7 | Đăng nhập bằng sđt/email | Test BC | AccountController, Program, EmailDelivery; cần retest SMTP/SQL theo điều kiện case |
| TC_WB_8 | Mở Redirect thông qua Url trả về | Test BC | AccountController, Program, EmailDelivery; cần retest SMTP/SQL theo điều kiện case |
| TC_WB_9 | Tính role từ role id và selterapprove | Test BC | AccountController, Program, EmailDelivery; cần retest SMTP/SQL theo điều kiện case |
| TC_WB_10 | Thu hồi role  khi đổi hoặc khóa | Test BC | AccountController, Program, EmailDelivery; cần retest SMTP/SQL theo điều kiện case |
| TC_WB_11 | Truy cập chéo vùng theo role | Test BC | AccountController, Program, EmailDelivery; cần retest SMTP/SQL theo điều kiện case |
| TC_WB_12 | Thiếu antiforgery token | Test BC | CartController, CartState, InventoryService; WB_12 kiểm tra HTTP antiforgery |
| TC_WB_13 | Số lượng không hợp lệ | Test BC | CartController, CartState, InventoryService; WB_12 kiểm tra HTTP antiforgery |
| TC_WB_14 | Vượt tồn kho | Test BC | CartController, CartState, InventoryService; WB_12 kiểm tra HTTP antiforgery |
| TC_WB_15 | Giới hạn 30 mặt hàng | Test BC | CartController, CartState, InventoryService; WB_12 kiểm tra HTTP antiforgery |
| TC_WB_16 | Lô hết hạn không tính vào tồn | Test BC | CartController, CartState, InventoryService; WB_12 kiểm tra HTTP antiforgery |
| TC_WB_17 | Cookie giỏ hàng bị sửa | Test BC | CartController, CartState, InventoryService; WB_12 kiểm tra HTTP antiforgery |
| TC_WB_18 | Gộp giỏ guest khi đăng nhập | Test BC | CartController, CartState, InventoryService; WB_12 kiểm tra HTTP antiforgery |
| TC_WB_19 | Phân bổ lô theo hạn dùng gần nhất | Test BC | CartController, CartState, InventoryService; WB_12 kiểm tra HTTP antiforgery |
| TC_WB_20 | Token checkout sai | Test BC | CartController, PaymentGateways; WB_27 cần SQL Server đồng thời |
| TC_WB_21 | Bấm đặt hàng 2 lần | Test BC | CartController, PaymentGateways; WB_27 cần SQL Server đồng thời |
| TC_WB_22 | Validate số điện thoại | Test BC | CartController, PaymentGateways; WB_27 cần SQL Server đồng thời |
| TC_WB_23 | Độ dài tên và địa chỉ | Test BC | CartController, PaymentGateways; WB_27 cần SQL Server đồng thời |
| TC_WB_24 | Cổng thanh toán chưa cấu hình | Test BC | CartController, PaymentGateways; WB_27 cần SQL Server đồng thời |
| TC_WB_25 | Voucher làm tổng 0đ nhưng chọn online | Test BC | CartController, PaymentGateways; WB_27 cần SQL Server đồng thời |
| TC_WB_26 | Voucher hết hiệu lực giữa chừng | Test BC | CartController, PaymentGateways; WB_27 cần SQL Server đồng thời |
| TC_WB_27 | Hai người mua món cuối cùng | Test BC | CartController, PaymentGateways; WB_27 cần SQL Server đồng thời |
| TC_WB_28 | Trạng thái voucher theo phương thức | Test BC | CartController, PaymentGateways; WB_27 cần SQL Server đồng thời |
| TC_WB_29 | Tạo link thanh toán lỗi | Test BC | CartController, PaymentGateways; WB_27 cần SQL Server đồng thời |
| TC_WB_30 | Webhook sai chữ ký | Test BC | PaymentController, MoMoGateway, OrderLifecycle, CommerceMaintenance, InventoryService; cần cổng/SQL thật cho tích hợp |
| TC_WB_31 | Webhook sai số tiền | Test BC | PaymentController, MoMoGateway, OrderLifecycle, CommerceMaintenance, InventoryService; cần cổng/SQL thật cho tích hợp |
| TC_WB_32 | Thanh toán thành công | Test BC | PaymentController, MoMoGateway, OrderLifecycle, CommerceMaintenance, InventoryService; cần cổng/SQL thật cho tích hợp |
| TC_WB_33 | Webhook gọi lặp | Test BC | PaymentController, MoMoGateway, OrderLifecycle, CommerceMaintenance, InventoryService; cần cổng/SQL thật cho tích hợp |
| TC_WB_34 | Tiền về sau khi đơn đã hủy | Test BC | PaymentController, MoMoGateway, OrderLifecycle, CommerceMaintenance, InventoryService; cần cổng/SQL thật cho tích hợp |
| TC_WB_35 | VNPay IPN bất thường | Test BC | PaymentController, MoMoGateway, OrderLifecycle, CommerceMaintenance, InventoryService; cần cổng/SQL thật cho tích hợp |
| TC_WB_36 | MoMo IPN requestId khác orderId | Test BC | PaymentController, MoMoGateway, OrderLifecycle, CommerceMaintenance, InventoryService; cần cổng/SQL thật cho tích hợp |
| TC_WB_37 | Xem kết quả thanh toán của người khác | Test BC | PaymentController, MoMoGateway, OrderLifecycle, CommerceMaintenance, InventoryService; cần cổng/SQL thật cho tích hợp |
| TC_WB_38 | Tự hủy đơn sau 15 phút | Test BC | PaymentController, MoMoGateway, OrderLifecycle, CommerceMaintenance, InventoryService; cần cổng/SQL thật cho tích hợp |
| TC_WB_39 | Hủy đơn 2 lần | Test BC | PaymentController, MoMoGateway, OrderLifecycle, CommerceMaintenance, InventoryService; cần cổng/SQL thật cho tích hợp |
| TC_WB_40 | Cổng không phản hồi khi đối soát | Test BC | PaymentController, MoMoGateway, OrderLifecycle, CommerceMaintenance, InventoryService; cần cổng/SQL thật cho tích hợp |
| TC_WB_41 | Hoàn kho đơn không có phân bổ lô | Test BC | PaymentController, MoMoGateway, OrderLifecycle, CommerceMaintenance, InventoryService; cần cổng/SQL thật cho tích hợp |
| TC_WB_42 | Ma trận chuyển trạng thái | Test BC | OrderStatus, AdminOrdersController, OrdersController; WB_47 xem ghi chú xung đột |
| TC_WB_43 | Seller sửa dòng của seller khác | Test BC | OrderStatus, AdminOrdersController, OrdersController; WB_47 xem ghi chú xung đột |
| TC_WB_44 | Seller xử lý đơn chưa thanh toán | Test BC | OrderStatus, AdminOrdersController, OrdersController; WB_47 xem ghi chú xung đột |
| TC_WB_45 | Tổng hợp trạng thái đơn nhiều seller | Test BC | OrderStatus, AdminOrdersController, OrdersController; WB_47 xem ghi chú xung đột |
| TC_WB_46 | Admin hủy đơn COD và đơn online | Test BC | OrderStatus, AdminOrdersController, OrdersController; WB_47 xem ghi chú xung đột |
| TC_WB_47 | Khách tự hủy đơn | Test BC | OrderStatus, AdminOrdersController, OrdersController; WB_47 xem ghi chú xung đột |
| TC_WB_48 | Đánh giá khi chưa mua | Test BC | ProductReviewsController, ReviewUploads; logic và media boundary có test |
| TC_WB_49 | Biên rating và nội dung | Test BC | ProductReviewsController, ReviewUploads; logic và media boundary có test |
| TC_WB_50 | File upload giả mạo | Test BC | ProductReviewsController, ReviewUploads; logic và media boundary có test |
| TC_WB_51 | Giới hạn dung lượng và số lượng media | Test BC | ProductReviewsController, ReviewUploads; logic và media boundary có test |
| TC_WB_52 | Sửa review kèm media mới | Test BC | ProductReviewsController, ReviewUploads; logic và media boundary có test |
| TC_WB_54 | Truy cập hội thoại của người khác | Test BC | ChatController, ChatbotService; kiểm tra chủ sở hữu hội thoại |
| TC_WB_55 | Guest hỏi về đơn hàng | Test BC | ChatController, ChatbotService; kiểm tra chủ sở hữu hội thoại |
| TC_WB_56 | Biên độ dài tin nhắn | Test BC | ChatController, ChatbotService; kiểm tra chủ sở hữu hội thoại |
| TC_WB_57 | Feedback vào tin của hội thoại khác | Test BC | ChatController, ChatbotService; kiểm tra chủ sở hữu hội thoại |
| TC_WB_58 | Quay thưởng lần 2 trong ngày | Test BC | HomeController, PromotionCatalog; WB_60 cần SQL Server đồng thời |
| TC_WB_59 | Quay thưởng sau khi hết chiến dịch | Test BC | HomeController, PromotionCatalog; WB_60 cần SQL Server đồng thời |
| TC_WB_60 | Hết quà khi 2 người cùng quay | Test BC | HomeController, PromotionCatalog; WB_60 cần SQL Server đồng thời |
| TC_WB_61 | Cấp voucher cùng loại | Test BC | HomeController, PromotionCatalog; WB_60 cần SQL Server đồng thời |
| TC_WB_62 | Email xác nhận đi vào outbox | Test BC | AccountController, Program, EmailDelivery; cần retest SMTP/SQL theo điều kiện case |
| TC_WB_63 | Chưa kích hoạt thì không vào được | Test BC | AccountController, Program, EmailDelivery; cần retest SMTP/SQL theo điều kiện case |
| TC_WB_64 | Token xác nhận hết hạn hoặc dùng lại | Test BC | AccountController, Program, EmailDelivery; cần retest SMTP/SQL theo điều kiện case |

Tài liệu MoMo tham chiếu: https://developers.momo.vn/v3/docs/payment/api/wallet/onetime/ và https://developers.momo.vn/v3/docs/payment/api/payment-api/query/.
