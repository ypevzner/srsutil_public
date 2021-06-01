using FDA.SRS.Database.Models;
using Microsoft.Data.Entity;
using System.Configuration;

/*
 Add-Migration InitSpl -Project FDA.SRS.Database -StartupProject FDA.SRS.Database -v
 Update-Database -Project FDA.SRS.Database -StartupProject FDA.SRS.Database -v
*/

namespace FDA.SRS.Database
{
	public class SrsDbContext : DbContext
	{
		public DbSet<SplDoc> SplDocs { get; set; }

		public DbSet<SplSet> SplSets { get; set; }

		public DbSet<SplChemical> SplChemicals { get; set; }

		public DbSet<SplDocChemical> SplDocChemicals { get; set; }

		public DbSet<SrsRecord> SrsRecords { get; set; }

		public DbSet<SrsSet> SrsSets { get; set; }

		private static bool _created = false;

		public SrsDbContext()
		{
			if ( !_created ) {
				_created = true;
				Database.EnsureCreated();
			}
		}

		protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
		{
			if ( ConfigurationManager.ConnectionStrings["DefaultConnection"] != null )
				optionsBuilder.UseSqlServer(ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString);
		}

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			// SplDoc
			// SplDoc column types
			modelBuilder.Entity<SplDoc>()
				.Property(d => d.UNII)
				.HasColumnType("varchar(10)");

			modelBuilder.Entity<SplDoc>()
				.Property(d => d.Spl)
				.HasColumnType("xml");

			// SplDoc indices
			modelBuilder.Entity<SplDoc>()
				.HasIndex(b => b.DocId)
				.IsUnique();
			modelBuilder.Entity<SplDoc>()
				.HasIndex(b => b.UNII)
				.IsUnique();

			// SplChemical
			// SplChemical column types
			modelBuilder.Entity<SplChemical>()
				.Property(d => d.UNII)
				.HasColumnType("varchar(10)");

			modelBuilder.Entity<SplChemical>()
				.Property(d => d.InChIKey)
				.HasColumnType("varchar(27)");

			// SplChemical indices
			modelBuilder.Entity<SplChemical>()
				.HasIndex(b => b.UNII)
				.IsUnique();
			modelBuilder.Entity<SplChemical>()
				.HasIndex(b => b.InChIKey)
				.IsUnique();

			// SplDocChemical
			// SplDoc and SplChemical many-to-many
			modelBuilder.Entity<SplDocChemical>()
				.HasOne(dc => dc.Doc)
				.WithMany(d => d.DocChemicals)
				.HasForeignKey(dc => dc.DocId);

			modelBuilder.Entity<SplDocChemical>()
				.HasOne(dc => dc.Chemical)
				.WithMany(c => c.DocChemicals)
				.HasForeignKey(dc => dc.ChemicalId);

			// SrsSet
			// SrsSet column types
			modelBuilder.Entity<SrsSet>()
				.Property(d => d.FileName)
				.HasColumnType("varchar(300)");

			// SrsRecords
			// SrsRecords column types
			modelBuilder.Entity<SrsRecord>()
				.Property(d => d.UNII)
				.HasColumnType("varchar(10)");

			modelBuilder.Entity<SrsRecord>()
				.Property(d => d.InChIKey)
				.HasColumnType("varchar(27)");

			modelBuilder.Entity<SrsRecord>()
				.Property(d => d.DescXml)
				.HasColumnType("xml");

			// SrsRecords indices
			modelBuilder.Entity<SrsRecord>()
				.HasIndex(b => b.SubstanceId);
			modelBuilder.Entity<SrsRecord>()
				.HasIndex(b => b.UNII);
			modelBuilder.Entity<SrsRecord>()
				.HasIndex(b => b.InChIKey);
		}
	}
}
