using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrbitBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddClipViewAndChannelFollow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChannelFollows",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ChannelId = table.Column<int>(type: "int", nullable: false),
                    FollowedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChannelFollows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChannelFollows_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChannelFollows_Channels_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "Channels",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ClipViews",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClipId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    SessionId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ViewedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClipViews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClipViews_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ClipViews_Clips_ClipId",
                        column: x => x.ClipId,
                        principalTable: "Clips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChannelFollows_ChannelId",
                table: "ChannelFollows",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_ChannelFollows_UserId_ChannelId",
                table: "ChannelFollows",
                columns: new[] { "UserId", "ChannelId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClipViews_ClipId_SessionId",
                table: "ClipViews",
                columns: new[] { "ClipId", "SessionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClipViews_ClipId_UserId",
                table: "ClipViews",
                columns: new[] { "ClipId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_ClipViews_UserId",
                table: "ClipViews",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChannelFollows");

            migrationBuilder.DropTable(
                name: "ClipViews");
        }
    }
}
