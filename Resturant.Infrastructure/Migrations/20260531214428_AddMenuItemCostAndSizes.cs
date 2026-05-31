using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Resturant.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMenuItemCostAndSizes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MenuItemSizeId",
                table: "OrderItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SizeName",
                table: "OrderItems",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitCost",
                table: "OrderItems",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Cost",
                table: "MenuItems",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "MenuItemSizes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MenuItemId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Cost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastUpdatedById = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastUpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuItemSizes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MenuItemSizes_MenuItems_MenuItemId",
                        column: x => x.MenuItemId,
                        principalTable: "MenuItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_MenuItemSizeId",
                table: "OrderItems",
                column: "MenuItemSizeId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemSizes_MenuItemId",
                table: "MenuItemSizes",
                column: "MenuItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderItems_MenuItemSizes_MenuItemSizeId",
                table: "OrderItems",
                column: "MenuItemSizeId",
                principalTable: "MenuItemSizes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrderItems_MenuItemSizes_MenuItemSizeId",
                table: "OrderItems");

            migrationBuilder.DropTable(
                name: "MenuItemSizes");

            migrationBuilder.DropIndex(
                name: "IX_OrderItems_MenuItemSizeId",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "MenuItemSizeId",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "SizeName",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "UnitCost",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "Cost",
                table: "MenuItems");
        }
    }
}
