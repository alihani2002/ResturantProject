using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Resturant.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWaiterToTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WaiterId",
                table: "RestaurantTables",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantTables_WaiterId",
                table: "RestaurantTables",
                column: "WaiterId");

            migrationBuilder.AddForeignKey(
                name: "FK_RestaurantTables_AspNetUsers_WaiterId",
                table: "RestaurantTables",
                column: "WaiterId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RestaurantTables_AspNetUsers_WaiterId",
                table: "RestaurantTables");

            migrationBuilder.DropIndex(
                name: "IX_RestaurantTables_WaiterId",
                table: "RestaurantTables");

            migrationBuilder.DropColumn(
                name: "WaiterId",
                table: "RestaurantTables");
        }
    }
}
