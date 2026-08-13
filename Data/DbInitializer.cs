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
            var productImagesPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "products");
            var fallbackImages = Directory.Exists(productImagesPath)
                ? Directory.GetFiles(productImagesPath)
                    .Select(Path.GetFileName)
                    .Where(fileName => !string.IsNullOrEmpty(fileName))
                    .Cast<string>()
                    .ToArray()
                : Array.Empty<string>();
            bool pathFixed = false;
            foreach (var p in productsToFix)
            {
                string? correctImageUrl = p.Name switch
                {
                    var name when name.Contains("Cà chua", StringComparison.OrdinalIgnoreCase)
                        => "/images/products/e6b982f9-acd6-4a01-bc99-fa6edd4d1dad.jpg",
                    var name when name.Contains("Rau muống", StringComparison.OrdinalIgnoreCase)
                        => "/images/products/7291682d-725c-4a7a-af97-1e153c89e16c.webp",
                    var name when name.Contains("Dâu tây", StringComparison.OrdinalIgnoreCase)
                        => "/images/products/dau-tay-huu-co.png",
                    var name when name.Contains("Trứng gà", StringComparison.OrdinalIgnoreCase)
                        => "/images/products/trung-ga-thao-moc.png",
                    _ => null
                };

                if (correctImageUrl != null && p.ImageUrl != correctImageUrl)
                {
                    p.ImageUrl = correctImageUrl;
                    pathFixed = true;
                }

                if (string.IsNullOrEmpty(p.ImageUrl)) continue;

                // Nếu phát hiện đường dẫn chứa ký tự ổ đĩa vật lý hoặc gạch chéo ngược Windows
                if (p.ImageUrl.Contains(":\\") || p.ImageUrl.Contains(":/") || p.ImageUrl.Contains("\\") || p.ImageUrl.Contains("web1_do_an"))
                {
                    var fileName = Path.GetFileName(p.ImageUrl);
                    p.ImageUrl = "/images/products/" + fileName;
                    pathFixed = true;
                }

                var currentFileName = Path.GetFileName(p.ImageUrl);
                var currentImagePath = Path.Combine(productImagesPath, currentFileName);
                if (!File.Exists(currentImagePath) && fallbackImages.Length > 0)
                {
                    var fallbackImage = fallbackImages[Math.Abs(p.Id) % fallbackImages.Length];
                    p.ImageUrl = "/images/products/" + fallbackImage;
                    pathFixed = true;
                }
            }

            // 6. Bổ sung danh mục sản phẩm phong phú cho cửa hàng.
            // Chỉ thêm theo tên để không tạo trùng khi ứng dụng khởi động nhiều lần.
            var categoryIds = context.Categories.OrderBy(c => c.Id).Select(c => c.Id).Take(3).ToArray();
            if (categoryIds.Length >= 3)
            {
                var rauId = categoryIds[0];
                var traiCayId = categoryIds[1];
                var thucPhamId = categoryIds[2];
                var additionalProducts = new[]
                {
                    new Product { Name = "Cà rốt hữu cơ", Description = "Cà rốt giòn ngọt, giàu beta-carotene, phù hợp nấu canh, ép nước và làm salad.", price = 32000, ImageUrl = "/images/products/b060a3c2-aa9c-4198-b939-04c637ac141e.webp", farmerStory = "Thu hoạch trong ngày tại vùng rau Đà Lạt.", stockQuantity = 90, categoryId = rauId },
                    new Product { Name = "Khoai tây Đà Lạt", Description = "Khoai tây ruột vàng, bở thơm, không mọc mầm và được tuyển chọn kỹ.", price = 38000, ImageUrl = "/images/products/4d3706ad-5cfa-4ae5-b3e2-1f76f4c28aa6.jpg", farmerStory = "Canh tác luân canh giúp đất khỏe và củ phát triển tự nhiên.", stockQuantity = 75, categoryId = rauId },
                    new Product { Name = "Dưa leo baby", Description = "Dưa leo tươi giòn, ít hạt, vị thanh mát và thích hợp ăn sống.", price = 28000, ImageUrl = "/images/products/e0a3240f-74b3-4b19-a1f5-8bae2f08e844.jpg", farmerStory = "Trồng trong nhà màng và tưới nhỏ giọt tiết kiệm nước.", stockQuantity = 110, categoryId = rauId },
                    new Product { Name = "Bí xanh sạch", Description = "Bí xanh chắc quả, vị ngọt dịu, dùng nấu canh hoặc làm nước ép.", price = 26000, ImageUrl = "/images/products/2b9db25d-de2b-4a5a-b77d-a0c11599c04e.jpg", farmerStory = "Được chăm sóc bằng phân ủ hữu cơ tại Hòa Bình.", stockQuantity = 65, categoryId = rauId },
                    new Product { Name = "Xà lách xoăn", Description = "Xà lách lá xoăn xanh non, giòn và phù hợp cho các món salad.", price = 35000, ImageUrl = "/images/products/494dc6d2-8b47-4404-b3bb-ac7875b23c1c.jpg", farmerStory = "Thu hái sáng sớm để giữ độ tươi và dinh dưỡng.", stockQuantity = 80, categoryId = rauId },
                    new Product { Name = "Cải bó xôi", Description = "Cải bó xôi xanh đậm, giàu sắt, folate và vitamin cho bữa ăn gia đình.", price = 42000, ImageUrl = "/images/products/cb6434fc-3d8b-487c-80b5-90863a0fcc0f.jpg", farmerStory = "Canh tác theo quy trình hạn chế thuốc bảo vệ thực vật.", stockQuantity = 70, categoryId = rauId },
                    new Product { Name = "Cải xanh VietGAP", Description = "Cải xanh non, vị ngọt nhẹ, thích hợp nấu canh và xào.", price = 24000, ImageUrl = "/images/products/966141d4-fc21-4a2b-bbd1-f005682794a4.webp", farmerStory = "Sản xuất tại hợp tác xã rau an toàn đạt chuẩn VietGAP.", stockQuantity = 100, categoryId = rauId },
                    new Product { Name = "Thanh long ruột đỏ", Description = "Thanh long ruột đỏ mọng nước, ngọt thanh và giàu chất chống oxy hóa.", price = 55000, ImageUrl = "/images/products/abf9a6f0-8f6d-4373-b919-31dc78570ba6.webp", farmerStory = "Chong đèn tiết kiệm và chăm sóc tại vườn Bình Thuận.", stockQuantity = 60, categoryId = traiCayId },
                    new Product { Name = "Xoài cát chín cây", Description = "Xoài cát thịt vàng, ít xơ, thơm đậm và chín tự nhiên.", price = 68000, ImageUrl = "/images/products/ff5a2207-f4a7-4f19-9da5-addb7a866e74.webp", farmerStory = "Tuyển từ những vườn xoài lâu năm tại miền Tây.", stockQuantity = 85, categoryId = traiCayId },
                    new Product { Name = "Dưa hấu đỏ", Description = "Dưa hấu ruột đỏ, giòn ngọt, nhiều nước và giải nhiệt tốt.", price = 30000, ImageUrl = "/images/products/5b03d283-6111-4a00-949c-0836f47d1b3f.jpg", farmerStory = "Thu hoạch đúng độ chín tại vùng trồng Long An.", stockQuantity = 95, categoryId = traiCayId },
                    new Product { Name = "Đu đủ ruột vàng", Description = "Đu đủ chín mềm, ngọt dịu, giàu vitamin A và enzyme tiêu hóa.", price = 36000, ImageUrl = "/images/products/65b40d16-943a-4965-8c20-3c897fceecfe.jpg", farmerStory = "Chín tự nhiên trên cây trong vườn sinh thái.", stockQuantity = 55, categoryId = traiCayId },
                    new Product { Name = "Chuối già Nam Mỹ", Description = "Chuối chín vàng, dẻo ngọt, tiện dùng cho bữa sáng và sinh tố.", price = 34000, ImageUrl = "/images/products/d3a5836c-63ad-4f74-a22a-a8e33e9f042f.jpg", farmerStory = "Canh tác theo hướng sinh học tại Đồng Nai.", stockQuantity = 120, categoryId = traiCayId },
                    new Product { Name = "Bưởi da xanh", Description = "Bưởi tép hồng, mọng nước, vị ngọt thanh và ít hạt.", price = 85000, ImageUrl = "/images/products/b6ffc5ee-0da8-4bdd-9f1e-d6784613e38f.webp", farmerStory = "Chăm sóc tại vườn Bến Tre, bao trái hạn chế sâu bệnh.", stockQuantity = 48, categoryId = traiCayId },
                    new Product { Name = "Chanh dây tím", Description = "Chanh dây thơm, ruột vàng nhiều dịch, dùng pha nước và làm bánh.", price = 45000, ImageUrl = "/images/products/d6000c24-9eb7-40a5-b36e-62331b868e0b.webp", farmerStory = "Trồng trên giàn tại cao nguyên Lâm Đồng.", stockQuantity = 70, categoryId = traiCayId },
                    new Product { Name = "Ổi nữ hoàng", Description = "Ổi giòn, ít hạt, vị ngọt nhẹ và giàu vitamin C.", price = 40000, ImageUrl = "/images/products/ca19ddbe-2df8-4505-a3b0-62ef2ed5a7db.jpg", farmerStory = "Bao trái thủ công để hạn chế sâu bệnh và tồn dư thuốc.", stockQuantity = 78, categoryId = traiCayId },
                    new Product { Name = "Gạo tám thơm", Description = "Gạo hạt dài, cơm mềm dẻo và có hương thơm tự nhiên.", price = 125000, ImageUrl = "/images/products/70eebf90-7ca7-416f-8ae3-032fc4765747.jpg", farmerStory = "Canh tác trên cánh đồng phù sa và xay xát theo mẻ nhỏ.", stockQuantity = 150, categoryId = thucPhamId },
                    new Product { Name = "Đậu xanh nguyên hạt", Description = "Đậu xanh hạt đều, dùng nấu chè, làm bánh và chế biến sữa hạt.", price = 52000, ImageUrl = "/images/products/eabb50c6-774a-46a1-9396-bff83e96632b.webp", farmerStory = "Phơi nắng tự nhiên và không dùng chất bảo quản.", stockQuantity = 100, categoryId = thucPhamId },
                    new Product { Name = "Đậu đỏ dinh dưỡng", Description = "Đậu đỏ chắc hạt, bùi thơm và giàu protein thực vật.", price = 58000, ImageUrl = "/images/products/2d62203d-9cb1-45c7-9a7d-9f13bfe00d01.jpg", farmerStory = "Thu mua trực tiếp từ nông hộ vùng cao.", stockQuantity = 95, categoryId = thucPhamId },
                    new Product { Name = "Hạt mè đen", Description = "Mè đen sạch, thơm béo, phù hợp làm sữa hạt và chế biến món ăn.", price = 65000, ImageUrl = "/images/products/7626f159-33fd-4bd0-ac1d-10306d522822.jpg", farmerStory = "Sàng lọc kỹ và đóng gói ngay sau thu hoạch.", stockQuantity = 88, categoryId = thucPhamId },
                    new Product { Name = "Hạt điều rang muối", Description = "Hạt điều nguyên hạt, giòn béo, rang vừa vị và tiện dùng hàng ngày.", price = 145000, ImageUrl = "/images/products/88bba10c-c0b7-403f-9861-c055c5da00c3.jpg", farmerStory = "Chế biến từ hạt điều Bình Phước tuyển chọn.", stockQuantity = 60, categoryId = thucPhamId }
                };

                var existingProductNames = context.Products.Select(p => p.Name).ToHashSet();
                var productsToAdd = additionalProducts.Where(p => !existingProductNames.Contains(p.Name)).ToArray();
                if (productsToAdd.Length > 0)
                {
                    context.Products.AddRange(productsToAdd);
                    context.SaveChanges();
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
                            ImageUrl = "/images/products/e6b982f9-acd6-4a01-bc99-fa6edd4d1dad.jpg",
                            farmerStory = "Được trồng và chăm sóc tỉ mỉ bởi các bác nông dân tại HTX rau sạch Đà Lạt với phương pháp bón phân hữu cơ sinh học và tưới nước giếng sạch.",
                            stockQuantity = 88,
                            categoryId = catRau.Id
                        },
                        new Product
                        {
                            Name = "Rau muống hữu cơ sông Hồng",
                            Description = "Rau muống non mướt, giòn ngọt tự nhiên, không chứa tồn dư thuốc bảo vệ thực vật.",
                            price = 15000,
                            ImageUrl = "/images/products/7291682d-725c-4a7a-af97-1e153c89e16c.webp",
                            farmerStory = "Trồng trên bãi bồi phù sa sông Hồng màu mỡ, thu hoạch vào buổi sáng sớm để giữ nguyên độ tươi non.",
                            stockQuantity = 120,
                            categoryId = catRau.Id
                        },
                        new Product
                        {
                            Name = "Dâu tây hữu cơ chín mọng",
                            Description = "Dâu tây giống New Zealand quả to, thơm dịu, vị ngọt thanh mát xen lẫn chua nhẹ.",
                            price = 150000,
                            ImageUrl = "/images/products/dau-tay-huu-co.png",
                            farmerStory = "Được trồng trong nhà màng công nghệ cao tại Đà Lạt, thụ phấn bằng ong mật tự nhiên.",
                            stockQuantity = 45,
                            categoryId = catTraiCay.Id
                        },
                        new Product
                        {
                            Name = "Trứng gà thảo mộc an toàn",
                            Description = "Trứng gà ta vỏ dày, lòng đỏ to đậm đà béo ngậy, gà được cho ăn thức ăn trộn thảo mộc chống dịch bệnh.",
                            price = 48000,
                            ImageUrl = "/images/products/trung-ga-thao-moc.png",
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
