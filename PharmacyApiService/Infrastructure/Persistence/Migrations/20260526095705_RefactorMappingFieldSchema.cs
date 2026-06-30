using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmacyApiService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RefactorMappingFieldSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_mapping_fields_sort_order",
                table: "mapping_fields");

            migrationBuilder.DropColumn(
                name: "description",
                table: "mapping_fields");

            migrationBuilder.DropColumn(
                name: "sort_order",
                table: "mapping_fields");

            migrationBuilder.RenameColumn(
                name: "label",
                table: "mapping_fields",
                newName: "display_name");

            migrationBuilder.RenameColumn(
                name: "column_name",
                table: "mapping_configuration_fields",
                newName: "dataset_column_name");

            migrationBuilder.AddColumn<string>(
                name: "field_type",
                table: "mapping_fields",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "mapping_fields",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            // ── Data migration: populate field_type and refresh display names ────
            migrationBuilder.Sql(@"
                UPDATE mapping_fields SET field_type = 'sales'
                    WHERE field_key IN ('date', 'product', 'quantity', 'price', 'category', 'discount');

                UPDATE mapping_fields SET field_type = 'inventory'
                    WHERE field_key = 'stock';

                UPDATE mapping_fields SET display_name = 'Cantidad Vendida'
                    WHERE field_key = 'quantity';

                UPDATE mapping_fields SET display_name = 'Stock Actual'
                    WHERE field_key = 'stock';

                UPDATE mapping_fields SET is_active = false
                    WHERE field_key = 'discount';

                INSERT INTO mapping_fields (id, field_key, display_name, field_type, is_required, is_active, created_at)
                VALUES ('a1b2c3d4-0001-0001-0001-000000000008', 'min_stock', 'Stock Mínimo', 'inventory', false, true, TIMESTAMPTZ '2026-01-01T00:00:00Z')
                ON CONFLICT (field_key) DO NOTHING;
            ");

            migrationBuilder.CreateIndex(
                name: "ix_mapping_fields_field_type",
                table: "mapping_fields",
                column: "field_type");

            migrationBuilder.CreateIndex(
                name: "ix_mapping_fields_is_active",
                table: "mapping_fields",
                column: "is_active");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_mapping_fields_field_type",
                table: "mapping_fields");

            migrationBuilder.DropIndex(
                name: "ix_mapping_fields_is_active",
                table: "mapping_fields");

            migrationBuilder.DropColumn(
                name: "field_type",
                table: "mapping_fields");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "mapping_fields");

            migrationBuilder.RenameColumn(
                name: "display_name",
                table: "mapping_fields",
                newName: "label");

            migrationBuilder.RenameColumn(
                name: "dataset_column_name",
                table: "mapping_configuration_fields",
                newName: "column_name");

            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "mapping_fields",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "sort_order",
                table: "mapping_fields",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "ix_mapping_fields_sort_order",
                table: "mapping_fields",
                column: "sort_order");
        }
    }
}
