using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmacyApiService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenamePriceToUnitPrice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // "price" was ambiguous in the raw POS data (unit price vs. whole-pack
            // price) — the ML service now expects this field pre-normalized to a
            // per-unit price, so the field_key is renamed to say that explicitly.
            // Safe to rename in place: mapping_configuration_fields references
            // MappingField by id (GUID), not by this string (see UnifyMappingFieldKeys).
            migrationBuilder.Sql(@"
                UPDATE mapping_fields SET field_key = 'unit_price' WHERE field_key = 'price';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE mapping_fields SET field_key = 'price' WHERE field_key = 'unit_price';
            ");
        }
    }
}
