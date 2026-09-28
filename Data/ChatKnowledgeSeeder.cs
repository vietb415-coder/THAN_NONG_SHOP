using THAN_NONG_SHOP.Models;

namespace THAN_NONG_SHOP.Data;

public static class ChatKnowledgeSeeder
{
    public static void Seed(THAN_NONG_SHOP_DbContext db)
    {
        if (db.ChatKnowledge.Any()) return;
        db.ChatKnowledge.AddRange(
            new ChatKnowledge { Category = "Thanh toán", Title = "Các phương thức thanh toán", Content = "Shop hỗ trợ thanh toán khi nhận hàng (COD) và thanh toán trực tuyến qua PayOS tại bước đặt hàng." },
            new ChatKnowledge { Category = "Giao hàng", Title = "Phí và thời gian giao hàng", Content = "Phí và thời gian giao hàng phụ thuộc địa chỉ nhận hàng. Khách vui lòng cung cấp tỉnh/thành phố để nhân viên xác nhận chính xác." },
            new ChatKnowledge { Category = "Đổi trả", Title = "Yêu cầu đổi trả", Content = "Khách cần cung cấp mã đơn và tình trạng sản phẩm. Nhân viên sẽ xác minh trước khi chấp nhận đổi trả hoặc hoàn tiền." },
            new ChatKnowledge { Category = "Bảo mật", Title = "Thông tin không được yêu cầu", Content = "Shop không yêu cầu khách cung cấp mật khẩu, mã OTP hoặc toàn bộ thông tin thẻ trong cuộc trò chuyện." },
            new ChatKnowledge { Category = "Sản phẩm", Title = "Tồn kho và giá", Content = "Giá và tồn kho được cập nhật theo dữ liệu trên website. Khách nên kiểm tra lại tại trang sản phẩm trước khi thanh toán." }
        );
        db.SaveChanges();
    }
}
