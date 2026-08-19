using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class betterPromotionsModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Promotions_ContactId",
                table: "Promotions",
                column: "ContactId");

            migrationBuilder.AddForeignKey(
                name: "FK_Promotions_Contacts_ContactId",
                table: "Promotions",
                column: "ContactId",
                principalTable: "Contacts",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Promotions_Contacts_ContactId",
                table: "Promotions");

            migrationBuilder.DropIndex(
                name: "IX_Promotions_ContactId",
                table: "Promotions");
        }
    }
}
