using HandyGo.web.Models;
using Microsoft.EntityFrameworkCore;
using HandyGo.web.Models;

namespace HandyGo.web.Data
{

    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }

        public DbSet<Request> Requests { get; set; }

        public DbSet<Message> Messages { get; set; }


        public DbSet<Review> Reviews { get; set; }
        public DbSet<Complaint> Complaints { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<FinancialDispute> FinancialDisputes { get; set; }
        public DbSet<UserReport> UserReports { get; set; }
        public DbSet<Bid> Bids { get; set; }
        public DbSet<WithdrawalRequest> WithdrawalRequests { get; set; }
        public DbSet<PromoCode> PromoCodes { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Request>()
                .HasOne(r => r.Client)
                .WithMany(u => u.ClientRequests)
                .HasForeignKey(r => r.ClientId)
                .OnDelete(DeleteBehavior.Restrict); 

            modelBuilder.Entity<Request>()
                .HasOne(r => r.Technician)
                .WithMany(u => u.TechnicianRequests)
                .HasForeignKey(r => r.TechnicianId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}

