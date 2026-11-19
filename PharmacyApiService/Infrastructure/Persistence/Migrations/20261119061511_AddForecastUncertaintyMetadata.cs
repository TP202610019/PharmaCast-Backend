using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmacyApiService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddForecastUncertaintyMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "confidence_level",
                table: "prediction_executions",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "product_categories",
                table: "prediction_executions",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "confidence_level",
                table: "prediction_executions");

            migrationBuilder.DropColumn(
                name: "product_categories",
                table: "prediction_executions");
        }
    }
}
