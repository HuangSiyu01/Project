using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Project.Migrations
{
    public partial class FixAttractionSeedData : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AttractionImages",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "AttractionImages",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "AttractionImages",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "AttractionImages",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "AttractionImages",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "AttractionImages",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "AttractionImages",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "Attractions",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.UpdateData(
                table: "AttractionImages",
                keyColumn: "Id",
                keyValue: 1,
                column: "ImageUrl",
                value: "/images/attractions/forbidden-city-1.jpg.jpg");

            migrationBuilder.UpdateData(
                table: "AttractionImages",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "AttractionId", "ImageUrl" },
                values: new object[] { 2, "/images/attractions/great-wall-1.jpg" });

            migrationBuilder.UpdateData(
                table: "Attractions",
                keyColumn: "Id",
                keyValue: 1,
                column: "ImageUrl",
                value: "/images/attractions/forbidden-city.jpg.jpg");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "AttractionImages",
                keyColumn: "Id",
                keyValue: 1,
                column: "ImageUrl",
                value: "/images/attractions/forbidden-city-1.jpg");

            migrationBuilder.UpdateData(
                table: "AttractionImages",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "AttractionId", "ImageUrl" },
                values: new object[] { 1, "/images/attractions/forbidden-city-2.jpg" });

            migrationBuilder.InsertData(
                table: "AttractionImages",
                columns: new[] { "Id", "AttractionId", "ImageUrl" },
                values: new object[,]
                {
                    { 3, 1, "/images/attractions/forbidden-city-3.jpg" },
                    { 4, 2, "/images/attractions/great-wall-1.jpg" },
                    { 5, 2, "/images/attractions/great-wall-2.jpg" },
                    { 6, 3, "/images/attractions/terracotta-army-1.jpg" },
                    { 7, 3, "/images/attractions/terracotta-army-2.jpg" }
                });

            migrationBuilder.UpdateData(
                table: "Attractions",
                keyColumn: "Id",
                keyValue: 1,
                column: "ImageUrl",
                value: "/images/attractions/forbidden-city.jpg");

            migrationBuilder.InsertData(
                table: "Attractions",
                columns: new[] { "Id", "City", "Description", "ImageUrl", "IsAvailable", "Name", "TicketPrice" },
                values: new object[] { 4, "Huangshan, China", "Experience dramatic granite peaks, ancient pine trees and famous mountain scenery.", "/images/attractions/huangshan.jpg", true, "Mount Huangshan", 190.00m });

            migrationBuilder.InsertData(
                table: "AttractionImages",
                columns: new[] { "Id", "AttractionId", "ImageUrl" },
                values: new object[] { 8, 4, "/images/attractions/huangshan-1.jpg" });

            migrationBuilder.InsertData(
                table: "AttractionImages",
                columns: new[] { "Id", "AttractionId", "ImageUrl" },
                values: new object[] { 9, 4, "/images/attractions/huangshan-2.jpg" });
        }
    }
}
