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
        public DbSet<Branch> Branches { get; set; }
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
        public DbSet<CashierShift> CashierShifts { get; set; }
        public DbSet<RestaurantSetting> RestaurantSettings { get; set; }

        // Enterprise ERP Tables
        public DbSet<Expense> Expenses { get; set; }
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<Ingredient> Ingredients { get; set; }
        public DbSet<Recipe> Recipes { get; set; }
        public DbSet<WasteLog> WasteLogs { get; set; }
        public DbSet<StockTransfer> StockTransfers { get; set; }
        public DbSet<StockTransferItem> StockTransferItems { get; set; }
        public DbSet<InventoryAdjustment> InventoryAdjustments { get; set; }

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

            modelBuilder.Entity<RestaurantTable>(entity =>
            {
                entity.HasOne(t => t.Waiter)
                    .WithMany()
                    .HasForeignKey(t => t.WaiterId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // Configure TableSession - Order relationship explicitly
            modelBuilder.Entity<Order>()
                .HasOne<TableSession>()
                .WithMany(s => s.Orders)
                .HasForeignKey(o => o.TableSessionId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Order>()
                .HasOne(o => o.Shift)
                .WithMany()
                .HasForeignKey(o => o.ShiftId)
                .OnDelete(DeleteBehavior.SetNull);

            // Configure multi-branch relationships
            modelBuilder.Entity<ApplicationUser>(entity =>
            {
                entity.HasOne(u => u.Branch)
                    .WithMany(b => b.Staff)
                    .HasForeignKey(u => u.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<RestaurantTable>(entity =>
            {
                entity.HasOne(t => t.Branch)
                    .WithMany(b => b.Tables)
                    .HasForeignKey(t => t.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Order>(entity =>
            {
                entity.HasOne(o => o.Branch)
                    .WithMany(b => b.Orders)
                    .HasForeignKey(o => o.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<TableSession>(entity =>
            {
                entity.HasOne(s => s.Branch)
                    .WithMany()
                    .HasForeignKey(s => s.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<MenuCategory>(entity =>
            {
                entity.HasOne(c => c.Branch)
                    .WithMany(b => b.MenuCategories)
                    .HasForeignKey(c => c.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<MenuItem>(entity =>
            {
                entity.HasOne(m => m.Branch)
                    .WithMany()
                    .HasForeignKey(m => m.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<CashierShift>(entity =>
            {
                entity.HasOne(s => s.Branch)
                    .WithMany()
                    .HasForeignKey(s => s.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<RestaurantSetting>(entity =>
            {
                entity.HasOne(s => s.Branch)
                    .WithMany()
                    .HasForeignKey(s => s.BranchId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ERP Configurations to prevent multiple cascade paths in SQL Server
            modelBuilder.Entity<Expense>(entity =>
            {
                entity.HasOne(e => e.Branch)
                    .WithMany()
                    .HasForeignKey(e => e.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Supplier>(entity =>
            {
                entity.HasOne(s => s.Branch)
                    .WithMany()
                    .HasForeignKey(s => s.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Ingredient>(entity =>
            {
                entity.HasOne(i => i.Branch)
                    .WithMany()
                    .HasForeignKey(i => i.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<WasteLog>(entity =>
            {
                entity.HasOne(w => w.Branch)
                    .WithMany()
                    .HasForeignKey(w => w.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<StockTransfer>(entity =>
            {
                entity.HasOne(s => s.SourceBranch)
                    .WithMany()
                    .HasForeignKey(s => s.SourceBranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(s => s.DestinationBranch)
                    .WithMany()
                    .HasForeignKey(s => s.DestinationBranchId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<StockTransferItem>(entity =>
            {
                entity.HasOne(i => i.StockTransfer)
                    .WithMany(t => t.Items)
                    .HasForeignKey(i => i.StockTransferId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<InventoryAdjustment>(entity =>
            {
                entity.HasOne(a => a.Branch)
                    .WithMany()
                    .HasForeignKey(a => a.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(a => a.Ingredient)
                    .WithMany()
                    .HasForeignKey(a => a.IngredientId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Seed Branches
            modelBuilder.Entity<Branch>().HasData(
                new Branch
                {
                    Id = 1,
                    Name = "Nasr City Branch",
                    Address = "Abbas El Akkad, Nasr City",
                    ContactPhone = "01000000001",
                    IsActive = true,
                    CreatedOn = new DateTime(2026, 1, 1)
                },
                new Branch
                {
                    Id = 2,
                    Name = "Maadi Branch",
                    Address = "Road 9, Maadi",
                    ContactPhone = "01000000002",
                    IsActive = true,
                    CreatedOn = new DateTime(2026, 1, 1)
                },
                new Branch
                {
                    Id = 3,
                    Name = "Central Warehouse",
                    Address = "Main Supply Depot, Cairo",
                    ContactPhone = "01000000003",
                    IsActive = true,
                    CreatedOn = new DateTime(2026, 1, 1)
                }
            );

            // Seed branch settings
            modelBuilder.Entity<RestaurantSetting>().HasData(
                new RestaurantSetting
                {
                    Id = 1,
                    BranchId = 1,
                    TaxPercentage = 14,
                    ServicePercentage = 12,
                    CreatedOn = new DateTime(2026, 1, 1)
                },
                new RestaurantSetting
                {
                    Id = 2,
                    BranchId = 2,
                    TaxPercentage = 14,
                    ServicePercentage = 12,
                    CreatedOn = new DateTime(2026, 1, 1)
                },
                new RestaurantSetting
                {
                    Id = 3,
                    BranchId = 3,
                    TaxPercentage = 14,
                    ServicePercentage = 12,
                    CreatedOn = new DateTime(2026, 1, 1)
                }
            );

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
