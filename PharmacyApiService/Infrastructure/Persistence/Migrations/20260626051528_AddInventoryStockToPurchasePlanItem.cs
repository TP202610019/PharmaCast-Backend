using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmacyApiService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryStockToPurchasePlanItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "demand_predicted",
                table: "purchase_plan_items",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "stock_actual",
                table: "purchase_plan_items",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "to_purchase",
                table: "purchase_plan_items",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "demand_predicted",
                table: "purchase_plan_items");

            migrationBuilder.DropColumn(
                name: "stock_actual",
                table: "purchase_plan_items");

            migrationBuilder.DropColumn(
                name: "to_purchase",
                table: "purchase_plan_items");
        }
    }
}
