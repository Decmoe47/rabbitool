using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rabbitool.Repository.Migrations
{
    public partial class Init_20220910_143300 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BilibiliSubscribe",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Uid = table.Column<uint>(type: "INTEGER", nullable: false),
                    Uname = table.Column<string>(type: "TEXT", nullable: false),
                    LastDynamicTime = table.Column<string>(type: "TEXT", nullable: false),
                    LastDynamicType = table.Column<int>(type: "INTEGER", nullable: false),
                    LastLiveStatus = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BilibiliSubscribe", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MailSubscribe",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Username = table.Column<string>(type: "TEXT", nullable: false),
                    Address = table.Column<string>(type: "TEXT", nullable: false),
                    Password = table.Column<string>(type: "TEXT", nullable: false),
                    Mailbox = table.Column<string>(type: "TEXT", nullable: false),
                    Host = table.Column<string>(type: "TEXT", nullable: false),
                    Port = table.Column<int>(type: "INTEGER", nullable: false),
                    Ssl = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastMailTime = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MailSubscribe", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QQChannelSubscribe",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GuildId = table.Column<string>(type: "TEXT", nullable: false),
                    GuildName = table.Column<string>(type: "TEXT", nullable: false),
                    ChannelId = table.Column<string>(type: "TEXT", nullable: false),
                    ChannelName = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QQChannelSubscribe", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TwitterSubscribe",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    ScreenName = table.Column<string>(type: "TEXT", nullable: false),
                    LastTweetId = table.Column<string>(type: "TEXT", nullable: false),
                    LastTweetTime = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TwitterSubscribe", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "YoutubeSubscribe",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    ChannelId = table.Column<string>(type: "TEXT", nullable: false),
                    LastVideoId = table.Column<string>(type: "TEXT", nullable: false),
                    LastVideoPubTime = table.Column<string>(type: "TEXT", nullable: false),
                    LastLiveRoomId = table.Column<string>(type: "TEXT", nullable: false),
                    LastLiveStartTime = table.Column<string>(type: "TEXT", nullable: false),
                    AllUpcomingLiveRoomIds = table.Column<string>(type: "TEXT", nullable: false),
                    AllArchiveVideoIds = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YoutubeSubscribe", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BilibiliSubscribe_QQChannelSubscribes",
                columns: table => new
                {
                    BilibiliSubscribesId = table.Column<Guid>(type: "TEXT", nullable: false),
                    QQChannelsId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BilibiliSubscribe_QQChannelSubscribes", x => new { x.BilibiliSubscribesId, x.QQChannelsId });
                    table.ForeignKey(
                        name: "FK_BilibiliSubscribe_QQChannelSubscribes_BilibiliSubscribe_BilibiliSubscribesId",
                        column: x => x.BilibiliSubscribesId,
                        principalTable: "BilibiliSubscribe",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BilibiliSubscribe_QQChannelSubscribes_QQChannelSubscribe_QQChannelsId",
                        column: x => x.QQChannelsId,
                        principalTable: "QQChannelSubscribe",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BilibiliSubscribeConfig",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    LivePush = table.Column<bool>(type: "INTEGER", nullable: false),
                    DynamicPush = table.Column<bool>(type: "INTEGER", nullable: false),
                    PureForwardDynamicPush = table.Column<bool>(type: "INTEGER", nullable: false),
                    LiveEndingPush = table.Column<bool>(type: "INTEGER", nullable: false),
                    QQChannelId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SubscribeId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BilibiliSubscribeConfig", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BilibiliSubscribeConfig_BilibiliSubscribe_SubscribeId",
                        column: x => x.SubscribeId,
                        principalTable: "BilibiliSubscribe",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BilibiliSubscribeConfig_QQChannelSubscribe_QQChannelId",
                        column: x => x.QQChannelId,
                        principalTable: "QQChannelSubscribe",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MailSubscribe_QQChannelSubscribes",
                columns: table => new
                {
                    MailSubscribesId = table.Column<Guid>(type: "TEXT", nullable: false),
                    QQChannelsId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MailSubscribe_QQChannelSubscribes", x => new { x.MailSubscribesId, x.QQChannelsId });
                    table.ForeignKey(
                        name: "FK_MailSubscribe_QQChannelSubscribes_MailSubscribe_MailSubscribesId",
                        column: x => x.MailSubscribesId,
                        principalTable: "MailSubscribe",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MailSubscribe_QQChannelSubscribes_QQChannelSubscribe_QQChannelsId",
                        column: x => x.QQChannelsId,
                        principalTable: "QQChannelSubscribe",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MailSubscribeConfig",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Detail = table.Column<bool>(type: "INTEGER", nullable: false),
                    PushToThread = table.Column<bool>(type: "INTEGER", nullable: false),
                    QQChannelId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SubscribeId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MailSubscribeConfig", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MailSubscribeConfig_MailSubscribe_SubscribeId",
                        column: x => x.SubscribeId,
                        principalTable: "MailSubscribe",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MailSubscribeConfig_QQChannelSubscribe_QQChannelId",
                        column: x => x.QQChannelId,
                        principalTable: "QQChannelSubscribe",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TwitterSubscribe_QQChannelSubscribes",
                columns: table => new
                {
                    QQChannelsId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TwitterSubscribesId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TwitterSubscribe_QQChannelSubscribes", x => new { x.QQChannelsId, x.TwitterSubscribesId });
                    table.ForeignKey(
                        name: "FK_TwitterSubscribe_QQChannelSubscribes_QQChannelSubscribe_QQChannelsId",
                        column: x => x.QQChannelsId,
                        principalTable: "QQChannelSubscribe",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TwitterSubscribe_QQChannelSubscribes_TwitterSubscribe_TwitterSubscribesId",
                        column: x => x.TwitterSubscribesId,
                        principalTable: "TwitterSubscribe",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TwitterSubscribeConfig",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    QuotePush = table.Column<bool>(type: "INTEGER", nullable: false),
                    RtPush = table.Column<bool>(type: "INTEGER", nullable: false),
                    PushToThread = table.Column<bool>(type: "INTEGER", nullable: false),
                    QQChannelId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SubscribeId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TwitterSubscribeConfig", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TwitterSubscribeConfig_QQChannelSubscribe_QQChannelId",
                        column: x => x.QQChannelId,
                        principalTable: "QQChannelSubscribe",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TwitterSubscribeConfig_TwitterSubscribe_SubscribeId",
                        column: x => x.SubscribeId,
                        principalTable: "TwitterSubscribe",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "YoutubeSubscribe_QQChannelSubscribes",
                columns: table => new
                {
                    QQChannelsId = table.Column<Guid>(type: "TEXT", nullable: false),
                    YoutubeSubscribesId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YoutubeSubscribe_QQChannelSubscribes", x => new { x.QQChannelsId, x.YoutubeSubscribesId });
                    table.ForeignKey(
                        name: "FK_YoutubeSubscribe_QQChannelSubscribes_QQChannelSubscribe_QQChannelsId",
                        column: x => x.QQChannelsId,
                        principalTable: "QQChannelSubscribe",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_YoutubeSubscribe_QQChannelSubscribes_YoutubeSubscribe_YoutubeSubscribesId",
                        column: x => x.YoutubeSubscribesId,
                        principalTable: "YoutubeSubscribe",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "YoutubeSubscribeConfig",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    VideoPush = table.Column<bool>(type: "INTEGER", nullable: false),
                    LivePush = table.Column<bool>(type: "INTEGER", nullable: false),
                    UpcomingLivePush = table.Column<bool>(type: "INTEGER", nullable: false),
                    ArchivePush = table.Column<bool>(type: "INTEGER", nullable: false),
                    QQChannelId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SubscribeId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YoutubeSubscribeConfig", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YoutubeSubscribeConfig_QQChannelSubscribe_QQChannelId",
                        column: x => x.QQChannelId,
                        principalTable: "QQChannelSubscribe",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_YoutubeSubscribeConfig_YoutubeSubscribe_SubscribeId",
                        column: x => x.SubscribeId,
                        principalTable: "YoutubeSubscribe",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BilibiliSubscribe_QQChannelSubscribes_QQChannelsId",
                table: "BilibiliSubscribe_QQChannelSubscribes",
                column: "QQChannelsId");

            migrationBuilder.CreateIndex(
                name: "IX_BilibiliSubscribeConfig_QQChannelId",
                table: "BilibiliSubscribeConfig",
                column: "QQChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_BilibiliSubscribeConfig_SubscribeId",
                table: "BilibiliSubscribeConfig",
                column: "SubscribeId");

            migrationBuilder.CreateIndex(
                name: "IX_MailSubscribe_QQChannelSubscribes_QQChannelsId",
                table: "MailSubscribe_QQChannelSubscribes",
                column: "QQChannelsId");

            migrationBuilder.CreateIndex(
                name: "IX_MailSubscribeConfig_QQChannelId",
                table: "MailSubscribeConfig",
                column: "QQChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_MailSubscribeConfig_SubscribeId",
                table: "MailSubscribeConfig",
                column: "SubscribeId");

            migrationBuilder.CreateIndex(
                name: "IX_TwitterSubscribe_QQChannelSubscribes_TwitterSubscribesId",
                table: "TwitterSubscribe_QQChannelSubscribes",
                column: "TwitterSubscribesId");

            migrationBuilder.CreateIndex(
                name: "IX_TwitterSubscribeConfig_QQChannelId",
                table: "TwitterSubscribeConfig",
                column: "QQChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_TwitterSubscribeConfig_SubscribeId",
                table: "TwitterSubscribeConfig",
                column: "SubscribeId");

            migrationBuilder.CreateIndex(
                name: "IX_YoutubeSubscribe_QQChannelSubscribes_YoutubeSubscribesId",
                table: "YoutubeSubscribe_QQChannelSubscribes",
                column: "YoutubeSubscribesId");

            migrationBuilder.CreateIndex(
                name: "IX_YoutubeSubscribeConfig_QQChannelId",
                table: "YoutubeSubscribeConfig",
                column: "QQChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_YoutubeSubscribeConfig_SubscribeId",
                table: "YoutubeSubscribeConfig",
                column: "SubscribeId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BilibiliSubscribe_QQChannelSubscribes");

            migrationBuilder.DropTable(
                name: "BilibiliSubscribeConfig");

            migrationBuilder.DropTable(
                name: "MailSubscribe_QQChannelSubscribes");

            migrationBuilder.DropTable(
                name: "MailSubscribeConfig");

            migrationBuilder.DropTable(
                name: "TwitterSubscribe_QQChannelSubscribes");

            migrationBuilder.DropTable(
                name: "TwitterSubscribeConfig");

            migrationBuilder.DropTable(
                name: "YoutubeSubscribe_QQChannelSubscribes");

            migrationBuilder.DropTable(
                name: "YoutubeSubscribeConfig");

            migrationBuilder.DropTable(
                name: "BilibiliSubscribe");

            migrationBuilder.DropTable(
                name: "MailSubscribe");

            migrationBuilder.DropTable(
                name: "TwitterSubscribe");

            migrationBuilder.DropTable(
                name: "QQChannelSubscribe");

            migrationBuilder.DropTable(
                name: "YoutubeSubscribe");
        }
    }
}
