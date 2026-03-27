using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CdrBilling.Infrastructure.Migrations
{
    public partial class AddCallRecordSessionDispositionIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_cr_session_disposition_called",
                table: "call_records",
                columns: new[] { "session_id", "disposition", "called_party" });

            migrationBuilder.CreateIndex(
                name: "ix_cr_session_disposition_calling",
                table: "call_records",
                columns: new[] { "session_id", "disposition", "calling_party" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_cr_session_disposition_called",
                table: "call_records");

            migrationBuilder.DropIndex(
                name: "ix_cr_session_disposition_calling",
                table: "call_records");
        }
    }
}
