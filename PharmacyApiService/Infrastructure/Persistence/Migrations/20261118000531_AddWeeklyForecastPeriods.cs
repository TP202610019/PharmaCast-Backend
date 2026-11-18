using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmacyApiService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWeeklyForecastPeriods : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "forecast_frequency",
                table: "prediction_executions",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "Daily");

            migrationBuilder.AddColumn<DateTime>(
                name: "period_end",
                table: "forecast_results",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "forecast_frequency",
                table: "prediction_executions");

            migrationBuilder.DropColumn(
                name: "period_end",
                table: "forecast_results");
        }
    }
}
