using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChaleAltoDoMorroAPI.Data.Migrations
{
    /// <inheritdoc />
    public partial class GaleriaEVideosPorChave : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "poster_url",
                table: "tour_videos");

            migrationBuilder.DropColumn(
                name: "src",
                table: "gallery_images");

            migrationBuilder.DropColumn(
                name: "src_set",
                table: "gallery_images");

            migrationBuilder.AlterColumn<string>(
                name: "url",
                table: "tour_videos",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AddColumn<string>(
                name: "file_key",
                table: "tour_videos",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "poster_key",
                table: "tour_videos",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "file_key",
                table: "gallery_images",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tour_videos_url_xor_file",
                table: "tour_videos",
                sql: "(url IS NULL) <> (file_key IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_tour_videos_url_xor_file",
                table: "tour_videos");

            migrationBuilder.DropColumn(
                name: "file_key",
                table: "tour_videos");

            migrationBuilder.DropColumn(
                name: "poster_key",
                table: "tour_videos");

            migrationBuilder.DropColumn(
                name: "file_key",
                table: "gallery_images");

            migrationBuilder.AlterColumn<string>(
                name: "url",
                table: "tour_videos",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "poster_url",
                table: "tour_videos",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "src",
                table: "gallery_images",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "src_set",
                table: "gallery_images",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);
        }
    }
}
