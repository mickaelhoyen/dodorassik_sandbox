using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Dodorassik.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGameMechanics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "GameMechanics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SourceGame = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Mechanics = table.Column<string[]>(type: "text[]", nullable: false),
                    Themes = table.Column<string[]>(type: "text[]", nullable: false),
                    AgeMin = table.Column<int>(type: "integer", nullable: false),
                    AgeMax = table.Column<int>(type: "integer", nullable: true),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: true),
                    PlayerCountMin = table.Column<int>(type: "integer", nullable: true),
                    PlayerCountMax = table.Column<int>(type: "integer", nullable: true),
                    Format = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Embedding = table.Column<float[]>(type: "vector(1536)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameMechanics", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GameMechanics_Format",
                table: "GameMechanics",
                column: "Format");

            // Index HNSW pour la recherche approchée par cosinus (pgvector).
            // Créé séparément car EF Core ne supporte pas nativement les index
            // vector_cosine_ops. Ignoré si pgvector n'est pas installé.
            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS \"IX_GameMechanics_Embedding_hnsw\" " +
                "ON \"GameMechanics\" USING hnsw (\"Embedding\" vector_cosine_ops);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "GameMechanics");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:vector", ",,");
        }
    }
}
