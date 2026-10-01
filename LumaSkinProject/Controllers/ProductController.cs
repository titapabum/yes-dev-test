using CsvHelper;
using System.Globalization;
using System.Text.RegularExpressions;
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

        // 2. ฟังก์ชันประมวลผลไฟล์ CSV ที่อัปโหลดเข้ามา (POST)
        [HttpPost]
        public async Task<IActionResult> ImportCsv(IFormFile csvFile)
        {
            if (csvFile == null || csvFile.Length == 0)
            {
                ModelState.AddModelError("", "กรุณาเลือกไฟล์ CSV ที่ต้องการนำเข้า");
                return View("Import");
            }

            var errorMessages = new List<string>();
            var productsToAdd = new List<Product>();
            
            // ดึง SKU ที่มีอยู่แล้วในฐานข้อมูลมาเช็คไม่ให้ซ้ำ
            var existingSkus = await _context.Products.Select(p => p.Sku).ToHashSetAsync();
            
            // หมวดหมู่ที่อนุญาตตามโจทย์
            var allowedCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Cleanser", "Toner", "Serum", "Moisturizer", "Sunscreen", "Mask"
            };

            using (var stream = csvFile.OpenReadStream())
            using (var reader = new StreamReader(stream))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                try
                {
                    csv.Read();
                    csv.ReadHeader();
                    int rowNumber = 1; // เริ่มนับแถวข้อมูลจริง (ไม่รวม Header)

                    while (csv.Read())
                    {
                        rowNumber++;
                        string sku = csv.GetField("sku")?.Trim() ?? "";
                        string name = csv.GetField("name")?.Trim() ?? "";
                        string category = csv.GetField("category")?.Trim() ?? "";
                        string priceStr = csv.GetField("price")?.Trim() ?? "";
                        string size = csv.GetField("size")?.Trim() ?? "";
                        string description = csv.GetField("description")?.Trim() ?? "";
                        string howToUse = csv.GetField("how_to_use")?.Trim() ?? "";
                        string status = csv.GetField("status")?.Trim().ToLower() ?? "";

                        // ==========================================
                        // ตรวจสอบเงื่อนไขความถูกต้อง (Validation)
                        // ==========================================

                        // 1. SKU: ห้ามว่าง และต้องตรงกับรูปแบบ LS-0000
                        if (string.IsNullOrEmpty(sku))
                        {
                            errorMessages.Add($"แถวที่ {rowNumber}: รหัสสินค้า (sku) ห้ามว่าง");
                            continue;
                        }
                        
                        if (!Regex.IsMatch(sku, @"^LS-\d{4}$"))
                        {
                            errorMessages.Add($"แถวที่ {rowNumber}: SKU '{sku}' ไม่ถูกต้อง (ต้องเป็นรูปแบบ LS-0000 เช่น LS-0001)");
                            continue;
                        }

                        // ห้ามซ้ำกับใน DB หรือซ้ำกันเองในไฟล์
                        if (existingSkus.Contains(sku) || productsToAdd.Any(p => p.Sku == sku))
                        {
                            errorMessages.Add($"แถวที่ {rowNumber}: SKU '{sku}' มีซ้ำกันในระบบหรือภายในไฟล์ CSV");
                            continue;
                        }

                        // 2. Name: ห้ามว่าง
                        if (string.IsNullOrEmpty(name))
                        {
                            errorMessages.Add($"แถวที่ {rowNumber}: ชื่อสินค้า (name) ห้ามว่าง");
                            continue;
                        }

                        // 3. Category: ถ้าใส่มาต้องถูกต้องตามที่กำหนด
                        if (!string.IsNullOrEmpty(category) && !allowedCategories.Contains(category))
                        {
                            errorMessages.Add($"แถวที่ {rowNumber}: ประเภทสินค้า '{category}' ไม่ถูกต้อง (ต้องเป็น Cleanser, Toner, Serum, Moisturizer, Sunscreen หรือ Mask)");
                            continue;
                        }

                        // 4. Price: ต้องเป็นตัวเลขและมากกว่า 0
                        if (!decimal.TryParse(priceStr, out decimal price) || price <= 0)
                        {
                            errorMessages.Add($"แถวที่ {rowNumber}: ราคา '{priceStr}' ไม่ถูกต้อง (ต้องเป็นตัวเลขและมากกว่า 0)");
                            continue;
                        }

                        // 5. Status: ถ้าว่างให้เป็น active, ถ้าใส่ต้องเป็น active หรือ inactive เท่านั้น
                        if (string.IsNullOrEmpty(status))
                        {
                            status = "active";
                        }
                        else if (status != "active" && status != "inactive")
                        {
                            errorMessages.Add($"แถวที่ {rowNumber}: สถานะ '{status}' ไม่ถูกต้อง (ต้องเป็น active หรือ inactive)");
                            continue;
                        }

                        // ผ่านการตรวจสอบ เพิ่มลง List รอเซฟ
                        productsToAdd.Add(new Product
                        {
                            Sku = sku,
                            Name = name,
                            Category = string.IsNullOrEmpty(category) ? null : category,
                            Price = price,
                            Size = string.IsNullOrEmpty(size) ? null : size,
                            Description = string.IsNullOrEmpty(description) ? null : description,
                            HowToUse = string.IsNullOrEmpty(howToUse) ? null : howToUse,
                            Status = status
                        });
                    }
                }
                catch (Exception ex)
                {
                    errorMessages.Add($"รูปแบบไฟล์ CSV ไม่ถูกต้องหรือเกิดข้อผิดพลาดในการอ่าน: {ex.Message}");
                }
            }

            // หากพบข้อมูลไม่ถูกต้อง ส่งรายการ Error กลับมาแสดงที่หน้าจอ
            if (errorMessages.Any())
            {
                ViewBag.Errors = errorMessages;
                return View("Import");
            }

            // หากถูกต้องทั้งหมด บันทึกลงฐานข้อมูลทีเดียว
            _context.Products.AddRange(productsToAdd);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"นำเข้าข้อมูลสินค้าสำเร็จจำนวน {productsToAdd.Count} รายการ!";
            return RedirectToAction("Index", "Admin");
        }
    }
}