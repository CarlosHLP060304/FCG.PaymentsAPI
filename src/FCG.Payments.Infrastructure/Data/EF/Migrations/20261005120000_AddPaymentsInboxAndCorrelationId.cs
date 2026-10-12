using FCG.Payments.Infrastructure.Data.EF.Context;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FCG.Payments.Infrastructure.Data.EF.Migrations;

[DbContext(typeof(PaymentsDbContext))]
[Migration("20261005120000_AddPaymentsInboxAndCorrelationId")]
public partial class AddPaymentsInboxAndCorrelationId : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "CorrelationId",
            table: "Payments",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "InboxMessages",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                EventId = table.Column<Guid>(type: "uuid", nullable: false),
                ConsumerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_InboxMessages", message => message.Id);
            });

        migrationBuilder.CreateIndex(
            name: "UX_InboxMessages_ConsumerName_EventId",
            table: "InboxMessages",
            columns: new[] { "ConsumerName", "EventId" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "InboxMessages");
        migrationBuilder.DropColumn(name: "CorrelationId", table: "Payments");
    }
}
