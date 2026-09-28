using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JamesThew.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase5BContestEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContestEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContestId = table.Column<int>(type: "int", nullable: false),
                    AuthorUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    EntryKind = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Servings = table.Column<int>(type: "int", nullable: true),
                    PrepMinutes = table.Column<int>(type: "int", nullable: true),
                    CookMinutes = table.Column<int>(type: "int", nullable: true),
                    TipBody = table.Column<string>(type: "nvarchar(max)", maxLength: 10000, nullable: true),
                    ContributorNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ImageUrl = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContestEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContestEntries_AspNetUsers_AuthorUserId",
                        column: x => x.AuthorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContestEntries_Contests_ContestId",
                        column: x => x.ContestId,
                        principalTable: "Contests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContestEntryIngredients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContestEntryId = table.Column<int>(type: "int", nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    QuantityText = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Unit = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContestEntryIngredients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContestEntryIngredients_ContestEntries_ContestEntryId",
                        column: x => x.ContestEntryId,
                        principalTable: "ContestEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContestEntrySteps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContestEntryId = table.Column<int>(type: "int", nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    Instruction = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContestEntrySteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContestEntrySteps_ContestEntries_ContestEntryId",
                        column: x => x.ContestEntryId,
                        principalTable: "ContestEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContestEntries_AuthorUserId",
                table: "ContestEntries",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ContestEntries_ContestId",
                table: "ContestEntries",
                column: "ContestId");

            migrationBuilder.CreateIndex(
                name: "IX_ContestEntries_ContestId_AuthorUserId",
                table: "ContestEntries",
                columns: new[] { "ContestId", "AuthorUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContestEntries_Status",
                table: "ContestEntries",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ContestEntries_SubmittedAtUtc",
                table: "ContestEntries",
                column: "SubmittedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ContestEntryIngredients_ContestEntryId_Position",
                table: "ContestEntryIngredients",
                columns: new[] { "ContestEntryId", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_ContestEntrySteps_ContestEntryId_Position",
                table: "ContestEntrySteps",
                columns: new[] { "ContestEntryId", "Position" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContestEntryIngredients");

            migrationBuilder.DropTable(
                name: "ContestEntrySteps");

            migrationBuilder.DropTable(
                name: "ContestEntries");
        }
    }
}
