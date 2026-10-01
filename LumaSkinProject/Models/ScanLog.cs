using System;

namespace LumaSkinProject.Models
{
    public class ScanLog
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public DateTime ScannedAt { get; set; } = DateTime.Now;

        // เชื่อมความสัมพันธ์กับตาราง Product
        public Product? Product { get; set; }
    }
}