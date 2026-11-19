using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmacyApiService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchasePlanUnitCost : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "unit_purchase_cost",
                table: "purchase_plan_items",
                type: "numeric",
                nullable: true);
            migrationBuilder.Sql("""
                INSERT INTO mapping_fields (id, field_key, display_name, field_type, is_required, is_active, created_at)
                VALUES ('80782f49-2677-4ee4-9cde-361bda5ca3f4', 'purchase_cost', 'Costo unitario de compra', 'inventory', FALSE, TRUE, NOW())
                ON CONFLICT (field_key) DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM mapping_fields WHERE id = '80782f49-2677-4ee4-9cde-361bda5ca3f4';");
            migrationBuilder.DropColumn(
                name: "unit_purchase_cost",
                table: "purchase_plan_items");
        }
    }
}
