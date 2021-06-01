using Microsoft.Data.Entity;
using Microsoft.Data.Entity.Infrastructure;
using Microsoft.Data.Entity.Metadata;
using System;

namespace FDA.SRS.Database.Migrations
{
	[DbContext(typeof(SrsDbContext))]
    partial class SrsDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
            modelBuilder
                .HasAnnotation("ProductVersion", "7.0.0-rc1-16348")
                .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

            modelBuilder.Entity("FDA.SRS.Database.Models.SplChemical", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("InChI")
                        .IsRequired();

                    b.Property<string>("InChIKey")
                        .IsRequired()
                        .HasAnnotation("MaxLength", 27)
                        .HasAnnotation("Relational:ColumnType", "varchar(27)");

                    b.Property<string>("Mol")
                        .IsRequired();

                    b.Property<string>("UNII")
                        .HasAnnotation("MaxLength", 10)
                        .HasAnnotation("Relational:ColumnType", "varchar(10)");

                    b.HasKey("Id");

                    b.HasIndex("InChIKey")
                        .IsUnique();

                    b.HasIndex("UNII")
                        .IsUnique();
                });

            modelBuilder.Entity("FDA.SRS.Database.Models.SplDoc", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<Guid>("DocId");

                    b.Property<string>("Err");

                    b.Property<string>("Sdf");

                    b.Property<string>("Spl")
                        .HasAnnotation("Relational:ColumnType", "xml");

                    b.Property<int?>("SplSetId");

                    b.Property<string>("Srs");

                    b.Property<string>("UNII")
                        .IsRequired()
                        .HasAnnotation("MaxLength", 10)
                        .HasAnnotation("Relational:ColumnType", "varchar(10)");

                    b.Property<int>("Version");

                    b.HasKey("Id");

                    b.HasIndex("DocId")
                        .IsUnique();

                    b.HasIndex("UNII")
                        .IsUnique();
                });

            modelBuilder.Entity("FDA.SRS.Database.Models.SplDocChemical", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<int>("ChemicalId");

                    b.Property<int>("DocId");

                    b.HasKey("Id");
                });

            modelBuilder.Entity("FDA.SRS.Database.Models.SplSet", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("Description");

                    b.Property<DateTime?>("FinishTime");

                    b.Property<string>("Name")
                        .HasAnnotation("MaxLength", 100);

                    b.Property<DateTime?>("StartTime");

                    b.HasKey("Id");
                });

            modelBuilder.Entity("FDA.SRS.Database.Models.SrsRecord", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("Comments");

                    b.Property<string>("Desc");

                    b.Property<string>("DescXml")
                        .HasAnnotation("Relational:ColumnType", "xml");

                    b.Property<string>("Errors");

                    b.Property<string>("InChIKey")
                        .HasAnnotation("MaxLength", 27)
                        .HasAnnotation("Relational:ColumnType", "varchar(27)");

                    b.Property<string>("Ref");

                    b.Property<string>("Sdf");

                    b.Property<int?>("SrsSetId");

                    b.Property<int>("SubstanceId");

                    b.Property<string>("UNII")
                        .HasAnnotation("MaxLength", 10)
                        .HasAnnotation("Relational:ColumnType", "varchar(10)");

                    b.HasKey("Id");

                    b.HasIndex("InChIKey");

                    b.HasIndex("SubstanceId");

                    b.HasIndex("UNII");
                });

            modelBuilder.Entity("FDA.SRS.Database.Models.SrsSet", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("Description");

                    b.Property<string>("FileName")
                        .HasAnnotation("MaxLength", 300)
                        .HasAnnotation("Relational:ColumnType", "varchar(300)");

                    b.Property<DateTime?>("ImportDate");

                    b.Property<DateTime?>("ModificationDate");

                    b.Property<string>("Name")
                        .HasAnnotation("MaxLength", 100);

                    b.HasKey("Id");
                });

            modelBuilder.Entity("FDA.SRS.Database.Models.SplDoc", b =>
                {
                    b.HasOne("FDA.SRS.Database.Models.SplSet")
                        .WithMany()
                        .HasForeignKey("SplSetId");
                });

            modelBuilder.Entity("FDA.SRS.Database.Models.SplDocChemical", b =>
                {
                    b.HasOne("FDA.SRS.Database.Models.SplChemical")
                        .WithMany()
                        .HasForeignKey("ChemicalId");

                    b.HasOne("FDA.SRS.Database.Models.SplDoc")
                        .WithMany()
                        .HasForeignKey("DocId");
                });

            modelBuilder.Entity("FDA.SRS.Database.Models.SrsRecord", b =>
                {
                    b.HasOne("FDA.SRS.Database.Models.SrsSet")
                        .WithMany()
                        .HasForeignKey("SrsSetId");
                });
        }
    }
}
