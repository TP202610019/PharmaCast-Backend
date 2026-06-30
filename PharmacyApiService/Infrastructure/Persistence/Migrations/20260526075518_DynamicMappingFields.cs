using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmacyApiService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DynamicMappingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "category_column",
                table: "mapping_configurations");

            migrationBuilder.DropColumn(
                name: "date_column",
                table: "mapping_configurations");

            migrationBuilder.DropColumn(
                name: "product_column",
                table: "mapping_configurations");

            migrationBuilder.DropColumn(
                name: "quantity_column",
                table: "mapping_configurations");

            migrationBuilder.DropColumn(
                name: "supplier_column",
                table: "mapping_configurations");

            migrationBuilder.CreateTable(
                name: "mapping_fields",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    field_key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_required = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mapping_fields", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "mapping_fields",
                columns: new[] { "id", "field_key", "label", "description", "is_required", "sort_order", "created_at" },
                values: new object[,]
                {
                    { new Guid("a1b2c3d4-0001-0001-0001-000000000001"), "date",     "Fecha de Venta",  "Columna con la fecha de la venta",              true,  1, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a1b2c3d4-0001-0001-0001-000000000002"), "product",  "Producto / SKU",  "Columna con el identificador o nombre del producto", true,  2, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a1b2c3d4-0001-0001-0001-000000000003"), "quantity", "Cantidad",        "Columna con las unidades vendidas",             true,  3, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a1b2c3d4-0001-0001-0001-000000000004"), "price",    "Precio Unitario", "Columna con el precio de venta unitario",       false, 4, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a1b2c3d4-0001-0001-0001-000000000005"), "category", "Categoría",       "Columna con la categoría del producto",         false, 5, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a1b2c3d4-0001-0001-0001-000000000006"), "discount", "Descuento / Promo","Columna con indicador de descuento o promoción",false, 6, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("a1b2c3d4-0001-0001-0001-000000000007"), "stock",    "Stock",           "Columna con el nivel de stock disponible",      false, 7, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                });

            migrationBuilder.CreateTable(
                name: "mapping_configuration_fields",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mapping_configuration_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mapping_field_id = table.Column<Guid>(type: "uuid", nullable: false),
                    column_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mapping_configuration_fields", x => x.id);
                    table.ForeignKey(
                        name: "fk_mapping_configuration_fields_mapping_configurations_mapping",
                        column: x => x.mapping_configuration_id,
                        principalTable: "mapping_configurations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_mapping_configuration_fields_mapping_fields_mapping_field_id",
                        column: x => x.mapping_field_id,
                        principalTable: "mapping_fields",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_mapping_configuration_fields_mapping_configuration_id",
                table: "mapping_configuration_fields",
                column: "mapping_configuration_id");

            migrationBuilder.CreateIndex(
                name: "ix_mapping_configuration_fields_mapping_configuration_id_mappi",
                table: "mapping_configuration_fields",
                columns: new[] { "mapping_configuration_id", "mapping_field_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_mapping_configuration_fields_mapping_field_id",
                table: "mapping_configuration_fields",
                column: "mapping_field_id");

            migrationBuilder.CreateIndex(
                name: "ix_mapping_fields_field_key",
                table: "mapping_fields",
                column: "field_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_mapping_fields_sort_order",
                table: "mapping_fields",
                column: "sort_order");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mapping_configuration_fields");

            migrationBuilder.DropTable(
                name: "mapping_fields");

            migrationBuilder.AddColumn<string>(
                name: "category_column",
                table: "mapping_configurations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "date_column",
                table: "mapping_configurations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "product_column",
                table: "mapping_configurations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "quantity_column",
                table: "mapping_configurations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "supplier_column",
                table: "mapping_configurations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }
    }
}
