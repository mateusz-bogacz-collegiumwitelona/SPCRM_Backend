using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class addSteelGrade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SteelGrade",
                table: "Products");

            migrationBuilder.AddColumn<Guid>(
                name: "SteelGradeId",
                table: "Products",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "SteelGrades",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Standard = table.Column<string>(type: "text", nullable: true),
                    Density = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SteelGrades", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Products_SteelGradeId",
                table: "Products",
                column: "SteelGradeId");

            migrationBuilder.CreateIndex(
                name: "IX_SteelGrades_Name",
                table: "SteelGrades",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Products_SteelGrades_SteelGradeId",
                table: "Products",
                column: "SteelGradeId",
                principalTable: "SteelGrades",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Products_SteelGrades_SteelGradeId",
                table: "Products");

            migrationBuilder.DropTable(
                name: "SteelGrades");

            migrationBuilder.DropIndex(
                name: "IX_Products_SteelGradeId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "SteelGradeId",
                table: "Products");

            migrationBuilder.AddColumn<string>(
                name: "SteelGrade",
                table: "Products",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
