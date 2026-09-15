using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingLeads.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLeadAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LeadAnalyses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeadId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedServices = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExtractedClientType = table.Column<int>(type: "int", nullable: true),
                    ExtractedApproximateAnnualRevenue = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ExtractedAccountingSoftware = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Urgency = table.Column<int>(type: "int", nullable: true),
                    ExtractedFilingDeadline = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeadAnalyses", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeadAnalyses_LeadId",
                table: "LeadAnalyses",
                column: "LeadId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeadAnalyses");
        }
    }
}
