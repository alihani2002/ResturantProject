using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Resturant.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCashierShiftAndOrderIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_ShiftId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_CashierShifts_BranchId",
                table: "CashierShifts");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_ShiftId_BranchId_Status",
                table: "Orders",
                columns: new[] { "ShiftId", "BranchId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CashierShifts_BranchId_StartTime",
                table: "CashierShifts",
                columns: new[] { "BranchId", "StartTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_ShiftId_BranchId_Status",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_CashierShifts_BranchId_StartTime",
                table: "CashierShifts");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_ShiftId",
                table: "Orders",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_CashierShifts_BranchId",
                table: "CashierShifts",
                column: "BranchId");
        }
    }
}
