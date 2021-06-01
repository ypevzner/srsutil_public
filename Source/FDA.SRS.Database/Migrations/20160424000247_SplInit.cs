using Microsoft.Data.Entity.Metadata;
using Microsoft.Data.Entity.Migrations;
using System;

namespace FDA.SRS.Database.Migrations
{
	public partial class SplInit : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SplChemical",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    InChI = table.Column<string>(nullable: false),
                    InChIKey = table.Column<string>(type: "varchar(27)", nullable: false),
                    Mol = table.Column<string>(nullable: false),
                    UNII = table.Column<string>(type: "varchar(10)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SplChemical", x => x.Id);
                });
            migrationBuilder.CreateTable(
                name: "SplSet",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    Description = table.Column<string>(nullable: true),
                    FinishTime = table.Column<DateTime>(nullable: true),
                    Name = table.Column<string>(nullable: true),
                    StartTime = table.Column<DateTime>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SplSet", x => x.Id);
                });
            migrationBuilder.CreateTable(
                name: "SrsSet",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    Description = table.Column<string>(nullable: true),
                    FileName = table.Column<string>(type: "varchar(300)", nullable: true),
                    ImportDate = table.Column<DateTime>(nullable: true),
                    ModificationDate = table.Column<DateTime>(nullable: true),
                    Name = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SrsSet", x => x.Id);
                });
            migrationBuilder.CreateTable(
                name: "SplDoc",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    DocId = table.Column<Guid>(nullable: false),
                    Err = table.Column<string>(nullable: true),
                    Sdf = table.Column<string>(nullable: true),
                    Spl = table.Column<string>(type: "xml", nullable: true),
                    SplSetId = table.Column<int>(nullable: true),
                    Srs = table.Column<string>(nullable: true),
                    UNII = table.Column<string>(type: "varchar(10)", nullable: false),
                    Version = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SplDoc", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SplDoc_SplSet_SplSetId",
                        column: x => x.SplSetId,
                        principalTable: "SplSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });
            migrationBuilder.CreateTable(
                name: "SrsRecord",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    Comments = table.Column<string>(nullable: true),
                    Desc = table.Column<string>(nullable: true),
                    DescXml = table.Column<string>(type: "xml", nullable: true),
                    Errors = table.Column<string>(nullable: true),
                    InChIKey = table.Column<string>(type: "varchar(27)", nullable: true),
                    Ref = table.Column<string>(nullable: true),
                    Sdf = table.Column<string>(nullable: true),
                    SrsSetId = table.Column<int>(nullable: true),
                    SubstanceId = table.Column<int>(nullable: false),
                    UNII = table.Column<string>(type: "varchar(10)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SrsRecord", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SrsRecord_SrsSet_SrsSetId",
                        column: x => x.SrsSetId,
                        principalTable: "SrsSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });
            migrationBuilder.CreateTable(
                name: "SplDocChemical",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    ChemicalId = table.Column<int>(nullable: false),
                    DocId = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SplDocChemical", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SplDocChemical_SplChemical_ChemicalId",
                        column: x => x.ChemicalId,
                        principalTable: "SplChemical",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SplDocChemical_SplDoc_DocId",
                        column: x => x.DocId,
                        principalTable: "SplDoc",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
            migrationBuilder.CreateIndex(
                name: "IX_SplChemical_InChIKey",
                table: "SplChemical",
                column: "InChIKey",
                unique: true);
            migrationBuilder.CreateIndex(
                name: "IX_SplChemical_UNII",
                table: "SplChemical",
                column: "UNII",
                unique: true);
            migrationBuilder.CreateIndex(
                name: "IX_SplDoc_DocId",
                table: "SplDoc",
                column: "DocId",
                unique: true);
            migrationBuilder.CreateIndex(
                name: "IX_SplDoc_UNII",
                table: "SplDoc",
                column: "UNII",
                unique: true);
            migrationBuilder.CreateIndex(
                name: "IX_SrsRecord_InChIKey",
                table: "SrsRecord",
                column: "InChIKey");
            migrationBuilder.CreateIndex(
                name: "IX_SrsRecord_SubstanceId",
                table: "SrsRecord",
                column: "SubstanceId");
            migrationBuilder.CreateIndex(
                name: "IX_SrsRecord_UNII",
                table: "SrsRecord",
                column: "UNII");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable("SplDocChemical");
            migrationBuilder.DropTable("SrsRecord");
            migrationBuilder.DropTable("SplChemical");
            migrationBuilder.DropTable("SplDoc");
            migrationBuilder.DropTable("SrsSet");
            migrationBuilder.DropTable("SplSet");
        }
    }
}
