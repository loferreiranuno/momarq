using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VisualSearch.Api.Migrations
{
    /// <inheritdoc />
    public partial class FixPauseResumeAndProductImport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "paused_at",
                table: "crawl_jobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "paused_by_admin_user_id",
                table: "crawl_jobs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "imported_product_id",
                table: "crawl_extracted_products",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "reviewed_at",
                table: "crawl_extracted_products",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "reviewed_by_admin_user_id",
                table: "crawl_extracted_products",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "status",
                table: "crawl_extracted_products",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_crawl_jobs_paused_by_admin_user_id",
                table: "crawl_jobs",
                column: "paused_by_admin_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_crawl_extracted_products_imported_product_id",
                table: "crawl_extracted_products",
                column: "imported_product_id");

            migrationBuilder.CreateIndex(
                name: "IX_crawl_extracted_products_reviewed_by_admin_user_id",
                table: "crawl_extracted_products",
                column: "reviewed_by_admin_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_crawl_extracted_products_status",
                table: "crawl_extracted_products",
                column: "status");

            migrationBuilder.AddForeignKey(
                name: "FK_crawl_extracted_products_admin_users_reviewed_by_admin_user~",
                table: "crawl_extracted_products",
                column: "reviewed_by_admin_user_id",
                principalTable: "admin_users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_crawl_extracted_products_products_imported_product_id",
                table: "crawl_extracted_products",
                column: "imported_product_id",
                principalTable: "products",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_crawl_jobs_admin_users_paused_by_admin_user_id",
                table: "crawl_jobs",
                column: "paused_by_admin_user_id",
                principalTable: "admin_users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_crawl_extracted_products_admin_users_reviewed_by_admin_user~",
                table: "crawl_extracted_products");

            migrationBuilder.DropForeignKey(
                name: "FK_crawl_extracted_products_products_imported_product_id",
                table: "crawl_extracted_products");

            migrationBuilder.DropForeignKey(
                name: "FK_crawl_jobs_admin_users_paused_by_admin_user_id",
                table: "crawl_jobs");

            migrationBuilder.DropIndex(
                name: "IX_crawl_jobs_paused_by_admin_user_id",
                table: "crawl_jobs");

            migrationBuilder.DropIndex(
                name: "IX_crawl_extracted_products_imported_product_id",
                table: "crawl_extracted_products");

            migrationBuilder.DropIndex(
                name: "IX_crawl_extracted_products_reviewed_by_admin_user_id",
                table: "crawl_extracted_products");

            migrationBuilder.DropIndex(
                name: "IX_crawl_extracted_products_status",
                table: "crawl_extracted_products");

            migrationBuilder.DropColumn(
                name: "paused_at",
                table: "crawl_jobs");

            migrationBuilder.DropColumn(
                name: "paused_by_admin_user_id",
                table: "crawl_jobs");

            migrationBuilder.DropColumn(
                name: "imported_product_id",
                table: "crawl_extracted_products");

            migrationBuilder.DropColumn(
                name: "reviewed_at",
                table: "crawl_extracted_products");

            migrationBuilder.DropColumn(
                name: "reviewed_by_admin_user_id",
                table: "crawl_extracted_products");

            migrationBuilder.DropColumn(
                name: "status",
                table: "crawl_extracted_products");
        }
    }
}
