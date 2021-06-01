using System;

namespace FDA.SRS
{
	public class SrsException : ApplicationException
    {
		public string Category { get; private set; }

		public SrsException(string category, string message)
            : base(message)
        {
			Category = category;
        }

		public SrsException(string category, string message, Exception innerException)
            : base(message, innerException)
        {
			Category = category;
        }
    }
}
