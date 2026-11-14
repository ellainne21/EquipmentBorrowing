using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EquipmentBorrowing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIdentifiersReturnDateAndConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Borrowings_EquipmentId",
                table: "Borrowings");

            migrationBuilder.AddColumn<string>(
                name: "Contact",
                table: "Students",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentNumber",
                table: "Students",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "StudentNumberKey",
                table: "Students",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Equipment",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Identifier",
                table: "Equipment",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "IdentifierKey",
                table: "Equipment",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ExpectedReturnDate",
                table: "Borrowings",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateBorrowed",
                table: "Borrowings",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone");

            migrationBuilder.AddColumn<DateTime>(
                name: "ReturnedDate",
                table: "Borrowings",
                type: "date",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_Students_StudentNumberKey",
                table: "Students",
                column: "StudentNumberKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Equipment_IdentifierKey",
                table: "Equipment",
                column: "IdentifierKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Borrowings_OneActivePerEquipment",
                table: "Borrowings",
                column: "EquipmentId",
                unique: true,
                filter: "\"Status\" = 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Borrowings_ExpectedAfterCheckout",
                table: "Borrowings",
                sql: "\"ExpectedReturnDate\" > \"DateBorrowed\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Borrowings_ReturnNotBeforeCheckout",
                table: "Borrowings",
                sql: "\"ReturnedDate\" IS NULL OR \"ReturnedDate\" >= \"DateBorrowed\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Students_StudentNumberKey",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "UX_Equipment_IdentifierKey",
                table: "Equipment");

            migrationBuilder.DropIndex(
                name: "UX_Borrowings_OneActivePerEquipment",
                table: "Borrowings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Borrowings_ExpectedAfterCheckout",
                table: "Borrowings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Borrowings_ReturnNotBeforeCheckout",
                table: "Borrowings");

            migrationBuilder.DropColumn(
                name: "Contact",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "StudentNumber",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "StudentNumberKey",
                table: "Students");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "Identifier",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "IdentifierKey",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "ReturnedDate",
                table: "Borrowings");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ExpectedReturnDate",
                table: "Borrowings",
                type: "timestamp without time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "date");

            migrationBuilder.AlterColumn<DateTime>(
                name: "DateBorrowed",
                table: "Borrowings",
                type: "timestamp without time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "date");

            migrationBuilder.CreateIndex(
                name: "IX_Borrowings_EquipmentId",
                table: "Borrowings",
                column: "EquipmentId");
        }
    }
}
