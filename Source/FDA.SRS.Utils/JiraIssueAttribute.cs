using System;
using System.Collections.Generic;
using System.Linq;

namespace FDA.SRS.Utils
{
	public class JiraIssueAttribute : Attribute
	{
		private List<string> _jiras;
		public JiraIssueAttribute(params string[] jiras)
		{
			_jiras = jiras.ToList();
		}
	}
}
