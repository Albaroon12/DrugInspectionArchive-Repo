using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DrugInspectionArchive.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Companies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Companies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Materials",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CanonicalName = table.Column<string>(type: "TEXT", maxLength: 250, nullable: false),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 250, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Materials", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InspectionBooks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CompanyId = table.Column<int>(type: "INTEGER", nullable: false),
                    BookNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    NormalizedBookNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    BookDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    AddedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    OriginalFileName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    PdfRelativePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    FileSha256 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    PageCount = table.Column<int>(type: "INTEGER", nullable: true),
                    OcrText = table.Column<string>(type: "TEXT", nullable: true),
                    OcrStatus = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionBooks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InspectionBooks_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BookMaterials",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    InspectionBookId = table.Column<int>(type: "INTEGER", nullable: false),
                    MaterialId = table.Column<int>(type: "INTEGER", nullable: false),
                    OriginalMaterialName = table.Column<string>(type: "TEXT", maxLength: 250, nullable: true),
                    BatchNumber = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    NormalizedBatchNumber = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    IssueDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    ExpiryDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    PageNumber = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookMaterials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BookMaterials_InspectionBooks_InspectionBookId",
                        column: x => x.InspectionBookId,
                        principalTable: "InspectionBooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BookMaterials_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BookMaterials_InspectionBookId",
                table: "BookMaterials",
                column: "InspectionBookId");

            migrationBuilder.CreateIndex(
                name: "IX_BookMaterials_MaterialId_InspectionBookId",
                table: "BookMaterials",
                columns: new[] { "MaterialId", "InspectionBookId" });

            migrationBuilder.CreateIndex(
                name: "IX_BookMaterials_NormalizedBatchNumber",
                table: "BookMaterials",
                column: "NormalizedBatchNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Companies_NormalizedName",
                table: "Companies",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InspectionBooks_CompanyId_IsDeleted_BookDate",
                table: "InspectionBooks",
                columns: new[] { "CompanyId", "IsDeleted", "BookDate" });

            migrationBuilder.CreateIndex(
                name: "IX_InspectionBooks_CompanyId_NormalizedBookNumber",
                table: "InspectionBooks",
                columns: new[] { "CompanyId", "NormalizedBookNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_InspectionBooks_FileSha256",
                table: "InspectionBooks",
                column: "FileSha256");

            migrationBuilder.CreateIndex(
                name: "IX_Materials_NormalizedName",
                table: "Materials",
                column: "NormalizedName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookMaterials");

            migrationBuilder.DropTable(
                name: "InspectionBooks");

            migrationBuilder.DropTable(
                name: "Materials");

            migrationBuilder.DropTable(
                name: "Companies");
        }
    }
}
