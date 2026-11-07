using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace NxtHotspot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HotspotRadiusPortal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DisconnectCommands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    NasId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedByAdminUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    Result = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DisconnectCommands", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InternetPackages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SiteId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DownloadKbps = table.Column<int>(type: "integer", nullable: false),
                    UploadKbps = table.Column<int>(type: "integer", nullable: false),
                    DataLimitMb = table.Column<long>(type: "bigint", nullable: true),
                    ValidityMinutes = table.Column<int>(type: "integer", nullable: true),
                    SessionTimeoutMinutes = table.Column<int>(type: "integer", nullable: true),
                    IdleTimeoutMinutes = table.Column<int>(type: "integer", nullable: true),
                    MaxDevices = table.Column<int>(type: "integer", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InternetPackages", x => x.Id);
                    table.CheckConstraint("CK_InternetPackages_Limits", "\"DownloadKbps\" >= 0 AND \"UploadKbps\" >= 0 AND \"MaxDevices\" >= 1 AND \"Price\" >= 0");
                    table.ForeignKey(
                        name: "FK_InternetPackages_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InternetPackages_Sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "Sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NasDevices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    Vendor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RouterOsVersion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    SharedSecretEncrypted = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    AuthPort = table.Column<int>(type: "integer", nullable: false),
                    AcctPort = table.Column<int>(type: "integer", nullable: false),
                    CoaPort = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    PortalKeyHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NasDevices", x => x.Id);
                    table.CheckConstraint("CK_NasDevices_Ports", "\"AuthPort\" BETWEEN 1 AND 65535 AND \"AcctPort\" BETWEEN 1 AND 65535 AND \"CoaPort\" BETWEEN 1 AND 65535");
                    table.ForeignKey(
                        name: "FK_NasDevices_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NasDevices_Sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "Sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OtpChallenges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    Purpose = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MobileNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CodeHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    ConsumedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    MacAddress = table.Column<string>(type: "character varying(17)", maxLength: 17, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OtpChallenges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RadiusAccountingEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NasId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcctSessionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StatusType = table.Column<int>(type: "integer", nullable: false),
                    SessionTime = table.Column<long>(type: "bigint", nullable: false),
                    DelayTime = table.Column<long>(type: "bigint", nullable: false),
                    InputBytes = table.Column<long>(type: "bigint", nullable: false),
                    OutputBytes = table.Column<long>(type: "bigint", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RadiusAccountingEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RadiusRequestLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    SiteId = table.Column<Guid>(type: "uuid", nullable: true),
                    NasId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceIp = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    PacketKind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Result = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    MacAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RadiusRequestLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RadiusServerStatuses",
                columns: table => new
                {
                    InstanceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    HostName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AuthPort = table.Column<int>(type: "integer", nullable: false),
                    AcctPort = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastHeartbeatAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AuthRequests = table.Column<long>(type: "bigint", nullable: false),
                    AuthAccepts = table.Column<long>(type: "bigint", nullable: false),
                    AuthRejects = table.Column<long>(type: "bigint", nullable: false),
                    AcctRequests = table.Column<long>(type: "bigint", nullable: false),
                    Dropped = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RadiusServerStatuses", x => x.InstanceId);
                });

            migrationBuilder.CreateTable(
                name: "SmsLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    SiteId = table.Column<Guid>(type: "uuid", nullable: true),
                    MobileMasked = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ProviderName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Success = table.Column<bool>(type: "boolean", nullable: false),
                    Error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmsLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SubscriberGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    GroupType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DefaultPackageId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriberGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubscriberGroups_InternetPackages_DefaultPackageId",
                        column: x => x.DefaultPackageId,
                        principalTable: "InternetPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SubscriberGroups_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Subscribers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SiteId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubscriberType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Identifier = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NormalizedIdentifier = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FullName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    MobileNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    PasswordHash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    PackageId = table.Column<Guid>(type: "uuid", nullable: true),
                    RoomNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CheckInAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CheckOutAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ValidUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MaxDevices = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FirstLoginAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastLoginAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subscribers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Subscribers_InternetPackages_PackageId",
                        column: x => x.PackageId,
                        principalTable: "InternetPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Subscribers_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Subscribers_Sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "Sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Subscribers_SubscriberGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "SubscriberGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "RadiusCredentials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubscriberId = table.Column<Guid>(type: "uuid", nullable: false),
                    NasId = table.Column<Guid>(type: "uuid", nullable: true),
                    Username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    MacAddress = table.Column<string>(type: "character varying(17)", maxLength: 17, nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RadiusCredentials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RadiusCredentials_Subscribers_SubscriberId",
                        column: x => x.SubscriberId,
                        principalTable: "Subscribers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RadiusSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    NasId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubscriberId = table.Column<Guid>(type: "uuid", nullable: true),
                    Username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AcctSessionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FramedIpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    CallingStationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CalledStationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    NasPortId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUpdateAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StoppedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SessionTimeSeconds = table.Column<long>(type: "bigint", nullable: false),
                    InputBytes = table.Column<long>(type: "bigint", nullable: false),
                    OutputBytes = table.Column<long>(type: "bigint", nullable: false),
                    TerminateCause = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RadiusSessions", x => x.Id);
                    table.CheckConstraint("CK_RadiusSessions_Counters", "\"InputBytes\" >= 0 AND \"OutputBytes\" >= 0 AND \"SessionTimeSeconds\" >= 0");
                    table.ForeignKey(
                        name: "FK_RadiusSessions_NasDevices_NasId",
                        column: x => x.NasId,
                        principalTable: "NasDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RadiusSessions_Subscribers_SubscriberId",
                        column: x => x.SubscriberId,
                        principalTable: "Subscribers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DisconnectCommands_SessionId",
                table: "DisconnectCommands",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_DisconnectCommands_Status_CreatedAt",
                table: "DisconnectCommands",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InternetPackages_OrganizationId_Name",
                table: "InternetPackages",
                columns: new[] { "OrganizationId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InternetPackages_SiteId",
                table: "InternetPackages",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_NasDevices_IpAddress",
                table: "NasDevices",
                column: "IpAddress",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NasDevices_OrganizationId_SiteId",
                table: "NasDevices",
                columns: new[] { "OrganizationId", "SiteId" });

            migrationBuilder.CreateIndex(
                name: "IX_NasDevices_PortalKeyHash",
                table: "NasDevices",
                column: "PortalKeyHash",
                unique: true,
                filter: "\"PortalKeyHash\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_NasDevices_SiteId",
                table: "NasDevices",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_OtpChallenges_ExpiresAt",
                table: "OtpChallenges",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_OtpChallenges_SiteId_MobileNumber_CreatedAt",
                table: "OtpChallenges",
                columns: new[] { "SiteId", "MobileNumber", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RadiusAccountingEvents_NasId_AcctSessionId_StatusType_Sessi~",
                table: "RadiusAccountingEvents",
                columns: new[] { "NasId", "AcctSessionId", "StatusType", "SessionTime", "DelayTime" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RadiusAccountingEvents_ReceivedAt",
                table: "RadiusAccountingEvents",
                column: "ReceivedAt");

            migrationBuilder.CreateIndex(
                name: "IX_RadiusCredentials_ExpiresAt",
                table: "RadiusCredentials",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_RadiusCredentials_SubscriberId",
                table: "RadiusCredentials",
                column: "SubscriberId");

            migrationBuilder.CreateIndex(
                name: "IX_RadiusCredentials_Username",
                table: "RadiusCredentials",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RadiusRequestLogs_CreatedAt",
                table: "RadiusRequestLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_RadiusRequestLogs_OrganizationId_CreatedAt",
                table: "RadiusRequestLogs",
                columns: new[] { "OrganizationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RadiusSessions_NasId_AcctSessionId",
                table: "RadiusSessions",
                columns: new[] { "NasId", "AcctSessionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RadiusSessions_OrganizationId_StartedAt",
                table: "RadiusSessions",
                columns: new[] { "OrganizationId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RadiusSessions_OrganizationId_Status",
                table: "RadiusSessions",
                columns: new[] { "OrganizationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RadiusSessions_SiteId_StartedAt",
                table: "RadiusSessions",
                columns: new[] { "SiteId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RadiusSessions_SubscriberId",
                table: "RadiusSessions",
                column: "SubscriberId");

            migrationBuilder.CreateIndex(
                name: "IX_SmsLogs_OrganizationId_CreatedAt",
                table: "SmsLogs",
                columns: new[] { "OrganizationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriberGroups_DefaultPackageId",
                table: "SubscriberGroups",
                column: "DefaultPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriberGroups_OrganizationId_GroupType_Name",
                table: "SubscriberGroups",
                columns: new[] { "OrganizationId", "GroupType", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subscribers_GroupId",
                table: "Subscribers",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Subscribers_OrganizationId_MobileNumber",
                table: "Subscribers",
                columns: new[] { "OrganizationId", "MobileNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_Subscribers_OrganizationId_NormalizedIdentifier",
                table: "Subscribers",
                columns: new[] { "OrganizationId", "NormalizedIdentifier" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subscribers_OrganizationId_SubscriberType_Status",
                table: "Subscribers",
                columns: new[] { "OrganizationId", "SubscriberType", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Subscribers_PackageId",
                table: "Subscribers",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "IX_Subscribers_SiteId_RoomNumber",
                table: "Subscribers",
                columns: new[] { "SiteId", "RoomNumber" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DisconnectCommands");

            migrationBuilder.DropTable(
                name: "OtpChallenges");

            migrationBuilder.DropTable(
                name: "RadiusAccountingEvents");

            migrationBuilder.DropTable(
                name: "RadiusCredentials");

            migrationBuilder.DropTable(
                name: "RadiusRequestLogs");

            migrationBuilder.DropTable(
                name: "RadiusServerStatuses");

            migrationBuilder.DropTable(
                name: "RadiusSessions");

            migrationBuilder.DropTable(
                name: "SmsLogs");

            migrationBuilder.DropTable(
                name: "NasDevices");

            migrationBuilder.DropTable(
                name: "Subscribers");

            migrationBuilder.DropTable(
                name: "SubscriberGroups");

            migrationBuilder.DropTable(
                name: "InternetPackages");
        }
    }
}
