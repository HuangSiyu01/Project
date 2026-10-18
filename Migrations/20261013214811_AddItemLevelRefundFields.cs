using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Project.Migrations
{
    public partial class AddItemLevelRefundFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OrderItemId",
                table: "RefundRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RefundQuantity",
                table: "RefundRequests",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefundRequests_OrderItemId",
                table: "RefundRequests",
                column: "OrderItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_RefundRequests_OrderItems_OrderItemId",
                table: "RefundRequests",
                column: "OrderItemId",
                principalTable: "OrderItems",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RefundRequests_OrderItems_OrderItemId",
                table: "RefundRequests");

            migrationBuilder.DropIndex(
                name: "IX_RefundRequests_OrderItemId",
                table: "RefundRequests");

            migrationBuilder.DropColumn(
                name: "OrderItemId",
                table: "RefundRequests");

            migrationBuilder.DropColumn(
                name: "RefundQuantity",
                table: "RefundRequests");
        }
    }
}
