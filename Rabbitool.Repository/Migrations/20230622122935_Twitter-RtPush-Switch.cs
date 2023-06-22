using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rabbitool.Repository.Migrations
{
    /// <inheritdoc />
    public partial class TwitterRtPushSwitch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "QuotePush",
                table: "TwitterSubscribeConfig",
                newName: "RtPush");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "RtPush",
                table: "TwitterSubscribeConfig",
                newName: "QuotePush");
        }
    }
}
