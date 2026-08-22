using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class addCurrencyToPromotions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CurrencyId",
                table: "Promotions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Promotions_CurrencyId",
                table: "Promotions",
                column: "CurrencyId");

            migrationBuilder.AddForeignKey(
                name: "FK_Promotions_Currencies_CurrencyId",
                table: "Promotions",
                column: "CurrencyId",
                principalTable: "Currencies",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Promotions_Currencies_CurrencyId",
                table: "Promotions");

            migrationBuilder.DropIndex(
                name: "IX_Promotions_CurrencyId",
                table: "Promotions");

            migrationBuilder.DropColumn(
                name: "CurrencyId",
                table: "Promotions");
        }
    }
}
