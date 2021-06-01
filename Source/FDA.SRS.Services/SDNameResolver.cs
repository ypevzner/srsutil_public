using FDA.SRS.ObjectModel;
using System;

namespace FDA.SRS.Services
{
	public class SDNameResolverResult
	{
		public int Confidence;
		public string Name;
	}

	public interface ISDNameResolver
	{
		SDNameResolverResult Resolve(SourceMaterial sourceMaterial);
	}

	public class SimpleSDNameResolver : ISDNameResolver
	{
		public SDNameResolverResult Resolve(SourceMaterial sourceMaterial)
		{
			return new SDNameResolverResult {
				Confidence = 100,
				Name = String.Join(" ", sourceMaterial.Reference, sourceMaterial.Part).Trim()
			};
		}
	}

}
