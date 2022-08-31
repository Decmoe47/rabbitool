using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rabbitool.Repository.Migrations
{
    public partial class Update : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Detail",
                table: "MailSubscribeConfig",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Detail",
                table: "MailSubscribeConfig");
        }
    }
}
