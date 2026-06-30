using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmacyApiService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "generated_reports");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "generated_reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    prediction_execution_id = table.Column<Guid>(type: "uuid", nullable: false),
                    blob_container = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    blob_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    file_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    generated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    generated_by = table.Column<Guid>(type: "uuid", nullable: false),
                    report_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
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
        }
    }
}
