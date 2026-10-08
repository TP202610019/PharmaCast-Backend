using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PharmacyApiService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionIdMappingField : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Optional field_key used by the ML service to detect accidental duplicate
            // rows (e.g. the same file re-uploaded, or an export emitting the same sale
            // line twice) — never used as a model feature, dropped right after dedup.
            // Optional on purpose: not every pharmacy's POS export has a per-line id.
            migrationBuilder.Sql(@"
                INSERT INTO mapping_fields (id, field_key, display_name, field_type, is_required, is_active, created_at)
                VALUES
                    ('a1b2c3d4-0001-0001-0001-000000000013', 'transaction_id', 'N° de Comprobante / Folio', 'sales', false, true, TIMESTAMPTZ '2026-01-01T00:00:00Z')
                ON CONFLICT (field_key) DO NOTHING;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM mapping_fields WHERE field_key = 'transaction_id';
            ");
        }
    }
}
