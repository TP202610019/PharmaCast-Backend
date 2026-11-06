using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace PharmacyApiService.Infrastructure.Persistence.Migrations;

public partial class StoreHistoricalSalesByDataset : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Legacy rows may combine several uploads. Preserve their original provenance.
        migrationBuilder.RenameTable("historical_daily_points", newName: "historical_sales_daily_legacy");
        migrationBuilder.CreateTable("historical_sales_daily", columns: table => new
        {
            id = table.Column<Guid>(nullable: false),
            dataset_id = table.Column<Guid>(nullable: false),
            product_name = table.Column<string>(nullable: false),
            date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            quantity = table.Column<double>(nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("pk_historical_sales_daily", x => x.id);
            table.ForeignKey("fk_historical_sales_daily_datasets_dataset_id", x => x.dataset_id,
                "datasets", "id", onDelete: ReferentialAction.Cascade);
        });
        migrationBuilder.CreateIndex("ix_historical_sales_daily_dataset_id_product_name_date",
            "historical_sales_daily", new[] { "dataset_id", "product_name", "date" }, unique: true);
        migrationBuilder.CreateIndex("ix_historical_sales_daily_dataset_id_date",
            "historical_sales_daily", new[] { "dataset_id", "date" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $$ BEGIN
              IF EXISTS (SELECT 1 FROM historical_sales_daily) THEN
                RAISE EXCEPTION 'Archive new historical sales before reverting this migration.';
              END IF;
            END $$;
            """);
        migrationBuilder.DropTable("historical_sales_daily");
        migrationBuilder.RenameTable("historical_sales_daily_legacy", newName: "historical_daily_points");
    }
}
