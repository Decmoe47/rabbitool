using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rabbitool.Repository.Migrations
{
    public partial class Update_20220906_144100 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "LastVideoOrLiveTime",
                table: "YoutubeSubscribe",
                newName: "LastVideoPubTime");

            migrationBuilder.RenameColumn(
                name: "LastVideoOrLiveId",
                table: "YoutubeSubscribe",
                newName: "LastVideoId");

            migrationBuilder.AlterColumn<string>(
                name: "AllArchiveVideoIds",
                table: "YoutubeSubscribe",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AllUpcomingLiveRoomIds",
                table: "YoutubeSubscribe",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LastLiveRoomId",
                table: "YoutubeSubscribe",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastLiveStartTime",
                table: "YoutubeSubscribe",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllUpcomingLiveRoomIds",
                table: "YoutubeSubscribe");

            migrationBuilder.DropColumn(
                name: "LastLiveRoomId",
                table: "YoutubeSubscribe");

            migrationBuilder.DropColumn(
                name: "LastLiveStartTime",
                table: "YoutubeSubscribe");

            migrationBuilder.RenameColumn(
                name: "LastVideoPubTime",
                table: "YoutubeSubscribe",
                newName: "LastVideoOrLiveTime");

            migrationBuilder.RenameColumn(
                name: "LastVideoId",
                table: "YoutubeSubscribe",
                newName: "LastVideoOrLiveId");

            migrationBuilder.AlterColumn<string>(
                name: "AllArchiveVideoIds",
                table: "YoutubeSubscribe",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");
        }
    }
}
