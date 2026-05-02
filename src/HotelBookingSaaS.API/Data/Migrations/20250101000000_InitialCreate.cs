using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HotelBookingSaaS.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Hotels",
                columns: table => new
                {
                    HotelId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HotelName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Country = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: "Jordan"),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TotalRooms = table.Column<int>(type: "int", nullable: false),
                    PmsType = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    LogoUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ChannexPropertyId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false,
                        defaultValueSql: "GETUTCDATE()")
                },
                constraints: table => table.PrimaryKey("PK_Hotels", x => x.HotelId));

            migrationBuilder.CreateTable(
                name: "Rooms",
                columns: table => new
                {
                    RoomId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HotelId = table.Column<int>(type: "int", nullable: false),
                    RoomNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RoomType = table.Column<int>(type: "int", nullable: false),
                    RoomTypeName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Capacity = table.Column<int>(type: "int", nullable: false, defaultValue: 2),
                    TotalCount = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    BasePrice = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false, defaultValue: "JOD"),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    ExternalRoomId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ChannexRoomTypeId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rooms", x => x.RoomId);
                    table.ForeignKey("FK_Rooms_Hotels_HotelId", x => x.HotelId, "Hotels", "HotelId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Bookings",
                columns: table => new
                {
                    BookingId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HotelId = table.Column<int>(type: "int", nullable: false),
                    OTABookingId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PmsConfirmationNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OTAType = table.Column<int>(type: "int", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    GuestName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    GuestEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    GuestPhone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    GuestNationality = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CheckInDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CheckOutDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Adults = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    Children = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    RoomType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RoomNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TotalPrice = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false, defaultValue: "JOD"),
                    RatePlan = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BookingStatus = table.Column<int>(type: "int", nullable: false),
                    SpecialRequests = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CancelledDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BookingDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bookings", x => x.BookingId);
                    table.ForeignKey("FK_Bookings_Hotels_HotelId", x => x.HotelId, "Hotels", "HotelId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OTACredentials",
                columns: table => new
                {
                    CredentialId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HotelId = table.Column<int>(type: "int", nullable: false),
                    OTAType = table.Column<int>(type: "int", nullable: false),
                    PropertyId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UsernameEncrypted = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PasswordEncrypted = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ApiKeyEncrypted = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    OAuthTokenEncrypted = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    RefreshTokenEncrypted = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    TokenExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ChannexChannelId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsViaChannex = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    ConnectedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastTestedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastTestResult = table.Column<bool>(type: "bit", nullable: true),
                    LastTestError = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OTACredentials", x => x.CredentialId);
                    table.ForeignKey("FK_OTACredentials_Hotels_HotelId", x => x.HotelId, "Hotels", "HotelId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PMSCredentials",
                columns: table => new
                {
                    CredentialId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HotelId = table.Column<int>(type: "int", nullable: false),
                    PmsType = table.Column<int>(type: "int", nullable: false),
                    ApiUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ApiKeyEncrypted = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ClientIdEncrypted = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ClientSecretEncrypted = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    UsernameEncrypted = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PasswordEncrypted = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AccessTokenEncrypted = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    RefreshTokenEncrypted = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    TokenExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AdditionalConfigJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    ConnectedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastTestedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastTestResult = table.Column<bool>(type: "bit", nullable: true),
                    LastTestError = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PMSCredentials", x => x.CredentialId);
                    table.ForeignKey("FK_PMSCredentials_Hotels_HotelId", x => x.HotelId, "Hotels", "HotelId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SyncLogs",
                columns: table => new
                {
                    LogId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HotelId = table.Column<int>(type: "int", nullable: false),
                    OTAType = table.Column<int>(type: "int", nullable: true),
                    PmsType = table.Column<int>(type: "int", nullable: true),
                    SyncStartTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SyncEndTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RecordsProcessed = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    RecordsCreated = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    RecordsUpdated = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    RecordsFailed = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DetailsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    HangfireJobId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncLogs", x => x.LogId);
                    table.ForeignKey("FK_SyncLogs_Hotels_HotelId", x => x.HotelId, "Hotels", "HotelId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HotelId = table.Column<int>(type: "int", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    RefreshToken = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RefreshTokenExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastLoginDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.UserId);
                    table.ForeignKey("FK_Users_Hotels_HotelId", x => x.HotelId, "Hotels", "HotelId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    NotificationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HotelId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    DataJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    IsRead = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    RelatedBookingId = table.Column<int>(type: "int", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReadDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.NotificationId);
                    table.ForeignKey("FK_Notifications_Hotels_HotelId", x => x.HotelId, "Hotels", "HotelId",
                        onDelete: ReferentialAction.Cascade);
                });

            // ─── Indexes ──────────────────────────────────────────────────────
            migrationBuilder.CreateIndex("IX_Hotels_HotelName", "Hotels", "HotelName");
            migrationBuilder.CreateIndex("IX_Hotels_IsActive", "Hotels", "IsActive");
            migrationBuilder.CreateIndex("IX_Rooms_HotelId_RoomNumber", "Rooms",
                new[] { "HotelId", "RoomNumber" }, unique: true);
            migrationBuilder.CreateIndex("IX_Bookings_HotelId", "Bookings", "HotelId");
            migrationBuilder.CreateIndex("IX_Bookings_CheckInDate", "Bookings", "CheckInDate");
            migrationBuilder.CreateIndex("IX_Bookings_BookingStatus", "Bookings", "BookingStatus");
            migrationBuilder.CreateIndex("IX_Bookings_OTABookingId", "Bookings",
                new[] { "HotelId", "OTAType", "OTABookingId" },
                unique: true, filter: "[OTABookingId] IS NOT NULL");
            migrationBuilder.CreateIndex("IX_OTACredentials_HotelId_OTAType", "OTACredentials",
                new[] { "HotelId", "OTAType" }, unique: true);
            migrationBuilder.CreateIndex("IX_PMSCredentials_HotelId_PmsType", "PMSCredentials",
                new[] { "HotelId", "PmsType" }, unique: true);
            migrationBuilder.CreateIndex("IX_Users_Email", "Users", "Email", unique: true);
            migrationBuilder.CreateIndex("IX_Notifications_HotelId_IsRead", "Notifications",
                new[] { "HotelId", "IsRead" });

            // ─── Seed SuperAdmin ──────────────────────────────────────────────
            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "UserId", "HotelId", "Email", "PasswordHash", "FirstName", "LastName",
                    "Role", "IsActive", "CreatedDate", "UpdatedDate" },
                values: new object[]
                {
                    1, null, "admin@hotelsaas.jo",
                    BCrypt.Net.BCrypt.HashPassword("Admin@123"),
                    "Super", "Admin", 1, true,
                    new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable("Notifications");
            migrationBuilder.DropTable("Users");
            migrationBuilder.DropTable("SyncLogs");
            migrationBuilder.DropTable("OTACredentials");
            migrationBuilder.DropTable("PMSCredentials");
            migrationBuilder.DropTable("Bookings");
            migrationBuilder.DropTable("Rooms");
            migrationBuilder.DropTable("Hotels");
        }
    }
}
