using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CdrBilling.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "billing_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    total_records = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    processed_records = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_billing_sessions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "subscribers",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    phone_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    client_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_subscribers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tariff_entries",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    prefix = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    destination = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    rate_per_min = table.Column<decimal>(type: "numeric(10,6)", precision: 10, scale: 6, nullable: false),
                    connection_fee = table.Column<decimal>(type: "numeric(10,6)", precision: 10, scale: 6, nullable: false),
                    timeband_start = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    timeband_end = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    weekday_mask = table.Column<byte>(type: "smallint", nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    effective_date = table.Column<DateOnly>(type: "date", nullable: false),
                    expiry_date = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tariff_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "call_records",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    start_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    end_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    calling_party = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    called_party = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    direction = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    disposition = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    duration_sec = table.Column<int>(type: "integer", nullable: false),
                    billable_sec = table.Column<int>(type: "integer", nullable: false),
                    original_charge = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: true),
                    account_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    call_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    trunk_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    computed_charge = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: true),
                    applied_tariff_id = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_call_records", x => x.id);
                    table.ForeignKey(
                        name: "fk_call_records_tariff_entries_applied_tariff_id",
                        column: x => x.applied_tariff_id,
                        principalTable: "tariff_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_call_records_applied_tariff_id",
                table: "call_records",
                column: "applied_tariff_id");

            migrationBuilder.CreateIndex(
                name: "ix_cr_called",
                table: "call_records",
                columns: new[] { "session_id", "called_party" });

            migrationBuilder.CreateIndex(
                name: "ix_cr_calling",
                table: "call_records",
                columns: new[] { "session_id", "calling_party" });

            migrationBuilder.CreateIndex(
                name: "ix_cr_session",
                table: "call_records",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "ix_sub_phone",
                table: "subscribers",
                columns: new[] { "session_id", "phone_number" });

            migrationBuilder.CreateIndex(
                name: "ix_sub_session",
                table: "subscribers",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "ix_te_prefix",
                table: "tariff_entries",
                columns: new[] { "session_id", "prefix" });

            migrationBuilder.CreateIndex(
                name: "ix_te_session",
                table: "tariff_entries",
                column: "session_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "billing_sessions");

            migrationBuilder.DropTable(
                name: "call_records");

            migrationBuilder.DropTable(
                name: "subscribers");

            migrationBuilder.DropTable(
                name: "tariff_entries");
        }
    }
}
