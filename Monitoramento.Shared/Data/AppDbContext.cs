using Microsoft.EntityFrameworkCore;
using Monitoramento.Shared.Models;

namespace Monitoramento.Shared.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<ApiMonitor> ApiMonitors { get; set; }
        public DbSet<Log> Logs { get; set; }
        public DbSet<Alert> Alerts { get; set; }
        public DbSet<PasswordResetToken> PasswordResetTokens { get; set; }
    }
}