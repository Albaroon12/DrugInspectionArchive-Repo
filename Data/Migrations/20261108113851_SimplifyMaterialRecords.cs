using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DrugInspectionArchive.Data.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyMaterialRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BookMaterials_InspectionBookId",
                table: "BookMaterials");

            migrationBuilder.DropIndex(
                name: "IX_BookMaterials_MaterialId_InspectionBookId",
                table: "BookMaterials");

            migrationBuilder.DropColumn(
                name: "ExpiryDate",
                table: "BookMaterials");

            migrationBuilder.DropColumn(
                name: "IssueDate",
                table: "BookMaterials");

            migrationBuilder.AlterColumn<string>(
                name: "NormalizedBatchNumber",
                table: "BookMaterials",
                type: "TEXT",
                maxLength: 120,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 120,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BatchNumber",
                table: "BookMaterials",
                type: "TEXT",
                maxLength: 120,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 120,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookMaterials_InspectionBookId_MaterialId",
                table: "BookMaterials",
                columns: new[] { "InspectionBookId", "MaterialId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookMaterials_MaterialId",
                table: "BookMaterials",
                column: "MaterialId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BookMaterials_InspectionBookId_MaterialId",
                table: "BookMaterials");

            migrationBuilder.DropIndex(
                name: "IX_BookMaterials_MaterialId",
                table: "BookMaterials");

            migrationBuilder.AlterColumn<string>(
                name: "NormalizedBatchNumber",
                table: "BookMaterials",
                type: "TEXT",
                maxLength: 120,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 120);

            migrationBuilder.AlterColumn<string>(
                name: "BatchNumber",
                table: "BookMaterials",
                type: "TEXT",
                maxLength: 120,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 120);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ExpiryDate",
                table: "BookMaterials",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "IssueDate",
                table: "BookMaterials",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookMaterials_InspectionBookId",
                table: "BookMaterials",
                column: "InspectionBookId");

            migrationBuilder.CreateIndex(
                name: "IX_BookMaterials_MaterialId_InspectionBookId",
                table: "BookMaterials",
                columns: new[] { "MaterialId", "InspectionBookId" });
        }
    }
}
