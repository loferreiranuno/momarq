using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace VisualSearch.Api.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Enable pgvector extension
        migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS vector;");

        migrationBuilder.CreateTable(
            name: "providers",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                logo_url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                website_url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_providers", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "products",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                provider_id = table.Column<int>(type: "integer", nullable: false),
                external_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                price = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false, defaultValue: "EUR"),
                category = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                product_url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_products", x => x.id);
                table.ForeignKey(
                    name: "FK_products_providers_provider_id",
                    column: x => x.provider_id,
                    principalTable: "providers",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "product_images",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                product_id = table.Column<int>(type: "integer", nullable: false),
                image_url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                embedding = table.Column<string>(type: "vector(768)", nullable: true),
                is_primary = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_product_images", x => x.id);
                table.ForeignKey(
                    name: "FK_product_images_products_product_id",
                    column: x => x.product_id,
                    principalTable: "products",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        // Create indexes
        migrationBuilder.CreateIndex(
            name: "IX_providers_name",
            table: "providers",
            column: "name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_products_provider_id",
            table: "products",
            column: "provider_id");

        migrationBuilder.CreateIndex(
            name: "IX_products_category",
            table: "products",
            column: "category");

        migrationBuilder.CreateIndex(
            name: "IX_products_provider_id_external_id",
            table: "products",
            columns: new[] { "provider_id", "external_id" },
            unique: true,
            filter: "external_id IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_product_images_product_id",
            table: "product_images",
            column: "product_id");

        migrationBuilder.CreateIndex(
            name: "IX_product_images_is_primary",
            table: "product_images",
            column: "is_primary");

        // Create HNSW index for vector similarity search
        // This is the key index for high-performance similarity search
        // m=16: number of bi-directional links, higher = better recall, more memory
        // ef_construction=200: size of dynamic candidate list during construction
        migrationBuilder.Sql(@"
            CREATE INDEX hnsw_embedding_idx 
            ON product_images 
            USING hnsw (embedding vector_cosine_ops) 
            WITH (m = 16, ef_construction = 200);
        ");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "product_images");

        migrationBuilder.DropTable(
            name: "products");

        migrationBuilder.DropTable(
            name: "providers");

        migrationBuilder.Sql("DROP EXTENSION IF EXISTS vector;");
    }
}
