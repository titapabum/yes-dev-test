using System.ComponentModel.DataAnnotations;

namespace LumaSkinProject.Models
{
    public class Product
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "กรุณากรอกรหัสสินค้า (SKU)")]
        public string Sku { get; set; } = string.Empty; // รูปแบบ LS-0000

        [Required(ErrorMessage = "กรุณากรอกชื่อสินค้า")]
        public string Name { get; set; } = string.Empty;

        public string? Category { get; set; } // Cleanser, Toner, Serum, Moisturizer, Sunscreen, Mask

        [Required(ErrorMessage = "กรุณากรอกราคา")]
        public decimal Price { get; set; } // มากกว่า 0

        public string? Size { get; set; } // เช่น 30 ml, 50 g

        public string? Description { get; set; }

        public string? HowToUse { get; set; }

        public string Status { get; set; } = "active"; // active หรือ inactive (ค่าเริ่มต้นเป็น active)
    }
}