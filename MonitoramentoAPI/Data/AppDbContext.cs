using Microsoft.EntityFrameworkCore;
using MonitoramentoAPI.Models;

namespace MonitoramentoAPI.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Monitor> Monitors { get; set; }
    }

}