using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartAutoFill.Migrations
{
    /// <inheritdoc />
    public partial class AddMappingExamples : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MappingExamples",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocCategory = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TextSnippet = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FieldKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Embedding = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MappingExamples", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MappingExamples_DocCategory",
                table: "MappingExamples",
                column: "DocCategory");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MappingExamples");
        }
    }
}
