using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase3SalesReportingIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SalesOrderItems_SalesOrderId",
                table: "SalesOrderItems");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_StoreId_CreatedAtUtc",
                table: "SalesOrders",
                columns: new[] { "StoreId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrderItems_ProductId",
                table: "SalesOrderItems",
                column: "ProductId")
                .Annotation("SqlServer:Include", new[] { "SalesOrderId", "Quantity", "UnitPrice", "TaxAmount", "LineDiscount" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrderItems_SalesOrderId",
                table: "SalesOrderItems",
                column: "SalesOrderId")
                .Annotation("SqlServer:Include", new[] { "ProductId", "Quantity", "UnitPrice", "TaxAmount", "LineDiscount" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SalesOrders_StoreId_CreatedAtUtc",
                table: "SalesOrders");

            migrationBuilder.DropIndex(
                name: "IX_SalesOrderItems_ProductId",
                table: "SalesOrderItems");

            migrationBuilder.DropIndex(
                name: "IX_SalesOrderItems_SalesOrderId",
                table: "SalesOrderItems");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrderItems_SalesOrderId",
                table: "SalesOrderItems",
                column: "SalesOrderId");
        }
    }
}
