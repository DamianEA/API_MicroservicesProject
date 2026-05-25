using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Drive.Migrations
{
    public partial class AddAuditoriaColumnsToExistingTable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "created_at",
                table: "users");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "users");
        }
    }
}