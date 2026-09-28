using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JamesThew.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase5CDurableRevocationAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WinnerRevocationReason",
                table: "Contests",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WinnerRevokedAtUtc",
                table: "Contests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WinnerRevokedByUserId",
                table: "Contests",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RevocationReason",
                table: "ContestEntries",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RevokedAtUtc",
                table: "ContestEntries",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Contests_WinnerRevokedByUserId",
                table: "Contests",
                column: "WinnerRevokedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Contests_AspNetUsers_WinnerRevokedByUserId",
                table: "Contests",
                column: "WinnerRevokedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Contests_AspNetUsers_WinnerRevokedByUserId",
                table: "Contests");

            migrationBuilder.DropIndex(
                name: "IX_Contests_WinnerRevokedByUserId",
                table: "Contests");

            migrationBuilder.DropColumn(
                name: "WinnerRevocationReason",
                table: "Contests");

            migrationBuilder.DropColumn(
                name: "WinnerRevokedAtUtc",
                table: "Contests");

            migrationBuilder.DropColumn(
                name: "WinnerRevokedByUserId",
                table: "Contests");

            migrationBuilder.DropColumn(
                name: "RevocationReason",
                table: "ContestEntries");

            migrationBuilder.DropColumn(
                name: "RevokedAtUtc",
                table: "ContestEntries");
        }
    }
}
