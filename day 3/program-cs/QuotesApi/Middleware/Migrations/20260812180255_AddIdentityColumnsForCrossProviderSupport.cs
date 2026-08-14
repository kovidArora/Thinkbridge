using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuotesApi.Migrations
{
    /// <inheritdoc />
    public partial class AddIdentityColumnsForCrossProviderSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
            {
                migrationBuilder.AlterColumn<int>(
                    name: "Id",
                    table: "Quotes",
                    type: "int",
                    nullable: false,
                    oldClrType: typeof(int),
                    oldType: "int")
                    .Annotation("SqlServer:Identity", "1, 1")
                    .OldAnnotation("SqlServer:Identity", null);

                migrationBuilder.AlterColumn<int>(
                    name: "Id",
                    table: "Users",
                    type: "int",
                    nullable: false,
                    oldClrType: typeof(int),
                    oldType: "int")
                    .Annotation("SqlServer:Identity", "1, 1")
                    .OldAnnotation("SqlServer:Identity", null);

                migrationBuilder.AlterColumn<int>(
                    name: "Id",
                    table: "Collections",
                    type: "int",
                    nullable: false,
                    oldClrType: typeof(int),
                    oldType: "int")
                    .Annotation("SqlServer:Identity", "1, 1")
                    .OldAnnotation("SqlServer:Identity", null);

                migrationBuilder.AlterColumn<int>(
                    name: "Id",
                    table: "RefreshTokens",
                    type: "int",
                    nullable: false,
                    oldClrType: typeof(int),
                    oldType: "int")
                    .Annotation("SqlServer:Identity", "1, 1")
                    .OldAnnotation("SqlServer:Identity", null);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally left empty - reverting identity columns
            // is not needed for this project's use case.
        }
    }
}