using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Project.Migrations
{
    public partial class AddAttractionImages : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AttractionImages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AttractionId = table.Column<int>(type: "int", nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttractionImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttractionImages_Attractions_AttractionId",
                        column: x => x.AttractionId,
                        principalTable: "Attractions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Attractions",
                columns: new[] { "Id", "City", "Description", "ImageUrl", "IsAvailable", "Name", "TicketPrice" },
                values: new object[,]
                {
                    { 1, "Beijing, China", "Explore the former imperial palace of China, with historic halls, courtyards and royal collections.", "/images/attractions/forbidden-city.jpg", true, "The Forbidden City", 60.00m },
                    { 2, "Beijing, China", "Visit one of the best-known sections of the Great Wall of China and enjoy its mountain views.", "/images/attractions/great-wall.jpg", true, "Badaling Great Wall", 40.00m },
                    { 3, "Xi'an, China", "Discover the world-famous Terracotta Army and the archaeological heritage of China's first emperor.", "/images/attractions/terracotta-army.jpg", true, "Emperor Qinshihuang's Mausoleum Site Museum", 120.00m },
                    { 4, "Huangshan, China", "Experience dramatic granite peaks, ancient pine trees and famous mountain scenery.", "/images/attractions/huangshan.jpg", true, "Mount Huangshan", 190.00m }
                });

            migrationBuilder.InsertData(
                table: "AttractionImages",
                columns: new[] { "Id", "AttractionId", "ImageUrl" },
                values: new object[,]
                {
                    { 1, 1, "/images/attractions/forbidden-city-1.jpg" },
                    { 2, 1, "/images/attractions/forbidden-city-2.jpg" },
                    { 3, 2, "/images/attractions/great-wall-1.jpg" },
                    { 4, 2, "/images/attractions/great-wall-2.jpg" },
                    { 5, 3, "/images/attractions/terracotta-army.jpg" },
                    { 6, 4, "/images/attractions/huangshan.jpg" },
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttractionImages_AttractionId",
                table: "AttractionImages",
                column: "AttractionId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttractionImages");

            migrationBuilder.DeleteData(
                table: "Attractions",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Attractions",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Attractions",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Attractions",
                keyColumn: "Id",
                keyValue: 4);
        }
    }
}
