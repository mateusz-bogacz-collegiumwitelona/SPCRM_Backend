using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class delateUnusedTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_User_Token",
                table: "User");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_Token",
                table: "Tasks");

            migrationBuilder.DropIndex(
                name: "IX_Contacts_Token",
                table: "Contacts");

            migrationBuilder.DropIndex(
                name: "IX_Companies_Token",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "Token",
                table: "User");

            migrationBuilder.DropColumn(
                name: "Token",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "Token",
                table: "Contacts");

            migrationBuilder.DropColumn(
                name: "Token",
                table: "Companies");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Token",
                table: "User",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Token",
                table: "Tasks",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Token",
                table: "Contacts",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Token",
                table: "Companies",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_User_Token",
                table: "User",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_Token",
                table: "Tasks",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_Token",
                table: "Contacts",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Companies_Token",
                table: "Companies",
                column: "Token",
                unique: true);
        }
    }
}
