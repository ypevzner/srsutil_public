using FDA.SRS.ObjectModel;
using System;

namespace FDA.SRS.Services
{
	public class OrganismResolverResult
	{
		public int Confidence;
		public Organism Organism;
	}

	public interface IOrganismResolver
	{
		OrganismResolverResult Resolve(Organism organism);
	}

	public class NullOrganismResolver : IOrganismResolver
	{
		public OrganismResolverResult Resolve(Organism organism)
		{
			return new OrganismResolverResult {
				Confidence = 100,
				Organism = organism
			};
		}
	}

	public class ITISOrganismResolver : IOrganismResolver
	{
		public OrganismResolverResult Resolve(Organism organism)
		{
			throw new NotImplementedException();
		}
	}
}
