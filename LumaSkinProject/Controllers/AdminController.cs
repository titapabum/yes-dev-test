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
        public async Task<IActionResult> Index()
        {
            var products = await _context.Products
                .OrderByDescending(p => p.Id)
                .Take(10)
                .ToListAsync();

            ViewBag.TotalProducts = await _context.Products.CountAsync();
            ViewBag.TotalScans = 1428;

            // สั่งให้ชี้ไปที่โฟลเดอร์ Home/Index.cshtml ตรงๆ
            return View("~/Views/Home/Index.cshtml", products);
        }

        // ==========================================
        // 1. ระบบเข้าสู่ระบบ (Login) - ไม่มีสมัครสมาชิก
        // ==========================================
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Login(string email, string password)
        {
            // TODO: ตรวจสอบอีเมลและรหัสผ่านจากฐานข้อมูล (เช่น เช็ค PasswordHash)
            // ตัวอย่างจำลองเมื่อ Login สำเร็จ
            bool isValidUser = true;

            if (!isValidUser)
            {
                ModelState.AddModelError("", "อีเมลหรือรหัสผ่านไม่ถูกต้อง");
                return View();
            }

            // TODO: สร้าง Authentication Cookie หรือ Session ที่นี่
            return RedirectToAction("Index", "Product"); // ไปยังหน้าจัดการสินค้าหลังบ้าน
        }

        // ==========================================
        // 2. ระบบเชิญ Admin ใหม่ (เฉพาะ Super Admin)
        // ==========================================
        [Authorize(Roles = "SuperAdmin")] // จำกัดสิทธิ์ให้เฉพาะ Super Admin เท่านั้น
        [HttpGet]
        public IActionResult Invite()
        {
            return View();
        }

        [Authorize(Roles = "SuperAdmin")]
        [HttpPost]
        public IActionResult Invite(string email, string role)
        {
            if (string.IsNullOrEmpty(email))
            {
                ModelState.AddModelError("", "กรุณากรอกอีเมลผู้ถูกเชิญ");
                return View();
            }

            // สร้าง Token เฉพาะตัวสำหรับยืนยันตัวตนตั้งรหัสผ่าน
            var inviteToken = Guid.NewGuid().ToString();

            // TODO: บันทึกข้อมูล Admin ใหม่ลง DB ในสถานะยังไม่แอคทีฟ พร้อมเก็บ inviteToken

            // จำลองการส่งอีเมลโดยการบันทึกลงไฟล์ Log แทนการส่งจริงตามโจทย์
            string inviteLink = Url.Action("SetPassword", "Admin", new { token = inviteToken }, Request.Scheme);
            string logContent = $"[{DateTime.Now}] Send Invite to: {email} | Role: {role} | Link: {inviteLink}\n";

            // บันทึกลงไฟล์ log ภายในเครื่อง
            System.IO.File.AppendAllText("email_invite_log.txt", logContent);

            TempData["SuccessMessage"] = $"ส่งคำเชิญไปยัง {email} เรียบร้อยแล้ว (ระบบบันทึกลง log แทนการส่งอีเมลจริง)";
            return RedirectToAction("Index", "Product");
        }

        // ==========================================
        // 3. ระบบตั้งรหัสผ่านครั้งแรกจากลิงก์เชิญ
        // ==========================================
        [HttpGet]
        public IActionResult SetPassword(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return BadRequest("ลิงก์ไม่ถูกต้องหรือหมดอายุ");
            }

            ViewBag.Token = token;
            return View();
        }

        [HttpPost]
        public IActionResult SetPassword(string token, string password, string confirmPassword)
        {
            if (password != confirmPassword)
            {
                ModelState.AddModelError("", "รหัสผ่านไม่ตรงกัน");
                ViewBag.Token = token;
                return View();
            }

            // TODO: ค้นหาผู้ใช้จาก token, ทำการ Hash รหัสผ่านใหม่, และเปลี่ยนสถานะให้ใช้งานได้

            TempData["SuccessMessage"] = "ตั้งรหัสผ่านสำเร็จ! กรุณาเข้าสู่ระบบด้วยรหัสผ่านใหม่";
            return RedirectToAction("Login");
        }

        // ==========================================
        // 4. ออกจากระบบ (Logout)
        // ==========================================
        public IActionResult Logout()
        {
            // TODO: ลบ Authentication Cookie หรือ Session
            return RedirectToAction("Login");
        }
    }
}