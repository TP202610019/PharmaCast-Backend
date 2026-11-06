using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmacyApiService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPeriodVolumeMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "absolute_total_error",
                table: "prediction_metrics",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "actual_total",
                table: "prediction_metrics",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "predicted_total",
                table: "prediction_metrics",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "total_error_percentage",
                table: "prediction_metrics",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "absolute_total_error",
                table: "prediction_metrics");

            migrationBuilder.DropColumn(
                name: "actual_total",
                table: "prediction_metrics");

            migrationBuilder.DropColumn(
                name: "predicted_total",
                table: "prediction_metrics");

            migrationBuilder.DropColumn(
                name: "total_error_percentage",
                table: "prediction_metrics");
        }
    }
}
