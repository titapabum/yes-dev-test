using CsvHelper;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using LumaSkinProject.Data;
using LumaSkinProject.Models;

public class ProductController : Controller
{
    private readonly ApplicationDbContext _context;

    public ProductController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public IActionResult ImportCsv(IFormFile csvFile)
    {
        if (csvFile == null || csvFile.Length == 0)
        {
            ModelState.AddModelError("", "กรุณาเลือกไฟล์ CSV");
            return View("Import");
        }

        var errorMessages = new List<string>();
        var productsToAdd = new List<Product>();
        
        // ดึง SKU ที่มีอยู่แล้วในระบบมาเช็คไม่ให้ซ้ำ
        var existingSkus = _context.Products.Select(p => p.Sku).ToHashSet();
        
        // หมวดหมู่ที่อนุญาต
        var allowedCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Cleanser", "Toner", "Serum", "Moisturizer", "Sunscreen", "Mask"
        };

        using (var stream = csvFile.OpenReadStream())
        using (var reader = new StreamReader(stream))
        using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
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
                // ตรวจสอบเงื่อนไขตามโจทย์ (Validation)
                // ==========================================

                // 1. ตรวจสอบ SKU (ห้ามว่าง, รูปแบบ LS-0000, และห้ามซ้ำ)
                if (string.IsNullOrEmpty(sku))
                {
                    errorMessages.Add($"แถวที่ {rowNumber}: รหัสสินค้า (sku) ห้ามว่าง");
                    continue;
                }
                
                // ตรวจสอบรูปแบบ LS-0000 (ตัวอักษร LS- ตามด้วยตัวเลข 4 หลัก)
                if (!Regex.IsMatch(sku, @"^LS-\d{4}$"))
                {
                    errorMessages.Add($"แถวที่ {rowNumber}: SKU '{sku}' ไม่ถูกต้องตามรูปแบบ (ต้องเป็นรูปแบบ LS-0000)");
                    continue;
                }

                if (existingSkus.Contains(sku) || productsToAdd.Any(p => p.Sku == sku))
                {
                    errorMessages.Add($"แถวที่ {rowNumber}: SKU '{sku}' มีซ้ำกันในระบบหรือในไฟล์ CSV");
                    continue;
                }

                // 2. ตรวจสอบชื่อสินค้า (name ห้ามว่าง)
                if (string.IsNullOrEmpty(name))
                {
                    errorMessages.Add($"แถวที่ {rowNumber}: ชื่อสินค้า (name) ห้ามว่าง");
                    continue;
                }

                // 3. ตรวจสอบ Category (ถ้าใส่มา ต้องตรงตามกำหนด)
                if (!string.IsNullOrEmpty(category) && !allowedCategories.Contains(category))
                {
                    errorMessages.Add($"แถวที่ {rowNumber}: ประเภทสินค้า '{category}' ไม่ถูกต้อง (ต้องเป็น Cleanser, Toner, Serum, Moisturizer, Sunscreen หรือ Mask)");
                    continue;
                }

                // 4. ตรวจสอบราคา (price ต้องเป็นตัวเลขและมากกว่า 0)
                if (!decimal.TryParse(priceStr, out decimal price) || price <= 0)
                {
                    errorMessages.Add($"แถวที่ {rowNumber}: ราคา '{priceStr}' ไม่ถูกต้อง (ต้องเป็นตัวเลขและมากกว่า 0)");
                    continue;
                }

                // 5. ตรวจสอบ Status (ถ้าว่างให้เป็น active, ถ้าใส่ต้องเป็น active หรือ inactive เท่านั้น)
                if (string.IsNullOrEmpty(status))
                {
                    status = "active"; // ค่าเริ่มต้นถ้าว่าง
                }
                else if (status != "active" && status != "inactive")
                {
                    errorMessages.Add($"แถวที่ {rowNumber}: สถานะ '{status}' ไม่ถูกต้อง (ต้องเป็น active หรือ inactive)");
                    continue;
                }

                // เพิ่มข้อมูลที่ผ่านการตรวจสอบลงใน List
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

        // หากพบข้อมูลไม่ถูกต้อง ให้ส่งรายการ Error กลับไปแสดงผลให้ Admin ทราบทันที
        if (errorMessages.Any())
        {
            ViewBag.Errors = errorMessages;
            return View("ImportResult");
        }

        // หากถูกต้องทั้งหมด บันทึกลงฐานข้อมูล
        _context.Products.AddRange(productsToAdd);
        _context.SaveChanges();

        TempData["SuccessMessage"] = $"นำเข้าข้อมูลสินค้าสำเร็จจำนวน {productsToAdd.Count} รายการ!";
        return RedirectToAction("Index");
    }
}