using CsvHelper;
using QRCoder;
using System.Drawing;
using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LumaSkinProject.Data;
using LumaSkinProject.Models;

namespace LumaSkinProject.Controllers
{
    public class ProductController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProductController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 1. หน้าจอสำหรับอัปโหลดไฟล์ CSV (GET)
        [HttpGet]
        public IActionResult Import()
        {
            return View();
        }

        // 2. ฟังก์ชันประมวลผลไฟล์ CSV (POST) - รองรับทั้ง Insert ครั้งแรกและ Update ซ้ำด้วย SKU
        [HttpPost]
        public async Task<IActionResult> ImportCsv(IFormFile csvFile)
        {
            if (csvFile == null || csvFile.Length == 0)
            {
                TempData["Error"] = "กรุณาเลือกไฟล์ CSV ที่ต้องการนำเข้า";
                return View("Import");
            }

            int successCount = 0;
            int updateCount = 0;

            using (var stream = csvFile.OpenReadStream())
            using (var reader = new StreamReader(stream))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                try
                {
                    csv.Read();
                    csv.ReadHeader();

                    while (csv.Read())
                    {
                        // ดึงข้อมูลแต่ละคอลัมน์จาก CSV
                        string sku = csv.GetField("sku")?.Trim() ?? "";
                        string name = csv.GetField("name")?.Trim() ?? "";
                        string category = csv.GetField("category")?.Trim() ?? "";
                        string priceStr = csv.GetField("price")?.Trim() ?? "";
                        string size = csv.GetField("size")?.Trim() ?? "";
                        string description = csv.GetField("description")?.Trim() ?? "";
                        string howToUse = csv.GetField("how_to_use")?.Trim() ?? "";
                        string status = csv.GetField("status")?.Trim().ToLower() ?? "";

                        // เงื่อนไขยืดหยุ่น: หาก SKU หรือ ชื่อสินค้าว่าง ให้ข้ามแถวนี้ไปเลยเพื่อความปลอดภัย
                        if (string.IsNullOrEmpty(sku) || string.IsNullOrEmpty(name))
                            continue;

                        // แปลงราคา (ถ้าช่องราคาว่างหรือผิดพลาด ให้เป็น 0.0)
                        decimal.TryParse(priceStr, out decimal price);

                        // ตรวจสอบสถานะ (ถ้าเว้นว่าง หรือระบุไม่ถูกต้อง ให้กำหนดค่าเริ่มต้นเป็น "active")
                        if (string.IsNullOrEmpty(status) || (status != "active" && status != "inactive"))
                        {
                            status = "active";
                        }

                        // ==========================================
                        // ระบบ UPSERT (เช็คว่ามี SKU นี้ในฐานข้อมูลหรือยัง)
                        // ==========================================
                        var existingProduct = await _context.Products.FirstOrDefaultAsync(p => p.Sku == sku);

                        if (existingProduct != null)
                        {
                            // CASE 2: มี SKU นี้อยู่แล้ว -> ทำการอัปเดตข้อมูลใหม่ทับ (ใช้สำหรับไฟล์อัปเดตครั้งที่ 2)
                            existingProduct.Name = name;
                            existingProduct.Category = string.IsNullOrEmpty(category) ? existingProduct.Category : category;
                            existingProduct.Price = price > 0 ? price : existingProduct.Price;
                            existingProduct.Size = string.IsNullOrEmpty(size) ? existingProduct.Size : size;
                            existingProduct.Description = string.IsNullOrEmpty(description) ? existingProduct.Description : description;
                            existingProduct.HowToUse = string.IsNullOrEmpty(howToUse) ? existingProduct.HowToUse : howToUse;
                            existingProduct.Status = status;

                            updateCount++;
                        }
                        else
                        {
                            // CASE 1: ยังไม่มี SKU นี้ -> เพิ่มข้อมูลใหม่ (ใช้สำหรับไฟล์นำเข้าครั้งแรก)
                            var newProduct = new Product
                            {
                                Sku = sku,
                                Name = name,
                                Category = string.IsNullOrEmpty(category) ? null : category,
                                Price = price,
                                Size = string.IsNullOrEmpty(size) ? null : size,
                                Description = string.IsNullOrEmpty(description) ? null : description,
                                HowToUse = string.IsNullOrEmpty(howToUse) ? null : howToUse,
                                Status = status
                            };

                            _context.Products.Add(newProduct);
                            successCount++;
                        }
                    }

                    // บันทึกการเปลี่ยนแปลงทั้งหมดลงฐานข้อมูล SQLite ทีเดียว
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    TempData["Error"] = $"เกิดข้อผิดพลาดในการอ่านไฟล์ CSV: {ex.Message}";
                    return View("Import");
                }
            }

            TempData["SuccessMessage"] = $"นำเข้าไฟล์ CSV สำเร็จ: เพิ่มสินค้าใหม่ {successCount} รายการ, อัปเดตข้อมูลเดิม {updateCount} รายการ";
            return RedirectToAction("Index", "Admin");
        }
    }

    namespace LumaSkinProject.Controllers
    {
        public class ProductController : Controller
        {
            private readonly ApplicationDbContext _context;

            public ProductController(ApplicationDbContext context)
            {
                _context = context;
            }

            // 1. ฟังก์ชันสร้างและแสดงรูปภาพ QR Code สำหรับสินค้าแต่ละชิ้น (เรียกใช้ผ่าน ID หรือ SKU)
            [HttpGet]
            public IActionResult GenerateQrCode(int id)
            {
                var product = _context.Products.Find(id);
                if (product == null)
                {
                    return NotFound();
                }

                // สร้าง URL ปลายทางเมื่อสแกน (เช่น https://localhost:xxxx/Product/PublicDetail/5)
                string publicUrl = Url.Action("PublicDetail", "Product", new { id = product.Id }, Request.Scheme);

                // ใช้ QRCoder สร้าง QR Code
                using (QRCodeGenerator qrGenerator = new QRCodeGenerator())
                {
                    using (QRCodeData qrCodeData = qrGenerator.CreateQrCode(publicUrl, QRCodeGenerator.ECCLevel.Q))
                    {
                        using (PngByteQRCode qrCode = new PngByteQRCode(qrCodeData))
                        {
                            byte[] qrCodeBytes = qrCode.GetGraphic(20);
                            return File(qrCodeBytes, "image/png"); // ส่งออกเป็นไฟล์รูปภาพ PNG ทันที
                        }
                    }
                }
            }

            // 2. หน้าเว็บแสดงรายละเอียดสินค้าฝั่งลูกค้า (เมื่อสแกน QR Code เข้ามา)
            [HttpGet]
            public async Task<IActionResult> PublicDetail(int id)
            {
                var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == id);
                if (product == null)
                {
                    return NotFound();
                }

                // ==========================================
                // บันทึก Log ทุกครั้งที่มีคนเปิดหน้าสินค้าจาก QR Code
                // ==========================================
                var scanLog = new ScanLog
                {
                    ProductId = product.Id,
                    ScannedAt = DateTime.Now
                };

                _context.ScanLogs.Add(scanLog);
                await _context.SaveChangesAsync();

                return View(product); // ส่งไปแสดงผลที่หน้าจอ Public Mobile-friendly
            }
        }
    }
}