using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Birmarket.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class StorePaymentProviderToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProviderToken",
                table: "Orders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_ProviderToken",
                table: "Orders",
                column: "ProviderToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_ProviderToken",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ProviderToken",
                table: "Orders");
        }
    }
}
