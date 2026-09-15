using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingLeads.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLeadQualification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BookkeepingMonthsBehind",
                table: "LeadAnalyses",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EstimatedDeadlineInDays",
                table: "LeadAnalyses",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LeadQualificationResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeadId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Reasons = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RulesVersionApplied = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeadQualificationResults", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeadQualificationResults_LeadId",
                table: "LeadQualificationResults",
                column: "LeadId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeadQualificationResults");

            migrationBuilder.DropColumn(
                name: "BookkeepingMonthsBehind",
                table: "LeadAnalyses");

            migrationBuilder.DropColumn(
                name: "EstimatedDeadlineInDays",
                table: "LeadAnalyses");
        }
    }
}
