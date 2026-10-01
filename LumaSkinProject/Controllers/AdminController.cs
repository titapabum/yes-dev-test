using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LumaSkinProject.Data;
using LumaSkinProject.Models;

namespace LumaSkinProject.Controllers
{
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
        }

        // หน้า Admin Dashboard
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var products = await _context.Products
                .OrderByDescending(p => p.Id)
                .Take(10)
                .ToListAsync();

            ViewBag.TotalProducts = await _context.Products.CountAsync();

            // 1. นับยอดสแกน QR ทั้งหมดในระบบ
            ViewBag.TotalScans = await _context.ScanLogs.CountAsync();

            // 2. คำนวณหาสินค้าที่ถูกสแกนมากที่สุด
            var topScanGroup = await _context.ScanLogs
                .GroupBy(s => s.ProductId)
                .Select(g => new { ProductId = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .FirstOrDefaultAsync();

            if (topScanGroup != null)
            {
                var topProduct = await _context.Products.FindAsync(topScanGroup.ProductId);
                ViewBag.TopProduct = topProduct != null ? $"{topProduct.Name}" : "ไม่พบข้อมูลสินค้า";
                ViewBag.TopProductCount = $"สแกน {topScanGroup.Count} ครั้ง";
                ViewBag.TopProductSku = topProduct != null ? $"SKU: {topProduct.Sku}" : "";
            }
            else
            {
                ViewBag.TopProduct = "ยังไม่มีการสแกน";
                ViewBag.TopProductCount = "0 ครั้ง";
                ViewBag.TopProductSku = "-";
            }

            return View("~/Views/Home/Index.cshtml", products);
        }

        // ==========================================
        // 1. ระบบเข้าสู่ระบบ (Login) - ไม่มีสมัครสมาชิก
        // ==========================================
        // [HttpGet]
        // public IActionResult Login()
        // {
        //     return View();
        // }

        // [HttpPost]
        // public IActionResult Login(string email, string password)
        // {
        //     // TODO: ตรวจสอบอีเมลและรหัสผ่านจากฐานข้อมูล (เช่น เช็ค PasswordHash)
        //     // ตัวอย่างจำลองเมื่อ Login สำเร็จ
        //     bool isValidUser = true;

        //     if (!isValidUser)
        //     {
        //         ModelState.AddModelError("", "อีเมลหรือรหัสผ่านไม่ถูกต้อง");
        //         return View();
        //     }

        //     // TODO: สร้าง Authentication Cookie หรือ Session ที่นี่
        //     return RedirectToAction("Index", "Product"); // ไปยังหน้าจัดการสินค้าหลังบ้าน
        // }

        // // ==========================================
        // // 2. ระบบเชิญ Admin ใหม่ (เฉพาะ Super Admin)
        // // ==========================================
        // [Authorize(Roles = "SuperAdmin")] // จำกัดสิทธิ์ให้เฉพาะ Super Admin เท่านั้น
        // [HttpGet]
        // public IActionResult Invite()
        // {
        //     return View();
        // }

        // [Authorize(Roles = "SuperAdmin")]
        // [HttpPost]
        // public IActionResult Invite(string email, string role)
        // {
        //     if (string.IsNullOrEmpty(email))
        //     {
        //         ModelState.AddModelError("", "กรุณากรอกอีเมลผู้ถูกเชิญ");
        //         return View();
        //     }

        //     // สร้าง Token เฉพาะตัวสำหรับยืนยันตัวตนตั้งรหัสผ่าน
        //     var inviteToken = Guid.NewGuid().ToString();

        //     // TODO: บันทึกข้อมูล Admin ใหม่ลง DB ในสถานะยังไม่แอคทีฟ พร้อมเก็บ inviteToken

        //     // จำลองการส่งอีเมลโดยการบันทึกลงไฟล์ Log แทนการส่งจริงตามโจทย์
        //     string inviteLink = Url.Action("SetPassword", "Admin", new { token = inviteToken }, Request.Scheme);
        //     string logContent = $"[{DateTime.Now}] Send Invite to: {email} | Role: {role} | Link: {inviteLink}\n";

        //     // บันทึกลงไฟล์ log ภายในเครื่อง
        //     System.IO.File.AppendAllText("email_invite_log.txt", logContent);

        //     TempData["SuccessMessage"] = $"ส่งคำเชิญไปยัง {email} เรียบร้อยแล้ว (ระบบบันทึกลง log แทนการส่งอีเมลจริง)";
        //     return RedirectToAction("Index", "Product");
        // }

        // // ==========================================
        // // 3. ระบบตั้งรหัสผ่านครั้งแรกจากลิงก์เชิญ
        // // ==========================================
        // [HttpGet]
        // public IActionResult SetPassword(string token)
        // {
        //     if (string.IsNullOrEmpty(token))
        //     {
        //         return BadRequest("ลิงก์ไม่ถูกต้องหรือหมดอายุ");
        //     }

        //     ViewBag.Token = token;
        //     return View();
        // }

        [HttpPost]
        public IActionResult Login(string username, string password)
        {
            // ตรวจสอบค่าที่กำหนดตามโจทย์
            if (username == "admin" && password == "12345")
            {
                // หากถูกต้อง ส่งไปยังหน้า Dashboard (Index)
                return RedirectToAction("Index", "Admin");
            }

            // หากไม่ถูกต้อง แสดงข้อความแจ้งเตือน
            ViewBag.Error = "ชื่อผู้ใช้งานหรือรหัสผ่านไม่ถูกต้อง";
            return View();
        }

        // ==========================================
        // 4. ออกจากระบบ (Logout)
        // ==========================================
        public IActionResult Logout()
        {
            return RedirectToAction("Login", "Home");
        }
    }
}
