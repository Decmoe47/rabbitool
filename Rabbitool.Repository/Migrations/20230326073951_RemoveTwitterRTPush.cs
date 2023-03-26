using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rabbitool.Repository.Migrations
{
    /// <inheritdoc />
    public partial class RemoveTwitterRTPush : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RtPush",
                table: "TwitterSubscribeConfig");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RtPush",
                table: "TwitterSubscribeConfig",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }
    }
}
