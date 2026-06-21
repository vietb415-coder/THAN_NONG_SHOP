using System;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using THAN_NONG_SHOP.Models;

namespace THAN_NONG_SHOP.Data
{
    public static class DbInitializer
    {
        public static void Seed(THAN_NONG_SHOP_DbContext context)
        {
            // Đảm bảo cơ sở dữ liệu đã được tạo
            context.Database.EnsureCreated();

            // 1. Tự động đồng bộ hóa đường dẫn ảnh sản phẩm nếu có lỗi đường dẫn vật lý ổ đĩa cục bộ C:\, D:\
            var productsToFix = context.Products.ToList();
            bool pathFixed = false;
            foreach (var p in productsToFix)
            {
                if (string.IsNullOrEmpty(p.ImageUrl)) continue;

                // Nếu phát hiện đường dẫn chứa ký tự ổ đĩa vật lý hoặc gạch chéo ngược Windows
                if (p.ImageUrl.Contains(":\\") || p.ImageUrl.Contains(":/") || p.ImageUrl.Contains("\\") || p.ImageUrl.Contains("web1_do_an"))
                {
                    var fileName = Path.GetFileName(p.ImageUrl);
                    p.ImageUrl = "/images/products/" + fileName;
                    pathFixed = true;
                }
            }
            if (pathFixed)
            {
                context.SaveChanges();
            }

            // 2. Gieo dữ liệu hạt giống Roles (Quyền)
            if (!context.Roles.Any())
            {
                using (var transaction = context.Database.BeginTransaction())
                {
                    try
                    {
                        context.Database.ExecuteSqlRaw("SET IDENTITY_INSERT Roles ON");
                        context.Roles.Add(new Role { Id = 1, roleName = "Admin" });
                        context.Roles.Add(new Role { Id = 2, roleName = "User" });
                        context.SaveChanges();
                        context.Database.ExecuteSqlRaw("SET IDENTITY_INSERT Roles OFF");
                        transaction.Commit();
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        // Fallback nếu không chạy được lệnh Raw SQL
                        context.Roles.Add(new Role { roleName = "Admin" });
                        context.Roles.Add(new Role { roleName = "User" });
                        context.SaveChanges();
                    }
                }
            }

            // Lấy Role ID chính xác sau khi thêm
            var adminRole = context.Roles.FirstOrDefault(r => r.roleName == "Admin");
            var userRole = context.Roles.FirstOrDefault(r => r.roleName == "User");
            int adminRoleId = adminRole != null ? adminRole.Id : 1;
            int userRoleId = userRole != null ? userRole.Id : 2;

            // 3. Gieo dữ liệu hạt giống Admin User (Tài khoản Quản trị)
            if (!context.Users.Any(u => u.UserName == "admin"))
            {
                context.Users.Add(new user
                {
                    UserName = "admin",
                    Fullname = "Quản Trị Viên",
                    Email = "admin@thannong.com",
                    Password = "admin123", // Lưu thô cho đồ án dễ kiểm tra và chấm điểm
                    Phone = "0123456789",
                    RoleId = adminRoleId
                });
                context.SaveChanges();
            }

            // 4. Gieo dữ liệu danh mục nông sản (Categories)
            if (!context.Categories.Any())
            {
                context.Categories.AddRange(
                    new Category { Name = "Rau củ quả sạch", Description = "Rau củ quả được trồng theo tiêu chuẩn hữu cơ, VietGAP" },
                    new Category { Name = "Trái cây hữu cơ", Description = "Trái cây tự nhiên, không sử dụng chất kích thích bảo quản" },
                    new Category { Name = "Thịt trứng sạch", Description = "Sản phẩm chăn nuôi chăn thả tự nhiên, an toàn sinh học" }
                );
                context.SaveChanges();
            }

            // 5. Gieo dữ liệu sản phẩm nông sản mẫu (Products)
            if (!context.Products.Any())
            {
                var catRau = context.Categories.FirstOrDefault(c => c.Name == "Rau củ quả sạch");
                var catTraiCay = context.Categories.FirstOrDefault(c => c.Name == "Trái cây hữu cơ");
                var catThitTrung = context.Categories.FirstOrDefault(c => c.Name == "Thịt trứng sạch");

                if (catRau != null && catTraiCay != null && catThitTrung != null)
                {
                    context.Products.AddRange(
                        new Product
                        {
                            Name = "Cà chua hữu cơ Đà Lạt",
                            Description = "Cà chua hữu cơ chín tự nhiên trên cây, ngọt mát căng mọng nước, giàu Vitamin A và C tốt cho sức khỏe.",
                            price = 35000,
                            ImageUrl = "/images/products/51af3559-0840-4fc2-b4d0-56d75cc35660.webp",
                            farmerStory = "Được trồng và chăm sóc tỉ mỉ bởi các bác nông dân tại HTX rau sạch Đà Lạt với phương pháp bón phân hữu cơ sinh học và tưới nước giếng sạch.",
                            stockQuantity = 88,
                            categoryId = catRau.Id
                        },
                        new Product
                        {
                            Name = "Rau muống hữu cơ sông Hồng",
                            Description = "Rau muống non mướt, giòn ngọt tự nhiên, không chứa tồn dư thuốc bảo vệ thực vật.",
                            price = 15000,
                            ImageUrl = "/images/products/fe975593-8363-41e1-9db0-fd31090ad65b.webp",
                            farmerStory = "Trồng trên bãi bồi phù sa sông Hồng màu mỡ, thu hoạch vào buổi sáng sớm để giữ nguyên độ tươi non.",
                            stockQuantity = 120,
                            categoryId = catRau.Id
                        },
                        new Product
                        {
                            Name = "Dâu tây hữu cơ chín mọng",
                            Description = "Dâu tây giống New Zealand quả to, thơm dịu, vị ngọt thanh mát xen lẫn chua nhẹ.",
                            price = 150000,
                            ImageUrl = "/images/products/51af3559-0840-4fc2-b4d0-56d75cc35660.webp",
                            farmerStory = "Được trồng trong nhà màng công nghệ cao tại Đà Lạt, thụ phấn bằng ong mật tự nhiên.",
                            stockQuantity = 45,
                            categoryId = catTraiCay.Id
                        },
                        new Product
                        {
                            Name = "Trứng gà thảo mộc an toàn",
                            Description = "Trứng gà ta vỏ dày, lòng đỏ to đậm đà béo ngậy, gà được cho ăn thức ăn trộn thảo mộc chống dịch bệnh.",
                            price = 48000,
                            ImageUrl = "/images/products/fe975593-8363-41e1-9db0-fd31090ad65b.webp",
                            farmerStory = "Đến từ trang trại sinh thái chăn thả tự nhiên của chú Tư tại Đồng Nai.",
                            stockQuantity = 200,
                            categoryId = catThitTrung.Id
                        }
                    );
                    context.SaveChanges();
                }
            }
        }
    }
}
