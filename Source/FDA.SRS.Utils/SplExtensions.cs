using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace FDA.SRS.Utils
{
	/// <summary>
	/// Helpers to extract respective information from SPL
	/// </summary>
	public static class SplExtensions
	{
		public static string SplUNII(this XDocument xdoc)
		{
			return xdoc
				.Descendants(XName.Get("code", "urn:hl7-org:v3"))
				.Where(e => e.Attribute("codeSystem").Value == "2.16.840.1.113883.4.9")
				.FirstOrDefault()
				?.Attribute("code")
				?.Value;
		}

		public static void SplUNII(this XDocument xdoc, string unii)
		{
			if ( !Regex.IsMatch(unii, "[0-9A-Z]{10}") )
				throw new ArgumentOutOfRangeException("unii", "Malformed UNII string");

			var x = xdoc
				.Descendants(XName.Get("code", "urn:hl7-org:v3"))
				.Where(e => e.Attribute("codeSystem").Value == "2.16.840.1.113883.4.9")
				.FirstOrDefault()
				?.Attribute("code");

			if ( x == null )
				throw new ArgumentNullException("SPL attribute is not found - must be present in scaffold SPL!");

			x.Value = unii;
		}

		/*
		 <id root="12f85f46-acba-4679-9340-f4441e04d5ab" />
		*/
		public static Guid SplDocId(this XDocument xdoc)
		{
			return Guid.Parse(
				xdoc
				.Descendants(XName.Get("id", "urn:hl7-org:v3"))
				.FirstOrDefault()
				?.Attribute("root")
				?.Value
			);
		}
		
		public static void SplDocId(this XDocument xdoc, Guid docId)
		{
			var x = xdoc
				.Descendants(XName.Get("id", "urn:hl7-org:v3"))
				.FirstOrDefault()
				?.Attribute("root");

			if ( x == null )
				throw new ArgumentNullException("SPL attribute is not found - must be present in scaffold SPL!");

			x.Value = docId.ToString();
		}

		/*
		 <setId root="b81a90bc-912f-4046-906c-c24ae9358dfc" />
		*/
		public static Guid SplSetId(this XDocument xdoc)
		{
			return Guid.Parse(
				xdoc
				.Descendants(XName.Get("setId", "urn:hl7-org:v3"))
				.FirstOrDefault()
				?.Attribute("root")
				?.Value
			);
		}

		public static void SplSetId(this XDocument xdoc, Guid setid)
		{
			var x = xdoc
				.Descendants(XName.Get("setId", "urn:hl7-org:v3"))
				.FirstOrDefault()
				?.Attribute("root");

			if ( x == null )
				throw new ArgumentNullException("SPL attribute is not found - must be present in scaffold SPL!");

			x.Value = setid.ToString();
		}

		/*
		 <versionNumber value="1" />
		*/
		public static int SplVersion(this XDocument xdoc)
		{
			return int.Parse(
				xdoc
				.Descendants(XName.Get("versionNumber", "urn:hl7-org:v3"))
				.FirstOrDefault()
				?.Attribute("value")
				?.Value
			);
		}

		public static void SplVersion(this XDocument xdoc, int version)
		{
			var x = xdoc
				.Descendants(XName.Get("versionNumber", "urn:hl7-org:v3"))
				.FirstOrDefault()
				?.Attribute("value");

			if ( x == null )
				throw new ArgumentNullException("SPL attribute is not found - must be present in scaffold SPL!");

			x.Value = version.ToString();
		}

		/*
		 <asEquivalentSubstance>
		   <definingSubstance>
			 <code code="2d2e01e9-8198-de45-02b8-6332e7720dca" codeSystem="2.16.840.1.113883.3.2705" />
		   </definingSubstance>
		 </asEquivalentSubstance>
		*/
		public static Guid SplHash(this XDocument xdoc)
		{
			return Guid.Parse(xdoc
				.Descendants(XName.Get("code", "urn:hl7-org:v3"))
				.Where(e => e.Attribute("codeSystem").Value == "2.16.840.1.113883.3.2705")
				.FirstOrDefault()
				?.Attribute("code")
				?.Value);
		}

		public static void SplHash(this XDocument xdoc, Guid hash)
		{
			var x = xdoc
				.Descendants(XName.Get("code", "urn:hl7-org:v3"))
				.Where(e => e.Attribute("codeSystem").Value == "2.16.840.1.113883.3.2705")
				.FirstOrDefault()
				?.Attribute("code");

			if ( x == null )
				throw new ArgumentNullException("SPL attribute is not found - must be present in scaffold SPL!");

			x.Value = hash.ToString();
		}

		/*
		<subjectOf>
			<document>
				<bibliographicDesignationText>Prostanthera rotundifolia R. Br.</bibliographicDesignationText>
			</document>
		</subjectOf>
		*/
		public static string SplBibRef(this XDocument xdoc)
		{
			return xdoc
				.Descendants(XName.Get("bibliographicDesignationText", "urn:hl7-org:v3"))
				.FirstOrDefault()
				?.Value;
		}

		public static void SplBibRef(this XDocument xdoc, string bibRef)
		{
			var x = xdoc
				.Descendants(XName.Get("bibliographicDesignationText", "urn:hl7-org:v3"))
				.FirstOrDefault();

			if ( x == null )
				throw new ArgumentNullException("SPL attribute is not found - must be present in scaffold SPL!");

			x.Value = bibRef;
		}

		/*
		<subjectOf>
			<characteristic>
				<code code="C103240" codeSystem="2.16.840.1.113883.3.26.1.1" displayName="Chemical Structure" />
				<value xsi:type="ED" mediaType="application/x-mdl-molfile"><![CDATA[
				...
				]]></value>
			</characteristic>
		</subjectOf>
		*/
		public static IEnumerable<string> SplMols(this XDocument xdoc)
		{
			var nsm = new XmlNamespaceManager(new NameTable());
			nsm.AddNamespace("s", "urn:hl7-org:v3");
			var ms = xdoc.XPathSelectElements("//s:moiety[s:subjectOf/s:characteristic/s:value]", nsm);
			foreach ( var m in ms ) {
				var mol = m.XPathSelectElement("s:subjectOf/s:characteristic/s:value[@mediaType='application/x-mdl-molfile']", nsm)?.Value;
				yield return mol;
			}
		}
	}
}
