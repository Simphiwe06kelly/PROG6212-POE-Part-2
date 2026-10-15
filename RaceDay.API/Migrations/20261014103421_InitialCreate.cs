using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RaceDay.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    UserID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FullName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.UserID);
                    table.CheckConstraint("CK_Users_Role", "[Role] IN ('Organiser','Participant')");
                });

            migrationBuilder.CreateTable(
                name: "Venues",
                columns: table => new
                {
                    VenueID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VenueName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Province = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Latitude = table.Column<decimal>(type: "decimal(9,6)", nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(9,6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Venues", x => x.VenueID);
                });

            migrationBuilder.CreateTable(
                name: "Events",
                columns: table => new
                {
                    EventID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganiserID = table.Column<int>(type: "int", nullable: false),
                    VenueID = table.Column<int>(type: "int", nullable: false),
                    EventName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    EventDate = table.Column<DateTime>(type: "date", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Events", x => x.EventID);
                    table.CheckConstraint("CK_Events_Type", "[EventType] IN ('Run','Walk','Cycle')");
                    table.ForeignKey(
                        name: "FK_Events_Users_OrganiserID",
                        column: x => x.OrganiserID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Events_Venues_VenueID",
                        column: x => x.VenueID,
                        principalTable: "Venues",
                        principalColumn: "VenueID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    CategoryID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventID = table.Column<int>(type: "int", nullable: false),
                    CategoryName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DistanceKm = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    EntryFee = table.Column<decimal>(type: "decimal(8,2)", nullable: false),
                    MaxParticipants = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.CategoryID);
                    table.ForeignKey(
                        name: "FK_Categories_Events_EventID",
                        column: x => x.EventID,
                        principalTable: "Events",
                        principalColumn: "EventID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Enrolments",
                columns: table => new
                {
                    EnrolmentID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParticipantID = table.Column<int>(type: "int", nullable: false),
                    CategoryID = table.Column<int>(type: "int", nullable: false),
                    EnrolmentDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Enrolments", x => x.EnrolmentID);
                    table.CheckConstraint("CK_Enrolments_Status", "[Status] IN ('Confirmed','Cancelled')");
                    table.ForeignKey(
                        name: "FK_Enrolments_Categories_CategoryID",
                        column: x => x.CategoryID,
                        principalTable: "Categories",
                        principalColumn: "CategoryID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Enrolments_Users_ParticipantID",
                        column: x => x.ParticipantID,
                        principalTable: "Users",
                        principalColumn: "UserID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Results",
                columns: table => new
                {
                    ResultID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EnrolmentID = table.Column<int>(type: "int", nullable: false),
                    FinishTime = table.Column<TimeSpan>(type: "time", nullable: true),
                    OverallPosition = table.Column<int>(type: "int", nullable: true),
                    CategoryPosition = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Results", x => x.ResultID);
                    table.CheckConstraint("CK_Results_Status", "[Status] IN ('Finished','DNF','DQ')");
                    table.ForeignKey(
                        name: "FK_Results_Enrolments_EnrolmentID",
                        column: x => x.EnrolmentID,
                        principalTable: "Enrolments",
                        principalColumn: "EnrolmentID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "UserID", "CreatedAt", "Email", "FullName", "PasswordHash", "Role" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "thandiwe.mokoena@raceday.co.za", "Thandiwe Mokoena", "HASHED_PASSWORD_1", "Organiser" },
                    { 2, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "johan.vdm@raceday.co.za", "Johan van der Merwe", "HASHED_PASSWORD_2", "Organiser" },
                    { 3, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "lindiwe.dlamini@gmail.com", "Lindiwe Dlamini", "HASHED_PASSWORD_3", "Participant" },
                    { 4, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "craig.adams@gmail.com", "Craig Adams", "HASHED_PASSWORD_4", "Participant" }
                });

            migrationBuilder.InsertData(
                table: "Venues",
                columns: new[] { "VenueID", "Address", "City", "Latitude", "Longitude", "Province", "VenueName" },
                values: new object[,]
                {
                    { 1, "Fritz Sonnenberg Rd", "Cape Town", -33.9047m, 18.4058m, "Western Cape", "Green Point Park" },
                    { 2, "106 Judith Rd", "Johannesburg", -26.1615m, 28.0201m, "Gauteng", "Marks Park Sports Club" },
                    { 3, "Peter Brown Dr", "Pietermaritzburg", -29.6006m, 30.3794m, "KwaZulu-Natal", "Comrades Marathon Route" }
                });

            migrationBuilder.InsertData(
                table: "Events",
                columns: new[] { "EventID", "CreatedAt", "Description", "EventDate", "EventName", "EventType", "OrganiserID", "VenueID" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "A community cycling event around the Cape Peninsula.", new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Unspecified), "Cape Town Cycle Tour Fun Ride", "Cycle", 1, 1 },
                    { 2, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "A timed 5km/10km park run series in Johannesburg.", new DateTime(2026, 10, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), "Johannesburg Park Run Challenge", "Run", 2, 2 },
                    { 3, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "A community walk along part of the Comrades route.", new DateTime(2026, 9, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), "Comrades Legacy Charity Walk", "Walk", 1, 3 }
                });

            migrationBuilder.InsertData(
                table: "Categories",
                columns: new[] { "CategoryID", "CategoryName", "DistanceKm", "EntryFee", "EventID", "MaxParticipants" },
                values: new object[,]
                {
                    { 1, "35km Short Route", 35m, 350m, 1, 500 },
                    { 2, "109km Full Route", 109m, 650m, 1, 1000 },
                    { 3, "5km Fun Run", 5m, 80m, 2, 300 },
                    { 4, "10km Timed Run", 10m, 120m, 2, 300 },
                    { 5, "10km Charity Walk", 10m, 100m, 3, 200 }
                });

            migrationBuilder.InsertData(
                table: "Enrolments",
                columns: new[] { "EnrolmentID", "CategoryID", "EnrolmentDate", "ParticipantID", "Status" },
                values: new object[,]
                {
                    { 1, 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 3, "Confirmed" },
                    { 2, 3, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 3, "Confirmed" },
                    { 3, 4, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 4, "Confirmed" },
                    { 4, 5, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 4, "Confirmed" }
                });

            migrationBuilder.InsertData(
                table: "Results",
                columns: new[] { "ResultID", "CategoryPosition", "EnrolmentID", "FinishTime", "OverallPosition", "Status" },
                values: new object[,]
                {
                    { 1, 4, 2, new TimeSpan(0, 0, 22, 14, 0), 15, "Finished" },
                    { 2, 10, 3, new TimeSpan(0, 0, 48, 52, 0), 32, "Finished" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_EventID",
                table: "Categories",
                column: "EventID");

            migrationBuilder.CreateIndex(
                name: "IX_Enrolments_CategoryID",
                table: "Enrolments",
                column: "CategoryID");

            migrationBuilder.CreateIndex(
                name: "IX_Enrolments_ParticipantID_CategoryID",
                table: "Enrolments",
                columns: new[] { "ParticipantID", "CategoryID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Events_OrganiserID",
                table: "Events",
                column: "OrganiserID");

            migrationBuilder.CreateIndex(
                name: "IX_Events_VenueID",
                table: "Events",
                column: "VenueID");

            migrationBuilder.CreateIndex(
                name: "IX_Results_EnrolmentID",
                table: "Results",
                column: "EnrolmentID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Results");

            migrationBuilder.DropTable(
                name: "Enrolments");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "Events");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Venues");
        }
    }
}
