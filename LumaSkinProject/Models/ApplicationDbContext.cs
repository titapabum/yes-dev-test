using Microsoft.EntityFrameworkCore;
using LumaSkinProject.Models;

namespace LumaSkinProject.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }
        public DbSet<Product> Products { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<ScanLog> ScanLogs { get; set; }
    }
}