using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Resturant.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiBranchSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Create Branches table first
            migrationBuilder.CreateTable(
                name: "Branches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ContactPhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdatedById = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastUpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Branches", x => x.Id);
                });

            // 2. Insert default branches before columns are added
            migrationBuilder.InsertData(
                table: "Branches",
                columns: new[] { "Id", "Address", "ContactPhone", "CreatedById", "CreatedOn", "IsActive", "IsDeleted", "LastUpdatedById", "LastUpdatedOn", "Name" },
                values: new object[,]
                {
                    { 1, "Abbas El Akkad, Nasr City", "01000000001", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, false, null, null, "Nasr City Branch" },
                    { 2, "Road 9, Maadi", "01000000002", null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), true, false, null, null, "Maadi Branch" }
                });

            // 3. Add BranchId columns to all other tables with defaultValue: 1
            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "WasteLogs",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "TableSessions",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "Suppliers",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "RestaurantTables",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "RestaurantSettings",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "Orders",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "MenuItems",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "MenuCategories",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "Ingredients",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "Expenses",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "CashierShifts",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "AspNetUsers",
                type: "int",
                nullable: true);

            // 4. Seeding updates and configurations
            migrationBuilder.UpdateData(
                table: "RestaurantSettings",
                keyColumn: "Id",
                keyValue: 1,
                column: "BranchId",
                value: 1);

            migrationBuilder.InsertData(
                table: "RestaurantSettings",
                columns: new[] { "Id", "BranchId", "CreatedById", "CreatedOn", "IsDeleted", "LastUpdatedById", "LastUpdatedOn", "ServicePercentage", "TaxPercentage" },
                values: new object[] { 2, 2, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), false, null, null, 12m, 14m });

            migrationBuilder.CreateIndex(
                name: "IX_WasteLogs_BranchId",
                table: "WasteLogs",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_TableSessions_BranchId",
                table: "TableSessions",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_BranchId",
                table: "Suppliers",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantTables_BranchId",
                table: "RestaurantTables",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantSettings_BranchId",
                table: "RestaurantSettings",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_BranchId",
                table: "Orders",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_BranchId",
                table: "MenuItems",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuCategories_BranchId",
                table: "MenuCategories",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Ingredients_BranchId",
                table: "Ingredients",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_BranchId",
                table: "Expenses",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_CashierShifts_BranchId",
                table: "CashierShifts",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_BranchId",
                table: "AspNetUsers",
                column: "BranchId");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Branches_BranchId",
                table: "AspNetUsers",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CashierShifts_Branches_BranchId",
                table: "CashierShifts",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_Branches_BranchId",
                table: "Expenses",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Ingredients_Branches_BranchId",
                table: "Ingredients",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MenuCategories_Branches_BranchId",
                table: "MenuCategories",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MenuItems_Branches_BranchId",
                table: "MenuItems",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Branches_BranchId",
                table: "Orders",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RestaurantSettings_Branches_BranchId",
                table: "RestaurantSettings",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RestaurantTables_Branches_BranchId",
                table: "RestaurantTables",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Suppliers_Branches_BranchId",
                table: "Suppliers",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TableSessions_Branches_BranchId",
                table: "TableSessions",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WasteLogs_Branches_BranchId",
                table: "WasteLogs",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_Branches_BranchId",
                table: "AspNetUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_CashierShifts_Branches_BranchId",
                table: "CashierShifts");

            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_Branches_BranchId",
                table: "Expenses");

            migrationBuilder.DropForeignKey(
                name: "FK_Ingredients_Branches_BranchId",
                table: "Ingredients");

            migrationBuilder.DropForeignKey(
                name: "FK_MenuCategories_Branches_BranchId",
                table: "MenuCategories");

            migrationBuilder.DropForeignKey(
                name: "FK_MenuItems_Branches_BranchId",
                table: "MenuItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Branches_BranchId",
                table: "Orders");

            migrationBuilder.DropForeignKey(
                name: "FK_RestaurantSettings_Branches_BranchId",
                table: "RestaurantSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_RestaurantTables_Branches_BranchId",
                table: "RestaurantTables");

            migrationBuilder.DropForeignKey(
                name: "FK_Suppliers_Branches_BranchId",
                table: "Suppliers");

            migrationBuilder.DropForeignKey(
                name: "FK_TableSessions_Branches_BranchId",
                table: "TableSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_WasteLogs_Branches_BranchId",
                table: "WasteLogs");

            migrationBuilder.DropTable(
                name: "Branches");

            migrationBuilder.DropIndex(
                name: "IX_WasteLogs_BranchId",
                table: "WasteLogs");

            migrationBuilder.DropIndex(
                name: "IX_TableSessions_BranchId",
                table: "TableSessions");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_BranchId",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_RestaurantTables_BranchId",
                table: "RestaurantTables");

            migrationBuilder.DropIndex(
                name: "IX_RestaurantSettings_BranchId",
                table: "RestaurantSettings");

            migrationBuilder.DropIndex(
                name: "IX_Orders_BranchId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_MenuItems_BranchId",
                table: "MenuItems");

            migrationBuilder.DropIndex(
                name: "IX_MenuCategories_BranchId",
                table: "MenuCategories");

            migrationBuilder.DropIndex(
                name: "IX_Ingredients_BranchId",
                table: "Ingredients");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_BranchId",
                table: "Expenses");

            migrationBuilder.DropIndex(
                name: "IX_CashierShifts_BranchId",
                table: "CashierShifts");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_BranchId",
                table: "AspNetUsers");

            migrationBuilder.DeleteData(
                table: "RestaurantSettings",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "WasteLogs");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "TableSessions");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "RestaurantTables");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "RestaurantSettings");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "MenuCategories");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "Ingredients");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "CashierShifts");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "AspNetUsers");
        }
    }
}
