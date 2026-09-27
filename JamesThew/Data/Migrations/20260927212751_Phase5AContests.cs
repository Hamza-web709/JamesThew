using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JamesThew.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase5AContests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Contests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DescriptionAndRules = table.Column<string>(type: "nvarchar(max)", maxLength: 10000, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PrizeDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ImageUrl = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    OpensAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClosesAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Contests_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Contests_ClosesAtUtc",
                table: "Contests",
                column: "ClosesAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Contests_CreatedByUserId",
                table: "Contests",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Contests_OpensAtUtc",
                table: "Contests",
                column: "OpensAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Contests_Slug",
                table: "Contests",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Contests_Status",
                table: "Contests",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Contests");
        }
    }
}
