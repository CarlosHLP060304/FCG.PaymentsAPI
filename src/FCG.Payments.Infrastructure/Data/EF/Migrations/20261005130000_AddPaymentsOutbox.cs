using FCG.Payments.Infrastructure.Data.EF.Context;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FCG.Payments.Infrastructure.Data.EF.Migrations;

public partial class AddPaymentsOutbox : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "OutboxMessages",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                EventId = table.Column<Guid>(type: "uuid", nullable: false),
                PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                EventType = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                Payload = table.Column<string>(type: "text", nullable: false),
                OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                Attempts = table.Column<int>(type: "integer", nullable: false),
                NextAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                LeaseId = table.Column<Guid>(type: "uuid", nullable: true),
                LeaseExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OutboxMessages", message => message.Id);
                table.CheckConstraint("CK_OutboxMessages_Attempts_NonNegative", "\"Attempts\" >= 0");
            });

        migrationBuilder.CreateIndex(
            name: "UX_OutboxMessages_EventId",
            table: "OutboxMessages",
            column: "EventId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "UX_OutboxMessages_PaymentId",
            table: "OutboxMessages",
            column: "PaymentId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_OutboxMessages_Pending",
            table: "OutboxMessages",
            columns: new[] { "PublishedAt", "NextAttemptAt", "LeaseExpiresAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "OutboxMessages");
    }
}
