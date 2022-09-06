using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rabbitool.Repository.Migrations
{
    public partial class AddYoutubeConfigs : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "LivePush",
                table: "YoutubeSubscribeConfig",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "UpcomingLivePush",
                table: "YoutubeSubscribeConfig",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "VideoPush",
                table: "YoutubeSubscribeConfig",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LivePush",
                table: "YoutubeSubscribeConfig");

            migrationBuilder.DropColumn(
                name: "UpcomingLivePush",
                table: "YoutubeSubscribeConfig");

            migrationBuilder.DropColumn(
                name: "VideoPush",
                table: "YoutubeSubscribeConfig");
        }
    }
}
