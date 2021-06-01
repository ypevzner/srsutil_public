using System;
using System.Text.RegularExpressions;

namespace FDA.SRS.Services
{
	/// <summary>
	/// 
	/// </summary>
	public class Author
	{
		public Guid? ORCID;
		public string FirstName;
		public string LastName;
		public string MiddleName;

		public override string ToString()
		{
			return
				( !String.IsNullOrEmpty(MiddleName) ?
				String.Format("{0} {1} {2}", FirstName, MiddleName, LastName) :
				String.Format("{0} {1}", FirstName, LastName) ).Trim();
		}
	}

	public class AuthorResolverResult
	{
		public int Confidence;
		public Author Author;
	}

	public interface IAuthorResolver
	{
		AuthorResolverResult Resolve(string author);
	}

	public class NullAuthorResolver : IAuthorResolver
	{
		public AuthorResolverResult Resolve(string author)
		{
			return new AuthorResolverResult {
				Confidence = 100,
				Author = new Author {
					LastName = author
				}
			};
		}
	}

	public class ParseAuthorResolver : IAuthorResolver
	{
		public AuthorResolverResult Resolve(string author)
		{
			string[] parts = Regex.Split(author, @"\s+");
			return new AuthorResolverResult {
				Confidence = 100,
				Author = new Author {
					FirstName = parts[0],
					MiddleName = parts[1],
					LastName = parts[2],
				}
			};
		}
	}

	public class ORCIDAuthorResolver : IAuthorResolver
	{
		public AuthorResolverResult Resolve(string author)
		{
			throw new NotImplementedException();
		}
	}
}
