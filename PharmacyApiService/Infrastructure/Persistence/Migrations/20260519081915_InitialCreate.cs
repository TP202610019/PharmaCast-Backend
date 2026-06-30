using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmacyApiService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "mapping_configurations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    configuration_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    product_column = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    quantity_column = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    date_column = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    category_column = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    supplier_column = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mapping_configurations", x => x.id);
                    table.ForeignKey(
                        name: "fk_mapping_configurations_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    refresh_token = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_revoked = table.Column<bool>(type: "boolean", nullable: false),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_sessions", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_sessions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "datasets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    uploaded_by = table.Column<Guid>(type: "uuid", nullable: false),
                    mapping_configuration_id = table.Column<Guid>(type: "uuid", nullable: true),
                    original_file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    stored_file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    blob_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    blob_container = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    file_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    dataset_stage = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    upload_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    rows_count = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_datasets", x => x.id);
                    table.ForeignKey(
                        name: "fk_datasets_mapping_configurations_mapping_configuration_id",
                        column: x => x.mapping_configuration_id,
                        principalTable: "mapping_configurations",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_datasets_users_uploaded_by",
                        column: x => x.uploaded_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "prediction_executions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dataset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    executed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    forecast_period = table.Column<int>(type: "integer", nullable: false),
                    execution_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error_message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_prediction_executions", x => x.id);
                    table.ForeignKey(
                        name: "fk_prediction_executions_datasets_dataset_id",
                        column: x => x.dataset_id,
                        principalTable: "datasets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_prediction_executions_users_executed_by",
                        column: x => x.executed_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "forecast_results",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    prediction_execution_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    forecast_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    predicted_quantity = table.Column<double>(type: "double precision", nullable: false),
                    lower_bound = table.Column<double>(type: "double precision", nullable: true),
                    upper_bound = table.Column<double>(type: "double precision", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_forecast_results", x => x.id);
                    table.ForeignKey(
                        name: "fk_forecast_results_prediction_executions_prediction_execution",
                        column: x => x.prediction_execution_id,
                        principalTable: "prediction_executions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "generated_reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    prediction_execution_id = table.Column<Guid>(type: "uuid", nullable: false),
                    report_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    file_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    blob_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    blob_container = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    generated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    generated_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_generated_reports", x => x.id);
                    table.ForeignKey(
                        name: "fk_generated_reports_prediction_executions_prediction_executio",
                        column: x => x.prediction_execution_id,
                        principalTable: "prediction_executions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "prediction_metrics",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    prediction_execution_id = table.Column<Guid>(type: "uuid", nullable: false),
                    metric_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    value = table.Column<double>(type: "double precision", nullable: false),
                    product_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_prediction_metrics", x => x.id);
                    table.ForeignKey(
                        name: "fk_prediction_metrics_prediction_executions_prediction_executi",
                        column: x => x.prediction_execution_id,
                        principalTable: "prediction_executions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "purchase_plans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    prediction_execution_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    generated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_purchase_plans", x => x.id);
                    table.ForeignKey(
                        name: "fk_purchase_plans_prediction_executions_prediction_execution_id",
                        column: x => x.prediction_execution_id,
                        principalTable: "prediction_executions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "purchase_plan_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    recommended_quantity = table.Column<double>(type: "double precision", nullable: false),
                    category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    supplier = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    estimated_cost = table.Column<double>(type: "double precision", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_purchase_plan_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_purchase_plan_items_purchase_plans_purchase_plan_id",
                        column: x => x.purchase_plan_id,
                        principalTable: "purchase_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_datasets_created_at",
                table: "datasets",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_datasets_dataset_stage",
                table: "datasets",
                column: "dataset_stage");

            migrationBuilder.CreateIndex(
                name: "ix_datasets_mapping_configuration_id",
                table: "datasets",
                column: "mapping_configuration_id");

            migrationBuilder.CreateIndex(
                name: "ix_datasets_upload_status",
                table: "datasets",
                column: "upload_status");

            migrationBuilder.CreateIndex(
                name: "ix_datasets_uploaded_by",
                table: "datasets",
                column: "uploaded_by");

            migrationBuilder.CreateIndex(
                name: "ix_datasets_uploaded_by_created_at",
                table: "datasets",
                columns: new[] { "uploaded_by", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_forecast_results_prediction_execution_id",
                table: "forecast_results",
                column: "prediction_execution_id");

            migrationBuilder.CreateIndex(
                name: "ix_forecast_results_prediction_execution_id_forecast_date",
                table: "forecast_results",
                columns: new[] { "prediction_execution_id", "forecast_date" });

            migrationBuilder.CreateIndex(
                name: "ix_forecast_results_prediction_execution_id_product_name",
                table: "forecast_results",
                columns: new[] { "prediction_execution_id", "product_name" });

            migrationBuilder.CreateIndex(
                name: "ix_generated_reports_generated_at",
                table: "generated_reports",
                column: "generated_at");

            migrationBuilder.CreateIndex(
                name: "ix_generated_reports_generated_by",
                table: "generated_reports",
                column: "generated_by");

            migrationBuilder.CreateIndex(
                name: "ix_generated_reports_generated_by_generated_at",
                table: "generated_reports",
                columns: new[] { "generated_by", "generated_at" });

            migrationBuilder.CreateIndex(
                name: "ix_generated_reports_prediction_execution_id",
                table: "generated_reports",
                column: "prediction_execution_id");

            migrationBuilder.CreateIndex(
                name: "ix_generated_reports_prediction_execution_id_report_type",
                table: "generated_reports",
                columns: new[] { "prediction_execution_id", "report_type" });

            migrationBuilder.CreateIndex(
                name: "ix_mapping_configurations_user_id",
                table: "mapping_configurations",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_mapping_configurations_user_id_configuration_name",
                table: "mapping_configurations",
                columns: new[] { "user_id", "configuration_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_mapping_configurations_user_id_is_default",
                table: "mapping_configurations",
                columns: new[] { "user_id", "is_default" });

            migrationBuilder.CreateIndex(
                name: "ix_prediction_executions_created_at",
                table: "prediction_executions",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_prediction_executions_dataset_id",
                table: "prediction_executions",
                column: "dataset_id");

            migrationBuilder.CreateIndex(
                name: "ix_prediction_executions_dataset_id_execution_status",
                table: "prediction_executions",
                columns: new[] { "dataset_id", "execution_status" });

            migrationBuilder.CreateIndex(
                name: "ix_prediction_executions_executed_by",
                table: "prediction_executions",
                column: "executed_by");

            migrationBuilder.CreateIndex(
                name: "ix_prediction_executions_executed_by_created_at",
                table: "prediction_executions",
                columns: new[] { "executed_by", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_prediction_executions_execution_status",
                table: "prediction_executions",
                column: "execution_status");

            migrationBuilder.CreateIndex(
                name: "ix_prediction_metrics_prediction_execution_id",
                table: "prediction_metrics",
                column: "prediction_execution_id");

            migrationBuilder.CreateIndex(
                name: "ix_prediction_metrics_prediction_execution_id_metric_name",
                table: "prediction_metrics",
                columns: new[] { "prediction_execution_id", "metric_name" });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_plan_items_purchase_plan_id",
                table: "purchase_plan_items",
                column: "purchase_plan_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_plan_items_purchase_plan_id_product_name",
                table: "purchase_plan_items",
                columns: new[] { "purchase_plan_id", "product_name" });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_plans_prediction_execution_id",
                table: "purchase_plans",
                column: "prediction_execution_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_sessions_expires_at",
                table: "user_sessions",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_user_sessions_refresh_token",
                table: "user_sessions",
                column: "refresh_token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_sessions_user_id",
                table: "user_sessions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_sessions_user_id_is_revoked",
                table: "user_sessions",
                columns: new[] { "user_id", "is_revoked" });

            migrationBuilder.CreateIndex(
                name: "ix_users_created_at",
                table: "users",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_is_active",
                table: "users",
                column: "is_active");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "forecast_results");

            migrationBuilder.DropTable(
                name: "generated_reports");

            migrationBuilder.DropTable(
                name: "prediction_metrics");

            migrationBuilder.DropTable(
                name: "purchase_plan_items");

            migrationBuilder.DropTable(
                name: "user_sessions");

            migrationBuilder.DropTable(
                name: "purchase_plans");

            migrationBuilder.DropTable(
                name: "prediction_executions");

            migrationBuilder.DropTable(
                name: "datasets");

            migrationBuilder.DropTable(
                name: "mapping_configurations");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
