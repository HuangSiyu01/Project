using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Project.Migrations
{
    public partial class RenameLongReviewToReviewOptional : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LongReview",
                table: "Posts");

            migrationBuilder.AddColumn<string>(
                name: "Review",
                table: "Posts",
                type: "nvarchar(max)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Review",
                table: "Posts");

            migrationBuilder.AddColumn<string>(
                name: "LongReview",
                table: "Posts",
                type: "nvarchar(max)",
                maxLength: 5000,
                nullable: false,
                defaultValue: "");
        }
    }
}
