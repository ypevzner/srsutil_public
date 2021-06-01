using FDA.SRS.ObjectModel;
using FDA.SRS.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace UnitTestProject
{
	[TestClass]
	public class SequenceTests
	{
		// TODO: uncomment when hashcodes for proteins are fixed
		// [TestMethod]
		public void OneSeqUIDTest()
		{
			string seq = "QVQLQESGPGLVRPSQTLSLTCTVSGYSITSDHAWSWVRQPPGRGLEWIGYISYSGITTYNPSLKSRVTMLRDTSKNQFSLRLSSVTAADTAVYYCARSLARTTAMDYWGQGSLVTVSSASTKGPSVFPLAPSSKSTSGGTAALGCLVKDYFPEPVTVSWNSGALTSGVHTFPAVLQSSGLYSLSSVVTVPSSSLGTQTYICNVNHKPSNTKVDKKVEPKSCDKTHTCPPC";
			string revSeq = new String(seq.Reverse().ToArray());
			Assert.AreEqual(new Sequence(null, seq).UID, new Sequence(null, revSeq).UID);
		}

		public class SequenceComparer : IEqualityComparer<Sequence>
		{
			public bool Equals(Sequence x, Sequence y)
			{
				return x.UID == y.UID;
			}

			public int GetHashCode(Sequence obj)
			{
				return obj.UID.GetHashCode();
			}
		}

		[TestMethod]
		public void SeqPermutationUIDTest()
		{
			Dictionary<string, string> uids = new Dictionary<string, string>();
			"QVQLQESG"
				.Permutations()
				.Select(a => new Sequence(null, new String(a.ToArray())))
				.Distinct(new SequenceComparer())
				.AsParallel()
				.ForAll(seq => {
					Assert.IsFalse(uids.ContainsValue(seq.UID));
					lock ( uids )
						uids.Add(seq.ToString(), seq.UID);
				});
		}
	}
}
