using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DrugInspectionArchive.Data.Migrations
{
    /// <inheritdoc />
    public partial class AllowMultipleBatchesPerMaterialInBook : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BookMaterials_InspectionBookId_MaterialId",
                table: "BookMaterials");

            migrationBuilder.CreateIndex(
                name: "IX_BookMaterials_InspectionBookId_MaterialId_NormalizedBatchNumber",
                table: "BookMaterials",
                columns: new[] { "InspectionBookId", "MaterialId", "NormalizedBatchNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BookMaterials_InspectionBookId_MaterialId_NormalizedBatchNumber",
                table: "BookMaterials");

            migrationBuilder.CreateIndex(
                name: "IX_BookMaterials_InspectionBookId_MaterialId",
                table: "BookMaterials",
                columns: new[] { "InspectionBookId", "MaterialId" },
                unique: true);
        }
    }
}
