using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskDeduplicationAndConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_tasks_UserId",
                table: "tasks");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "tasks",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "CreatedDate",
                table: "tasks",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<string>(
                name: "NormalizedTitle",
                table: "tasks",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "tasks",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.Sql(
                "UPDATE tasks SET NormalizedTitle = UPPER(LTRIM(RTRIM(Title))), " +
                "CreatedDate = CAST(CreatedAt AS date);");

            migrationBuilder.CreateIndex(
                name: "IX_tasks_UserId_NormalizedTitle_CreatedDate",
                table: "tasks",
                columns: new[] { "UserId", "NormalizedTitle", "CreatedDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_tasks_UserId_NormalizedTitle_CreatedDate",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "CreatedDate",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "NormalizedTitle",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "tasks");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "tasks",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_tasks_UserId",
                table: "tasks",
                column: "UserId");
        }
    }
}
