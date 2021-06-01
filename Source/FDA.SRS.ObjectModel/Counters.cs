namespace FDA.SRS.ObjectModel
{
	public static class Counters
	{
		public static int FragmentCounter;
		public static int ModificationCounter;
		public static int GlycosylationCounter;
		public static int LinkCounter;
		public static int SubunitCounter;
		public static int SequenceCounter;
        public static int SRUCounter;
        public static int HeadEndCounter;
        public static int TailEndCounter;
        public static int DisconnectedCounter;

        public static void Reset()
		{
			FragmentCounter = 0;
			ModificationCounter = 0;
			GlycosylationCounter = 0;
			LinkCounter = 0;
			SubunitCounter = 0;
			SequenceCounter = 0;
            SRUCounter = 0;
            HeadEndCounter = 0;
            TailEndCounter = 0;
            DisconnectedCounter = 0;
        }
	}
}
