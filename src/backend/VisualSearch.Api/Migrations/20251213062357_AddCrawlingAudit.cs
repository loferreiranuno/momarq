using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace VisualSearch.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCrawlingAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "crawl_jobs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    provider_id = table.Column<int>(type: "integer", nullable: false),
                    requested_by_admin_user_id = table.Column<int>(type: "integer", nullable: true),
                    start_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    sitemap_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    max_pages = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    lease_owner = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    lease_expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    canceled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_crawl_jobs", x => x.id);
                    table.ForeignKey(
                        name: "FK_crawl_jobs_admin_users_requested_by_admin_user_id",
                        column: x => x.requested_by_admin_user_id,
                        principalTable: "admin_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_crawl_jobs_providers_provider_id",
                        column: x => x.provider_id,
                        principalTable: "providers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "crawl_pages",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    crawl_job_id = table.Column<long>(type: "bigint", nullable: false),
                    url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    http_status_code = table.Column<int>(type: "integer", nullable: true),
                    content_type = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    title = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    content_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    content = table.Column<string>(type: "text", nullable: true),
                    error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    fetched_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_crawl_pages", x => x.id);
                    table.ForeignKey(
                        name: "FK_crawl_pages_crawl_jobs_crawl_job_id",
                        column: x => x.crawl_job_id,
                        principalTable: "crawl_jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "crawl_extracted_products",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    crawl_job_id = table.Column<long>(type: "bigint", nullable: false),
                    crawl_page_id = table.Column<long>(type: "bigint", nullable: false),
                    provider_id = table.Column<int>(type: "integer", nullable: false),
                    external_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    price = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    product_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    image_urls_json = table.Column<string>(type: "text", nullable: true),
                    raw_json = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_crawl_extracted_products", x => x.id);
                    table.ForeignKey(
                        name: "FK_crawl_extracted_products_crawl_jobs_crawl_job_id",
                        column: x => x.crawl_job_id,
                        principalTable: "crawl_jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_crawl_extracted_products_crawl_pages_crawl_page_id",
                        column: x => x.crawl_page_id,
                        principalTable: "crawl_pages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_crawl_extracted_products_providers_provider_id",
                        column: x => x.provider_id,
                        principalTable: "providers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_crawl_extracted_products_crawl_job_id",
                table: "crawl_extracted_products",
                column: "crawl_job_id");

            migrationBuilder.CreateIndex(
                name: "IX_crawl_extracted_products_crawl_page_id",
                table: "crawl_extracted_products",
                column: "crawl_page_id");

            migrationBuilder.CreateIndex(
                name: "IX_crawl_extracted_products_created_at",
                table: "crawl_extracted_products",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_crawl_extracted_products_provider_id",
                table: "crawl_extracted_products",
                column: "provider_id");

            migrationBuilder.CreateIndex(
                name: "IX_crawl_extracted_products_provider_id_external_id",
                table: "crawl_extracted_products",
                columns: new[] { "provider_id", "external_id" },
                filter: "external_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_crawl_jobs_created_at",
                table: "crawl_jobs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_crawl_jobs_provider_id",
                table: "crawl_jobs",
                column: "provider_id");

            migrationBuilder.CreateIndex(
                name: "IX_crawl_jobs_requested_by_admin_user_id",
                table: "crawl_jobs",
                column: "requested_by_admin_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_crawl_jobs_status",
                table: "crawl_jobs",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_crawl_jobs_status_lease_expires_at",
                table: "crawl_jobs",
                columns: new[] { "status", "lease_expires_at" });

            migrationBuilder.CreateIndex(
                name: "IX_crawl_pages_crawl_job_id",
                table: "crawl_pages",
                column: "crawl_job_id");

            migrationBuilder.CreateIndex(
                name: "IX_crawl_pages_crawl_job_id_url",
                table: "crawl_pages",
                columns: new[] { "crawl_job_id", "url" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_crawl_pages_created_at",
                table: "crawl_pages",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_crawl_pages_status",
                table: "crawl_pages",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "crawl_extracted_products");

            migrationBuilder.DropTable(
                name: "crawl_pages");

            migrationBuilder.DropTable(
                name: "crawl_jobs");
        }
    }
}
