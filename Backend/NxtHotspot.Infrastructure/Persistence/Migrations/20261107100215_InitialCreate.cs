using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace NxtHotspot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    AdminUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Details = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationTypes",
                columns: table => new
                {
                    OrganizationTypeId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TypeName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationTypes", x => x.OrganizationTypeId);
                });

            migrationBuilder.CreateTable(
                name: "Permissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    IsPlatformOnly = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MonthlyPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MaxSites = table.Column<int>(type: "integer", nullable: true),
                    MaxNasDevices = table.Column<int>(type: "integer", nullable: true),
                    MaxUsers = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationTypeDefaultLoginMethods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationTypeId = table.Column<int>(type: "integer", nullable: false),
                    AuthenticationType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    RequireOTP = table.Column<bool>(type: "boolean", nullable: false),
                    RequireBookingValidation = table.Column<bool>(type: "boolean", nullable: false),
                    AllowMultipleDevices = table.Column<bool>(type: "boolean", nullable: false),
                    MaxDevices = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationTypeDefaultLoginMethods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganizationTypeDefaultLoginMethods_OrganizationTypes_Organ~",
                        column: x => x.OrganizationTypeId,
                        principalTable: "OrganizationTypes",
                        principalColumn: "OrganizationTypeId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Organizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OrganizationTypeId = table.Column<int>(type: "integer", nullable: false),
                    SubscriptionPlanId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContactPerson = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    MobileNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organizations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Organizations_OrganizationTypes_OrganizationTypeId",
                        column: x => x.OrganizationTypeId,
                        principalTable: "OrganizationTypes",
                        principalColumn: "OrganizationTypeId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Organizations_SubscriptionPlans_SubscriptionPlanId",
                        column: x => x.SubscriptionPlanId,
                        principalTable: "SubscriptionPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AdminUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Username = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    NormalizedUsername = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FullName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    MobileNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    HasAllSitesAccess = table.Column<bool>(type: "boolean", nullable: false),
                    FailedLoginCount = table.Column<int>(type: "integer", nullable: false),
                    LockoutEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastLoginAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdminUsers_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationBranding",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    LogoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    WelcomeMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ThemeColor = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    BackgroundUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TermsAndConditions = table.Column<string>(type: "text", nullable: true),
                    PrivacyPolicy = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationBranding", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganizationBranding_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OtpExpirySeconds = table.Column<int>(type: "integer", nullable: false),
                    OtpResendCooldownSeconds = table.Column<int>(type: "integer", nullable: false),
                    OtpMaxAttempts = table.Column<int>(type: "integer", nullable: false),
                    SessionValiditySeconds = table.Column<int>(type: "integer", nullable: false),
                    DefaultMaxDevices = table.Column<int>(type: "integer", nullable: false),
                    DefaultDataLimitMb = table.Column<int>(type: "integer", nullable: true),
                    TimeZoneId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganizationSettings_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    IsPlatformRole = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Roles_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sites_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AdminUserRoles",
                columns: table => new
                {
                    AdminUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminUserRoles", x => new { x.AdminUserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AdminUserRoles_AdminUsers_AdminUserId",
                        column: x => x.AdminUserId,
                        principalTable: "AdminUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AdminUserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    PermissionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => new { x.RoleId, x.PermissionId });
                    table.ForeignKey(
                        name: "FK_RolePermissions_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AdminUserSites",
                columns: table => new
                {
                    AdminUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SiteId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminUserSites", x => new { x.AdminUserId, x.SiteId });
                    table.ForeignKey(
                        name: "FK_AdminUserSites_AdminUsers_AdminUserId",
                        column: x => x.AdminUserId,
                        principalTable: "AdminUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AdminUserSites_Sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "Sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationLoginMethods",
                columns: table => new
                {
                    LoginMethodId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SiteId = table.Column<Guid>(type: "uuid", nullable: true),
                    AuthenticationType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    RequireOTP = table.Column<bool>(type: "boolean", nullable: false),
                    RequireBookingValidation = table.Column<bool>(type: "boolean", nullable: false),
                    AllowMultipleDevices = table.Column<bool>(type: "boolean", nullable: false),
                    MaxDevices = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationLoginMethods", x => x.LoginMethodId);
                    table.CheckConstraint("CK_OrganizationLoginMethods_MaxDevices", "\"MaxDevices\" >= 1");
                    table.ForeignKey(
                        name: "FK_OrganizationLoginMethods_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrganizationLoginMethods_Sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "Sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationSmsSettings",
                columns: table => new
                {
                    SmsSettingId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    SiteId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProviderName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ApiEndpoint = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ApiKeyEncrypted = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    SenderId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MessageTemplate = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationSmsSettings", x => x.SmsSettingId);
                    table.CheckConstraint("CK_OrganizationSmsSettings_SiteNeedsOrg", "\"SiteId\" IS NULL OR \"OrganizationId\" IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_OrganizationSmsSettings_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrganizationSmsSettings_Sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "Sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SiteWallets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    Balance = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteWallets", x => x.Id);
                    table.CheckConstraint("CK_SiteWallets_Balance_NonNegative", "\"Balance\" >= 0");
                    table.ForeignKey(
                        name: "FK_SiteWallets_Sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "Sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SiteWalletTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    SiteWalletId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedByAdminUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiteWalletTransactions", x => x.Id);
                    table.CheckConstraint("CK_SiteWalletTransactions_Amount_Positive", "\"Amount\" > 0");
                    table.CheckConstraint("CK_SiteWalletTransactions_BalanceAfter_NonNegative", "\"BalanceAfter\" >= 0");
                    table.ForeignKey(
                        name: "FK_SiteWalletTransactions_SiteWallets_SiteWalletId",
                        column: x => x.SiteWalletId,
                        principalTable: "SiteWallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminUserRoles_RoleId",
                table: "AdminUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_AdminUsers_NormalizedUsername",
                table: "AdminUsers",
                column: "NormalizedUsername",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdminUsers_OrganizationId",
                table: "AdminUsers",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_AdminUserSites_SiteId",
                table: "AdminUserSites",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_AdminUserId",
                table: "AuditLogs",
                column: "AdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_OrganizationId_CreatedAt",
                table: "AuditLogs",
                columns: new[] { "OrganizationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationBranding_OrganizationId",
                table: "OrganizationBranding",
                column: "OrganizationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_LoginMethods_Org",
                table: "OrganizationLoginMethods",
                columns: new[] { "OrganizationId", "AuthenticationType" },
                unique: true,
                filter: "\"SiteId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_LoginMethods_Site",
                table: "OrganizationLoginMethods",
                columns: new[] { "SiteId", "AuthenticationType" },
                unique: true,
                filter: "\"SiteId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_Name",
                table: "Organizations",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_OrganizationTypeId",
                table: "Organizations",
                column: "OrganizationTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_Status",
                table: "Organizations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_SubscriptionPlanId",
                table: "Organizations",
                column: "SubscriptionPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationSettings_OrganizationId",
                table: "OrganizationSettings",
                column: "OrganizationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_SmsSettings_Org",
                table: "OrganizationSmsSettings",
                column: "OrganizationId",
                unique: true,
                filter: "\"SiteId\" IS NULL AND \"OrganizationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_SmsSettings_Site",
                table: "OrganizationSmsSettings",
                column: "SiteId",
                unique: true,
                filter: "\"SiteId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationTypeDefaultLoginMethods_OrganizationTypeId_Auth~",
                table: "OrganizationTypeDefaultLoginMethods",
                columns: new[] { "OrganizationTypeId", "AuthenticationType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationTypes_TypeName",
                table: "OrganizationTypes",
                column: "TypeName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_Code",
                table: "Permissions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_PermissionId",
                table: "RolePermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "UX_Roles_Org",
                table: "Roles",
                columns: new[] { "OrganizationId", "Name" },
                unique: true,
                filter: "\"OrganizationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Roles_System",
                table: "Roles",
                column: "Name",
                unique: true,
                filter: "\"OrganizationId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Sites_OrganizationId_Code",
                table: "Sites",
                columns: new[] { "OrganizationId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Sites_OneOrgPrimary",
                table: "Sites",
                column: "OrganizationId",
                unique: true,
                filter: "\"IsPrimary\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_SiteWallets_OrganizationId",
                table: "SiteWallets",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_SiteWallets_SiteId",
                table: "SiteWallets",
                column: "SiteId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SiteWalletTransactions_OrganizationId",
                table: "SiteWalletTransactions",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_SiteWalletTransactions_SiteWalletId_CreatedAt",
                table: "SiteWalletTransactions",
                columns: new[] { "SiteWalletId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "UX_WalletTxn_Idempotency",
                table: "SiteWalletTransactions",
                columns: new[] { "SiteWalletId", "IdempotencyKey" },
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPlans_Name",
                table: "SubscriptionPlans",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminUserRoles");

            migrationBuilder.DropTable(
                name: "AdminUserSites");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "OrganizationBranding");

            migrationBuilder.DropTable(
                name: "OrganizationLoginMethods");

            migrationBuilder.DropTable(
                name: "OrganizationSettings");

            migrationBuilder.DropTable(
                name: "OrganizationSmsSettings");

            migrationBuilder.DropTable(
                name: "OrganizationTypeDefaultLoginMethods");

            migrationBuilder.DropTable(
                name: "RolePermissions");

            migrationBuilder.DropTable(
                name: "SiteWalletTransactions");

            migrationBuilder.DropTable(
                name: "AdminUsers");

            migrationBuilder.DropTable(
                name: "Permissions");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "SiteWallets");

            migrationBuilder.DropTable(
                name: "Sites");

            migrationBuilder.DropTable(
                name: "Organizations");

            migrationBuilder.DropTable(
                name: "OrganizationTypes");

            migrationBuilder.DropTable(
                name: "SubscriptionPlans");
        }
    }
}
