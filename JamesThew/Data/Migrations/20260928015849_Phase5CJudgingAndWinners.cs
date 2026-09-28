using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JamesThew.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase5CJudgingAndWinners : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ContestEntries_ContestId",
                table: "ContestEntries");

            migrationBuilder.AddColumn<DateTime>(
                name: "WinnerAnnouncedAtUtc",
                table: "Contests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WinnerAnnouncedByUserId",
                table: "Contests",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WinnerSelectedAtUtc",
                table: "Contests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WinnerSelectedByUserId",
                table: "Contests",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WinningEntryId",
                table: "Contests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdminReviewNotes",
                table: "ContestEntries",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisqualificationReason",
                table: "ContestEntries",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAtUtc",
                table: "ContestEntries",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewedByUserId",
                table: "ContestEntries",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Contests_WinnerAnnouncedAtUtc",
                table: "Contests",
                column: "WinnerAnnouncedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Contests_WinnerAnnouncedByUserId",
                table: "Contests",
                column: "WinnerAnnouncedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Contests_WinnerSelectedByUserId",
                table: "Contests",
                column: "WinnerSelectedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Contests_WinningEntryId",
                table: "Contests",
                column: "WinningEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_ContestEntries_ContestId_SingleWinner",
                table: "ContestEntries",
                column: "ContestId",
                unique: true,
                filter: "[Status] = 4");

            migrationBuilder.CreateIndex(
                name: "IX_ContestEntries_ReviewedByUserId",
                table: "ContestEntries",
                column: "ReviewedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ContestEntries_AspNetUsers_ReviewedByUserId",
                table: "ContestEntries",
                column: "ReviewedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Contests_AspNetUsers_WinnerAnnouncedByUserId",
                table: "Contests",
                column: "WinnerAnnouncedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Contests_AspNetUsers_WinnerSelectedByUserId",
                table: "Contests",
                column: "WinnerSelectedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Contests_ContestEntries_WinningEntryId",
                table: "Contests",
                column: "WinningEntryId",
                principalTable: "ContestEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContestEntries_AspNetUsers_ReviewedByUserId",
                table: "ContestEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_Contests_AspNetUsers_WinnerAnnouncedByUserId",
                table: "Contests");

            migrationBuilder.DropForeignKey(
                name: "FK_Contests_AspNetUsers_WinnerSelectedByUserId",
                table: "Contests");

            migrationBuilder.DropForeignKey(
                name: "FK_Contests_ContestEntries_WinningEntryId",
                table: "Contests");

            migrationBuilder.DropIndex(
                name: "IX_Contests_WinnerAnnouncedAtUtc",
                table: "Contests");

            migrationBuilder.DropIndex(
                name: "IX_Contests_WinnerAnnouncedByUserId",
                table: "Contests");

            migrationBuilder.DropIndex(
                name: "IX_Contests_WinnerSelectedByUserId",
                table: "Contests");

            migrationBuilder.DropIndex(
                name: "IX_Contests_WinningEntryId",
                table: "Contests");

            migrationBuilder.DropIndex(
                name: "IX_ContestEntries_ContestId_SingleWinner",
                table: "ContestEntries");

            migrationBuilder.DropIndex(
                name: "IX_ContestEntries_ReviewedByUserId",
                table: "ContestEntries");

            migrationBuilder.DropColumn(
                name: "WinnerAnnouncedAtUtc",
                table: "Contests");

            migrationBuilder.DropColumn(
                name: "WinnerAnnouncedByUserId",
                table: "Contests");

            migrationBuilder.DropColumn(
                name: "WinnerSelectedAtUtc",
                table: "Contests");

            migrationBuilder.DropColumn(
                name: "WinnerSelectedByUserId",
                table: "Contests");

            migrationBuilder.DropColumn(
                name: "WinningEntryId",
                table: "Contests");

            migrationBuilder.DropColumn(
                name: "AdminReviewNotes",
                table: "ContestEntries");

            migrationBuilder.DropColumn(
                name: "DisqualificationReason",
                table: "ContestEntries");

            migrationBuilder.DropColumn(
                name: "ReviewedAtUtc",
                table: "ContestEntries");

            migrationBuilder.DropColumn(
                name: "ReviewedByUserId",
                table: "ContestEntries");

            migrationBuilder.CreateIndex(
                name: "IX_ContestEntries_ContestId",
                table: "ContestEntries",
                column: "ContestId");
        }
    }
}
