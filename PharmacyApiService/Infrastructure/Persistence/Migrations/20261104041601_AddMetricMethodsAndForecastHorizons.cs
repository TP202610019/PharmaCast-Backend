using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PharmacyApiService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMetricMethodsAndForecastHorizons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Refuse ambiguous legacy results; preserve every row for manual review.
            // This is only evaluated when an operator explicitly applies migrations.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM forecast_results
                        GROUP BY prediction_execution_id, product_name, forecast_date HAVING COUNT(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'Duplicate legacy forecast_results found. Review the preflight queries in BACKEND_API_CHANGES.md before applying this migration; no results were deleted.';
                    END IF;
                    IF EXISTS (
                        SELECT 1 FROM prediction_metrics
                        GROUP BY prediction_execution_id, product_name
                        HAVING COUNT(*) > 1 AND COUNT(DISTINCT (wape, mae, rmse, mape, rows, historical_avg)) > 1
                    ) THEN
                        RAISE EXCEPTION 'Conflicting legacy prediction_metrics found; manual review required.';
                    END IF;
                    -- Keep complete original records before removing exact metric copies.
                    CREATE TABLE prediction_metrics_duplicate_archive AS
                    SELECT original.* FROM prediction_metrics original
                    JOIN (
                        SELECT id, ROW_NUMBER() OVER (
                            PARTITION BY prediction_execution_id, product_name ORDER BY id
                        ) AS duplicate_rank FROM prediction_metrics
                    ) ranked ON original.id = ranked.id WHERE ranked.duplicate_rank > 1;
                    DELETE FROM prediction_metrics active USING prediction_metrics_duplicate_archive archived
                    WHERE active.id = archived.id;
                    IF EXISTS (
                        SELECT 1 FROM prediction_metrics
                        GROUP BY prediction_execution_id, product_name HAVING COUNT(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'Duplicate legacy prediction_metrics found. Review the preflight queries in BACKEND_API_CHANGES.md before applying this migration; no metrics were deleted.';
                    END IF;
                END $$;
                """);

            migrationBuilder.AddColumn<double>(
                name: "mbe",
                table: "prediction_metrics",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "method",
                table: "prediction_metrics",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "scope",
                table: "prediction_metrics",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "artifact_paths",
                table: "prediction_executions",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<DateTime>(
                name: "evaluation_period_end",
                table: "prediction_executions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "evaluation_period_start",
                table: "prediction_executions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_retrain",
                table: "prediction_executions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "training_period_end",
                table: "prediction_executions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "training_period_start",
                table: "prediction_executions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "warnings",
                table: "prediction_executions",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "forecast_step",
                table: "forecast_results",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "forecast_horizons",
                columns: table => new
                {
                    days = table.Column<int>(type: "integer", nullable: false),
                    display_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_forecast_horizons", x => x.days);
                });

            migrationBuilder.InsertData(
                table: "forecast_horizons",
                columns: new[] { "days", "display_name", "is_active" },
                values: new object[,]
                {
                    { 30, "30 días", true },
                    { 60, "60 días", true },
                    { 90, "90 días", true }
                });

            // Existing rows predate method/scope: preserve them as XGBoost and infer
            // only the explicit overall/product distinction, never date intervals.
            migrationBuilder.Sql("""
                UPDATE prediction_metrics
                SET method = 'XGBoost',
                    scope = CASE WHEN product_name IS NULL THEN 'Overall' ELSE 'Product' END;

                UPDATE mapping_fields SET is_required = false
                WHERE field_key = 'category' AND field_type = 'sales';
                """);

            migrationBuilder.CreateIndex(
                name: "ix_prediction_metrics_prediction_execution_id_method_scope",
                table: "prediction_metrics",
                columns: new[] { "prediction_execution_id", "method", "scope" },
                unique: true,
                filter: "product_name IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_prediction_metrics_prediction_execution_id_method_scope_pro",
                table: "prediction_metrics",
                columns: new[] { "prediction_execution_id", "method", "scope", "product_name" },
                unique: true,
                filter: "product_name IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_prediction_metrics_scope_product",
                table: "prediction_metrics",
                sql: "(scope = 'Overall' AND product_name IS NULL) OR (scope = 'Product' AND product_name IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "ix_forecast_results_prediction_execution_id_product_name_forec",
                table: "forecast_results",
                columns: new[] { "prediction_execution_id", "product_name", "forecast_date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "forecast_horizons");

            migrationBuilder.DropIndex(
                name: "ix_prediction_metrics_prediction_execution_id_method_scope",
                table: "prediction_metrics");

            migrationBuilder.DropIndex(
                name: "ix_prediction_metrics_prediction_execution_id_method_scope_pro",
                table: "prediction_metrics");

            migrationBuilder.DropCheckConstraint(
                name: "ck_prediction_metrics_scope_product",
                table: "prediction_metrics");

            migrationBuilder.DropIndex(
                name: "ix_forecast_results_prediction_execution_id_product_name_forec",
                table: "forecast_results");

            migrationBuilder.DropColumn(
                name: "mbe",
                table: "prediction_metrics");

            migrationBuilder.DropColumn(
                name: "method",
                table: "prediction_metrics");

            migrationBuilder.DropColumn(
                name: "scope",
                table: "prediction_metrics");

            migrationBuilder.DropColumn(
                name: "artifact_paths",
                table: "prediction_executions");

            migrationBuilder.DropColumn(
                name: "evaluation_period_end",
                table: "prediction_executions");

            migrationBuilder.DropColumn(
                name: "evaluation_period_start",
                table: "prediction_executions");

            migrationBuilder.DropColumn(
                name: "is_retrain",
                table: "prediction_executions");

            migrationBuilder.DropColumn(
                name: "training_period_end",
                table: "prediction_executions");

            migrationBuilder.DropColumn(
                name: "training_period_start",
                table: "prediction_executions");

            migrationBuilder.DropColumn(
                name: "warnings",
                table: "prediction_executions");

            migrationBuilder.DropColumn(
                name: "forecast_step",
                table: "forecast_results");
        }
    }
}
