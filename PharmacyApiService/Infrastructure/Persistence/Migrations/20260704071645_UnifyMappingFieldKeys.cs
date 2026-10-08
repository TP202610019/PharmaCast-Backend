using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmacyApiService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UnifyMappingFieldKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Align field_key naming with the ML service's internal canonical column
            // names, so the Backend→ML field mapping payload needs no translation.
            // Renaming the field_key text does not affect mapping_configuration_fields,
            // which references MappingField by id (GUID), not by this string.
            migrationBuilder.Sql(@"
                UPDATE mapping_fields SET field_key = 'product_id' WHERE field_key = 'product';
                UPDATE mapping_fields SET field_key = 'promotion' WHERE field_key = 'discount';

                INSERT INTO mapping_fields (id, field_key, display_name, field_type, is_required, is_active, created_at)
                VALUES
                    ('a1b2c3d4-0001-0001-0001-000000000010', 'season',   'Temporada',        'sales', false, true, TIMESTAMPTZ '2026-01-01T00:00:00Z'),
                    ('a1b2c3d4-0001-0001-0001-000000000011', 'location', 'Local / Sucursal', 'sales', false, true, TIMESTAMPTZ '2026-01-01T00:00:00Z'),
                    ('a1b2c3d4-0001-0001-0001-000000000012', 'holiday',  'Feriado',          'sales', false, true, TIMESTAMPTZ '2026-01-01T00:00:00Z')
                ON CONFLICT (field_key) DO NOTHING;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM mapping_fields WHERE field_key IN ('season', 'location', 'holiday');
                UPDATE mapping_fields SET field_key = 'product' WHERE field_key = 'product_id';
                UPDATE mapping_fields SET field_key = 'discount' WHERE field_key = 'promotion';
            ");
        }
    }
}
