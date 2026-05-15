using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Resturant.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Resturant.Infrastructure.Data
{
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        public DbSet<RestaurantTable> RestaurantTables { get; set; }
        public DbSet<QrCode> QrCodes { get; set; }
        public DbSet<MenuCategory> MenuCategories { get; set; }
        public DbSet<MenuItem> MenuItems { get; set; }
        public DbSet<MenuItemAddOn> MenuItemAddOns { get; set; }
        public DbSet<MenuItemRecommendation> MenuItemRecommendations { get; set; }
        public DbSet<TableSession> TableSessions { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<OrderItemAddOn> OrderItemAddOns { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<MenuItemRecommendation>(entity =>
            {
                entity.HasOne(r => r.PrimaryMenuItem)
                    .WithMany(m => m.Recommendations)
                    .HasForeignKey(r => r.PrimaryMenuItemId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(r => r.RecommendedMenuItem)
                    .WithMany()
                    .HasForeignKey(r => r.RecommendedMenuItemId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<TableSession>(entity =>
            {
                entity.HasOne(s => s.Table)
                    .WithMany(t => t.Sessions)
                    .HasForeignKey(s => s.TableId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure TableSession - Order relationship explicitly
            modelBuilder.Entity<Order>()
                .HasOne<TableSession>()
                .WithMany(s => s.Orders)
                .HasForeignKey(o => o.TableSessionId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<OrderItemAddOn>(entity =>
            {
                entity.HasOne(o => o.OrderItem)
                    .WithMany(i => i.AddOns)
                    .HasForeignKey(o => o.OrderItemId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(o => o.AddOn)
                    .WithMany()
                    .HasForeignKey(o => o.MenuItemAddOnId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<OrderItem>(entity =>
            {
                entity.HasOne(i => i.Order)
                    .WithMany(o => o.OrderItems)
                    .HasForeignKey(i => i.OrderId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(i => i.MenuItem)
                    .WithMany()
                    .HasForeignKey(i => i.MenuItemId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Set global precision for decimal properties to avoid warnings and truncation
            foreach (var property in modelBuilder.Model.GetEntityTypes()
                .SelectMany(t => t.GetProperties())
                .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
            {
                property.SetColumnType("decimal(18,2)");
            }
        }
    }
}
