using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class addCurrencyToOffer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OfferProducts_Currencies_CurrencyId",
                table: "OfferProducts");

            migrationBuilder.DropIndex(
                name: "IX_OfferProducts_CurrencyId",
                table: "OfferProducts");

            migrationBuilder.DropColumn(
                name: "CurrencyId",
                table: "OfferProducts");

            migrationBuilder.AddColumn<Guid>(
                name: "CurrencyId",
                table: "Offers",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_Offers_CurrencyId",
                table: "Offers",
                column: "CurrencyId");

            migrationBuilder.AddForeignKey(
                name: "FK_Offers_Currencies_CurrencyId",
                table: "Offers",
                column: "CurrencyId",
                principalTable: "Currencies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Offers_Currencies_CurrencyId",
                table: "Offers");

            migrationBuilder.DropIndex(
                name: "IX_Offers_CurrencyId",
                table: "Offers");

            migrationBuilder.DropColumn(
                name: "CurrencyId",
                table: "Offers");

            migrationBuilder.AddColumn<Guid>(
                name: "CurrencyId",
                table: "OfferProducts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_OfferProducts_CurrencyId",
                table: "OfferProducts",
                column: "CurrencyId");

            migrationBuilder.AddForeignKey(
                name: "FK_OfferProducts_Currencies_CurrencyId",
                table: "OfferProducts",
                column: "CurrencyId",
                principalTable: "Currencies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
