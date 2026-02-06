using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Resturant.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNewsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_QrCodes_RestaurantTables_RestaurantTableId",
                table: "QrCodes");

            migrationBuilder.DropIndex(
                name: "IX_QrCodes_RestaurantTableId",
                table: "QrCodes");

            migrationBuilder.DropColumn(
                name: "Url",
                table: "RestaurantTables");

            migrationBuilder.DropColumn(
                name: "RestaurantTableId",
                table: "QrCodes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Url",
                table: "RestaurantTables",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RestaurantTableId",
                table: "QrCodes",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_QrCodes_RestaurantTableId",
                table: "QrCodes",
                column: "RestaurantTableId");

            migrationBuilder.AddForeignKey(
                name: "FK_QrCodes_RestaurantTables_RestaurantTableId",
                table: "QrCodes",
                column: "RestaurantTableId",
                principalTable: "RestaurantTables",
                principalColumn: "Id");
        }
    }
}
