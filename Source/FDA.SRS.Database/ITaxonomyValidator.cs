namespace FDA.SRS.Database
{
	interface ITaxonomyValidator
	{
		bool ValidateName(string name);

		bool ValidateAuthor(string name);

		bool ValidateReference(string name);
	}
}
