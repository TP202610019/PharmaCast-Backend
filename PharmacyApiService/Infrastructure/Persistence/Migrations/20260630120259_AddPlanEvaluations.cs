using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmacyApiService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanEvaluations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // historical_daily_points already exists in the DB — created by a prior migration applied manually
            migrationBuilder.CreateTable(
                name: "plan_evaluations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    purchase_plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    evaluated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    total_products = table.Column<int>(type: "integer", nullable: false),
                    has_pharmacy_plan = table.Column<bool>(type: "boolean", nullable: false),
                    has_price = table.Column<bool>(type: "boolean", nullable: false),
                    pharma_cast_good_count = table.Column<int>(type: "integer", nullable: false),
                    pharma_cast_good_pct = table.Column<double>(type: "double precision", nullable: false),
                    pharma_cast_wape = table.Column<double>(type: "double precision", nullable: false),
                    pharmacy_good_count = table.Column<int>(type: "integer", nullable: true),
                    pharmacy_good_pct = table.Column<double>(type: "double precision", nullable: true),
                    pharmacy_wape = table.Column<double>(type: "double precision", nullable: true),
                    pharma_cast_total_sobrestock_soles = table.Column<double>(type: "double precision", nullable: false),
                    pharma_cast_total_desabasto_soles = table.Column<double>(type: "double precision", nullable: false),
                    pharmacy_total_sobrestock_soles = table.Column<double>(type: "double precision", nullable: true),
                    pharmacy_total_desabasto_soles = table.Column<double>(type: "double precision", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plan_evaluations", x => x.id);
                    table.ForeignKey(
                        name: "fk_plan_evaluations_purchase_plans_purchase_plan_id",
                        column: x => x.purchase_plan_id,
                        principalTable: "purchase_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "plan_evaluation_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_evaluation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_name = table.Column<string>(type: "text", nullable: false),
                    actual_sold = table.Column<double>(type: "double precision", nullable: false),
                    pharma_cast_plan = table.Column<double>(type: "double precision", nullable: false),
                    pharmacy_plan = table.Column<double>(type: "double precision", nullable: true),
                    pharma_cast_accuracy = table.Column<string>(type: "text", nullable: false),
                    pharmacy_accuracy = table.Column<string>(type: "text", nullable: true),
                    wape = table.Column<double>(type: "double precision", nullable: false),
                    pharmacy_wape = table.Column<double>(type: "double precision", nullable: true),
                    sobrestock_soles = table.Column<double>(type: "double precision", nullable: false),
                    desabasto_soles = table.Column<double>(type: "double precision", nullable: false),
                    pharmacy_sobrestock_soles = table.Column<double>(type: "double precision", nullable: true),
                    pharmacy_desabasto_soles = table.Column<double>(type: "double precision", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plan_evaluation_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_plan_evaluation_items_plan_evaluations_plan_evaluation_id",
                        column: x => x.plan_evaluation_id,
                        principalTable: "plan_evaluations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_plan_evaluation_items_plan_evaluation_id",
                table: "plan_evaluation_items",
                column: "plan_evaluation_id");

            migrationBuilder.CreateIndex(
                name: "ix_plan_evaluations_purchase_plan_id",
                table: "plan_evaluations",
                column: "purchase_plan_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "plan_evaluation_items");

            migrationBuilder.DropTable(
                name: "plan_evaluations");
        }
    }
}
