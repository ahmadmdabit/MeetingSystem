using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeetingSystem.Context.Migrations
{
    /// <inheritdoc />
    public partial class AddMeetingLogAndTrigger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE TRIGGER trg_Meetings_AfterDelete
                ON dbo.Meetings
                AFTER DELETE
                AS
                BEGIN
                  SET NOCOUNT ON;
                  INSERT INTO dbo.LogMeetings (OriginalId, RowJson)
                  SELECT 
                    d.Id,
                    (SELECT d.* FOR JSON PATH, WITHOUT_ARRAY_WRAPPER)
                  FROM deleted d;
                END;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS dbo.trg_Meetings_AfterDelete;");
        }
    }
}
