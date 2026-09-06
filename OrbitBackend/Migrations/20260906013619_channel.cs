using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrbitBackend.Migrations
{
    /// <inheritdoc />
    public partial class channel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LiveStreams_AspNetUsers_StreamerId",
                table: "LiveStreams");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_StreamKey",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "StreamKey",
                table: "AspNetUsers");

            migrationBuilder.AddColumn<int>(
                name: "ChannelId",
                table: "LiveStreams",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "DisconnectedAt",
                table: "LiveStreams",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedByUserId",
                table: "ChatMessages",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ChatMessages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Channels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChannelName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    StreamKey = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    OwnerId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Channels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Channels_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChannelModerators",
                columns: table => new
                {
                    ChannelId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    HiredAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChannelModerators", x => new { x.ChannelId, x.UserId });
                    table.ForeignKey(
                        name: "FK_ChannelModerators_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ChannelModerators_Channels_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "Channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChatBans",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChannelId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ModeratorId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    BannedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatBans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatBans_AspNetUsers_ModeratorId",
                        column: x => x.ModeratorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ChatBans_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ChatBans_Channels_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "Channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChatTimeouts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChannelId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ModeratorId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IssuedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatTimeouts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatTimeouts_AspNetUsers_ModeratorId",
                        column: x => x.ModeratorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ChatTimeouts_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ChatTimeouts_Channels_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "Channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Clean up orphaned data: delete ChatMessages for LiveStreams that have no valid Channel,
            // then delete those LiveStreams. Required because existing rows have ChannelId=0.
            migrationBuilder.Sql(
                "DELETE FROM [ChatMessages] WHERE [LiveStreamId] IN (SELECT [Id] FROM [LiveStreams] WHERE [ChannelId] = 0 OR [ChannelId] NOT IN (SELECT [Id] FROM [Channels]));");
            migrationBuilder.Sql(
                "DELETE FROM [LiveStreams] WHERE [ChannelId] = 0 OR [ChannelId] NOT IN (SELECT [Id] FROM [Channels]);");

            migrationBuilder.CreateIndex(
                name: "IX_LiveStreams_ChannelId",
                table: "LiveStreams",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_ChannelModerators_UserId",
                table: "ChannelModerators",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Channels_ChannelName",
                table: "Channels",
                column: "ChannelName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Channels_OwnerId",
                table: "Channels",
                column: "OwnerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Channels_StreamKey",
                table: "Channels",
                column: "StreamKey",
                unique: true,
                filter: "[StreamKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ChatBans_ChannelId_UserId_IsActive",
                table: "ChatBans",
                columns: new[] { "ChannelId", "UserId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatBans_ModeratorId",
                table: "ChatBans",
                column: "ModeratorId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatBans_UserId",
                table: "ChatBans",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatTimeouts_ChannelId_UserId_ExpiresAt",
                table: "ChatTimeouts",
                columns: new[] { "ChannelId", "UserId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatTimeouts_ModeratorId",
                table: "ChatTimeouts",
                column: "ModeratorId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatTimeouts_UserId",
                table: "ChatTimeouts",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_LiveStreams_AspNetUsers_StreamerId",
                table: "LiveStreams",
                column: "StreamerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_LiveStreams_Channels_ChannelId",
                table: "LiveStreams",
                column: "ChannelId",
                principalTable: "Channels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LiveStreams_AspNetUsers_StreamerId",
                table: "LiveStreams");

            migrationBuilder.DropForeignKey(
                name: "FK_LiveStreams_Channels_ChannelId",
                table: "LiveStreams");

            migrationBuilder.DropTable(
                name: "ChannelModerators");

            migrationBuilder.DropTable(
                name: "ChatBans");

            migrationBuilder.DropTable(
                name: "ChatTimeouts");

            migrationBuilder.DropTable(
                name: "Channels");

            migrationBuilder.DropIndex(
                name: "IX_LiveStreams_ChannelId",
                table: "LiveStreams");

            migrationBuilder.DropColumn(
                name: "ChannelId",
                table: "LiveStreams");

            migrationBuilder.DropColumn(
                name: "DisconnectedAt",
                table: "LiveStreams");

            migrationBuilder.DropColumn(
                name: "DeletedByUserId",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ChatMessages");

            migrationBuilder.AddColumn<string>(
                name: "StreamKey",
                table: "AspNetUsers",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_StreamKey",
                table: "AspNetUsers",
                column: "StreamKey",
                unique: true,
                filter: "[StreamKey] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_LiveStreams_AspNetUsers_StreamerId",
                table: "LiveStreams",
                column: "StreamerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
