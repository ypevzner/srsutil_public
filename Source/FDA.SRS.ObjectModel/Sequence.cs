using FDA.SRS.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Xml.Linq;

namespace FDA.SRS.ObjectModel
{
	public class Sequence : SplObject, IUniquelyIdentifiable
	{
		private string _id;
		public override string Id
		{
			set
			{
				_id = value;
			}
			get
			{
				if ( _id == null )
					_id = String.Format("SEQ{0}", Interlocked.Increment(ref Counters.SequenceCounter));
				return _id;
			}
		}

		private string _sequence;
		private string _sequenceOriginal;

		private string _uid;
		public string UID
		{
			get
			{
				if ( _uid == null )
					_uid = String.Join("|", _sequence.Select(l => AminoAcids.GetCompoundByLetter(l)).Select(c => c.UID)).GetMD5String();
				return _uid;
			}
		}

		public int Length
		{
			get
			{
				return _sequence.Length;
			}
		}

		public Sequence(SplObject rootObject, string sequence, string id = null, string name = null)
			: base(rootObject, id)
		{
			// Extension = id ?? base.Code;
			Name = name ?? base.Name;

			if ( sequence.Any(c => char.IsLower(c)) )
				TraceUtils.WriteUNIITrace(TraceEventType.Warning, null, null, "Low-case letters used in sequence");

			// sequence = sequence.ToUpper();

			if ( sequence.Any(c => String.IsNullOrEmpty(AminoAcids.GetNameByLetter(c))) )
				TraceUtils.WriteUNIITrace(TraceEventType.Error, null, null, "Unknown letter(s) used in sequence");

			// TODO: Recover and properly fix when working on hash codes invariants
			// Reason: references do not hold when seq is flipped
//begin YB

			//We don't need this for no proteins except the cyclic peptides
			//            string rev_sequence = new String(sequence.Reverse().ToArray());
			//			_sequence = String.Compare(sequence, rev_sequence) <= 0 ? sequence : rev_sequence;
			_sequence = sequence;
//end YB
			_sequenceOriginal = sequence;
		}

		public override string ToString()
		{
			return _sequence;
		}

        /*
         * Get all indexes (0-index) where the specified character is present.
         * 
         * This is used to get a list of amino acid sites for a specific AA residue (e.g. all "C" residues)
         * 
         * 
         * 
         */
        public List<int> SitesMatchingResidue(char res) {
            List<int> matching = new List<int>();

            _sequence.ForEachWithIndex((c, i) => {
                if (res == c) {
                    matching.Add(i);
                }
            });
            return matching;
        }

		public XElement SequenceSpl
		{
			get
			{
				return new SplCharacteristic("AMINO ACID SEQUENCE", _sequenceOriginal).SPL;
			}
		}

		public override XElement SPL
		{
			get
			{
				return
					new XElement(xmlns.spl + "subject",
						new XElement(xmlns.spl + "identifiedSubstance",
							new XElement(xmlns.spl + "id", new XAttribute("extension", Id ?? ""), new XAttribute("root", RootObject.Id)),
							new XElement(xmlns.spl + "identifiedSubstance",
								new XElement(xmlns.spl + "code", new XAttribute("code", Id ?? ""), new XAttribute("codeSystem", RootObject.Id)),
								// new XElement(xmlns.spl + "name", Name ?? "")
								new XElement(xmlns.spl + "moiety",
									SequenceSpl
								)
							)
						)
					);
			}
		}
	}
}
