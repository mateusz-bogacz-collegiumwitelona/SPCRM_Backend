using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class addContactToDeal4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Deals_Contacts_ContactId1",
                table: "Deals");

            migrationBuilder.DropIndex(
                name: "IX_Deals_ContactId1",
                table: "Deals");

            migrationBuilder.DropColumn(
                name: "ContactId1",
                table: "Deals");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ContactId1",
                table: "Deals",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Deals_ContactId1",
                table: "Deals",
                column: "ContactId1");

            migrationBuilder.AddForeignKey(
                name: "FK_Deals_Contacts_ContactId1",
                table: "Deals",
                column: "ContactId1",
                principalTable: "Contacts",
                principalColumn: "Id");
        }
    }
}
