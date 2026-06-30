using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmacyApiService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RefactorMetricsToColumnar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_prediction_metrics_prediction_execution_id_metric_name",
                table: "prediction_metrics");

            migrationBuilder.DropColumn(
                name: "metric_name",
                table: "prediction_metrics");

            migrationBuilder.DropColumn(
                name: "value",
                table: "prediction_metrics");

            migrationBuilder.AddColumn<double>(
                name: "wape",
                table: "prediction_metrics",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "rmse",
                table: "prediction_metrics",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "mae",
                table: "prediction_metrics",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "mape",
                table: "prediction_metrics",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "rows",
                table: "prediction_metrics",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "wape",  table: "prediction_metrics");
            migrationBuilder.DropColumn(name: "rmse",  table: "prediction_metrics");
            migrationBuilder.DropColumn(name: "mae",   table: "prediction_metrics");
            migrationBuilder.DropColumn(name: "mape",  table: "prediction_metrics");
            migrationBuilder.DropColumn(name: "rows",  table: "prediction_metrics");

            migrationBuilder.AddColumn<string>(
                name: "metric_name",
                table: "prediction_metrics",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<double>(
                name: "value",
                table: "prediction_metrics",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.CreateIndex(
                name: "ix_prediction_metrics_prediction_execution_id_metric_name",
                table: "prediction_metrics",
                columns: new[] { "prediction_execution_id", "metric_name" });
        }
    }
}
