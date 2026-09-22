using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserPermissions",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermissionKey = table.Column<string>(type: "nvarchar(64)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPermissions", x => new { x.UserId, x.PermissionKey });
                    table.ForeignKey(
                        name: "FK_UserPermissions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserPermissions_Permissions_PermissionKey",
                        column: x => x.PermissionKey,
                        principalTable: "Permissions",
                        principalColumn: "Key",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserPermissions_PermissionKey",
                table: "UserPermissions",
                column: "PermissionKey");

            // Data backfill: permission resolution moves from "derived via
            // role at read time" to "stored directly per user" as of this
            // migration (see UserPermissionEntity's remarks). Without this,
            // every existing user — every real account created before
            // today — would silently lose all access on their next login,
            // since GetPermissionsAsync now reads UserPermissions only.
            // This inserts exactly the permission set each user's current
            // role membership already implies, so behavior is unchanged
            // for existing accounts; only new staff accounts (created via
            // the no-role staff-invite path) go through the new model.
            migrationBuilder.Sql(@"
                INSERT INTO UserPermissions (UserId, PermissionKey)
                SELECT DISTINCT ur.UserId, rp.PermissionKey
                FROM AspNetUserRoles ur
                JOIN RolePermissions rp ON rp.RoleId = ur.RoleId;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserPermissions");
        }
    }
}
