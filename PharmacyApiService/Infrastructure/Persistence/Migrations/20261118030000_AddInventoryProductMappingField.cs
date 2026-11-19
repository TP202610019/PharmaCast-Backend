using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace PharmacyApiService.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261118030000_AddInventoryProductMappingField")]
public class AddInventoryProductMappingField : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            INSERT INTO mapping_fields (id, field_key, display_name, field_type, is_required, is_active, created_at)
            VALUES ('c65fe833-5c89-44c2-ab0d-520b0343a406', 'inv_product', 'Producto de inventario', 'inventory', TRUE, TRUE, NOW())
            ON CONFLICT (field_key) DO NOTHING;
            UPDATE mapping_fields SET is_active = TRUE, is_required = TRUE
            WHERE field_key = 'inv_product';
            UPDATE mapping_fields SET is_required = TRUE WHERE field_key = 'stock';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE mapping_fields SET is_active = FALSE
            WHERE id = 'c65fe833-5c89-44c2-ab0d-520b0343a406';
            UPDATE mapping_fields SET is_required = FALSE WHERE field_key = 'stock';
            """);
    }
}
