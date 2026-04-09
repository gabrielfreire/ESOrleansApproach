using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ESOrleansApproach.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class updateentities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CorrelationId",
                table: "DomainEvents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestPath",
                table: "DomainEvents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "DomainEvents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TraceId",
                table: "DomainEvents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserAgent",
                table: "DomainEvents",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CorrelationId",
                table: "DomainEvents");

            migrationBuilder.DropColumn(
                name: "RequestPath",
                table: "DomainEvents");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "DomainEvents");

            migrationBuilder.DropColumn(
                name: "TraceId",
                table: "DomainEvents");

            migrationBuilder.DropColumn(
                name: "UserAgent",
                table: "DomainEvents");
        }
    }
}
