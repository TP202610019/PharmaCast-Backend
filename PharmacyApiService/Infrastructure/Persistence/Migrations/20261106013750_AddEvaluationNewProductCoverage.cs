using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmacyApiService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEvaluationNewProductCoverage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "new_products_count",
                table: "prediction_executions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "new_products_units",
                table: "prediction_executions",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "new_products_count",
                table: "prediction_executions");

            migrationBuilder.DropColumn(
                name: "new_products_units",
                table: "prediction_executions");
        }
    }
}
