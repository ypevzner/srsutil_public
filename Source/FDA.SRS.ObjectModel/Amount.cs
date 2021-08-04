using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Linq;
using FDA.SRS.Utils;

namespace FDA.SRS.ObjectModel
{
    /*
	// Exact value
	<quantity>
		<numerator value="22" unit="1"/>
		<denominator value="1" unit="1"/>
	</quantity> 
		 
	// Statistical value
	<quantity>
		<numerator xsi:type="URG_PQ" value="22">
			<low value="11" unit="1"/>
			<high value="33" unit="1"/>
		</numerator>
		<denominator value="1" unit="1"/>
	</quantity>

	// Unknown, can be 0
	<quantity>
		<numerator xsi:type="URG_PQ">
			<low value="0" inclusive="true" unit="1"/>
		</numerator>
		<denominator value="1" unit="1"/>
	</quantity>

	// Unknown, can NOT be 0
	<quantity>
		<numerator xsi:type="URG_PQ">
			<low value="0" inclusive="false" unit="1"/>
		</numerator>
		<denominator value="1" unit="1"/>
	</quantity>
	*/

    public enum AmountType { Exact, Statistical, UncertainZero, UncertainNonZero }

    public class Amount : ISplable, IUniquelyIdentifiable
    {
        public AmountType AmountType { get; set; } = AmountType.Exact;

        public string SrsAmountType { get; set; }

        public string ExtentAmountUnits { get; set; }

        public double? Low { get; set; }

        public double? High { get; set; }

        public double? Center { get; set; }

        public double? Numerator {
            get;
            set;
        }

        //YP SRS-361 need to keep track of whether the denominator is default and not actually read from JSON
        public bool isDefaultDenominator = true;

        public bool isDefaultNumerator = false;

        //YP SRS-361 need to keep track of whether exetnt is PARTIAL so as not to allow AdjustAmounts to reset the type to Statistical
        public bool isExtentPartial = false;

        //YP SRS-413 Complete extent requires some special treatment (default to 1/1 mol) so need this as part of the amount class
        public bool isExtentComplete = false;


        private double _denominator { get; set; } = 1;

        public double Denominator
        {
            get
            {
                //TODO: this is a hacky override to make sure that 0 denominators
                //get converted to 1
                if (this._denominator == 0)
                {
                    return 1;
                }
                return this._denominator;
            }
            set { this._denominator = value; }

        }

        public string NonNumericValue { get; set; }

        public string Unit { get; set; } = "mol";

        //YP SRS-372 replacing this with default DenominatorUnit "mol"
        //as the denominator unit should always be "mol" per Yulia
        //public string DenominatorUnit { get; set; } = "1";
        public string DenominatorUnit { get; set; } = "mol";

        public bool IsWhole
        {
            get { return (Numerator / Denominator) == 1; }
        }

        public override string ToString()
        {
            string v = "";
            if (Numerator != null)
                v = String.Format("<{0}>", Numerator);
            else if (Low == null && High == null)
                if (Center != null)
                    v = String.Format("[{0}]", Center);
                else
                    v = "";
            else if (Low == null)
                v = String.Format("[..{0}]", High);
            else if (High == null)
                v = String.Format("[{0}..]", Low);
            else
                v = String.Format("[{0}..{1}]", Low, High);

            return String.Format("{0} = {1} {2}", AmountType, v, Unit);
        }

        public static Amount UncertainZero
        {
            get { return new Amount { AmountType = AmountType.UncertainZero }; }
        }

        public static Amount UncertainNonZero
        {
            get { return new Amount { AmountType = AmountType.UncertainNonZero }; }
        }

        public Amount()
        {

        }

        public Amount(double a, string u = null, string du = null)
        {
            Numerator = a;

            if (u != null)
                Unit = u;
            if (du != null)
                this.DenominatorUnit = du;
        }

        public Amount(double? a, double? l, double? h, string u = null)
        {
            Numerator = a;
            Low = l;
            High = h;
            AmountType = AmountType.Statistical;
            if (u != null)
                Unit = u;
        }

        //parses "CHEMICALS" tag under SD tag "DESC_PART!" and creates dictionary : key - MOEITY_ID, value: SRS.AMOUNT
        private static Dictionary<string, Amount> getAmounts(SdfRecord sdf)
        {
            Dictionary<string, Amount> comp_amounts = new Dictionary<string, Amount>();
            try
            {
                string xml = null;
                string moeity_id = null;
                if (sdf.HasField("DESC_PART1"))
                {
                    xml = sdf.GetFieldValue("DESC_PART1");
                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.LoadXml(xml);

                    XmlNodeList moiety_groups = xmlDoc.GetElementsByTagName("MOIETY_GROUP");
                    foreach (XmlNode moiety_group in moiety_groups)
                    {
                        foreach (XmlNode moiety_node in moiety_group.ChildNodes)
                        {
                            if (moiety_node.Name.Equals("MOIETY_ID"))
                                moeity_id = moiety_node.InnerText;
                            else if (moiety_node.Name.Equals("MOIETY_AMOUNT"))
                            {
                                double? average = null, high = null, low = null;
                                foreach (XmlNode amount in moiety_node.ChildNodes)
                                {
                                    if (amount.Name.Equals("AVERAGE") && !String.IsNullOrEmpty(amount.InnerText))
                                        average = Convert.ToDouble(amount.InnerText);
                                    else if (amount.Name.Equals("LOW_LIMIT") && !String.IsNullOrEmpty(amount.InnerText))
                                        low = Convert.ToDouble(amount.InnerText);
                                    else if (amount.Name.Equals("HIGH_LIMIT") && !String.IsNullOrEmpty(amount.InnerText))
                                        high = Convert.ToDouble(amount.InnerText);
                                }
                                if (!comp_amounts.ContainsKey(moeity_id))
                                    comp_amounts.Add(moeity_id, new Amount(average, low, high));
                                else
                                    comp_amounts[moeity_id] = new Amount(average, low, high);
                            }
                        }

                    }
                }
            }
            catch (Exception ex)
            {
                throw new SrsException("unknown", "", ex);
            }
            return comp_amounts;
        }

        public string UID
        {
            get
            {
                return IsWhole ? "" : ToString();
            }
        }

        public XElement SPL
        {
            get
            {
                this.AdjustAmount();

                var xNum = new XElement(xmlns.spl + "numerator");

                if (AmountType != AmountType.Exact && Center == null)
                    xNum.Add(new XAttribute(xmlns.xsi + "type", "URG_PQ"));

                if (AmountType == AmountType.Exact)
                {
                    if (Numerator != null)
                        xNum.Add(new XAttribute("value", Numerator == null ? "" : Numerator.ToString()), new XAttribute("unit", Unit.ToString()));
                }
                else if (AmountType == AmountType.Statistical)
                {
                    if (Numerator != null && !isDefaultNumerator && Center != null)
                        xNum.Add(new XAttribute("value", Numerator == null ? "" : Numerator.ToString()));
                    if (Numerator != null && !isDefaultNumerator && Center != null)
                        xNum.Add(new XAttribute("unit", Unit.ToString()));
                    if (Low != null)
                    {
                        xNum.Add(new XElement(xmlns.spl + "low", new XAttribute("value", Low == null ? "" : Low.ToString()), new XAttribute("unit", Unit.ToString())));
                    }
                    if (High != null)
                    {
                        xNum.Add(new XElement(xmlns.spl + "high", new XAttribute("value", High == null ? "" : High.ToString()), new XAttribute("unit", Unit.ToString())));
                    }
                    if (Center != null)
                    {
                        //xNum.Add(new XElement(xmlns.spl + "center", new XAttribute("value", Center == null ? "" : Center.ToString()), new XAttribute("unit", Unit.ToString())));
                        xNum.Add(new XAttribute("value", Center == null ? "" : Center.ToString()), new XAttribute("unit", Unit.ToString()));
                    }
                }
                else if (AmountType == AmountType.UncertainNonZero)
                {
                    xNum.Add(new XElement(xmlns.spl + "low", new XAttribute("value", "0"), new XAttribute("inclusive", "false"), new XAttribute("unit", Unit.ToString())));
                }
                else if (AmountType == AmountType.UncertainZero)
                {
                    xNum.Add(new XElement(xmlns.spl + "low", new XAttribute("value", "0"), new XAttribute("inclusive", "true"), new XAttribute("unit", Unit.ToString())));
                    //YP per SRS-361 partial extent with no amount in the "extentAmount" field (defaulted to 1)
                    if (isExtentPartial && isDefaultDenominator)
                    {
                        Denominator = 1;
                        //YP SRS-406 "inclusive" no longer passes validation
                        //xNum.Add(new XElement(xmlns.spl + "high", new XAttribute("value", "1"), new XAttribute("inclusive", "true"), new XAttribute("unit", Unit.ToString())));
                        xNum.Add(new XElement(xmlns.spl + "high", new XAttribute("value", "1"), new XAttribute("unit", Unit.ToString())));
                    }
                }

                return
                    new XElement(xmlns.spl + "quantity",
                        xNum,
                        new XElement(xmlns.spl + "denominator", new XAttribute("value", Denominator.ToString()), new XAttribute("unit", DenominatorUnit.ToString()))
                    );
            }
        }

        /// <summary>
        /// Heuristic tweaks to set/adjust Amount's props
        /// </summary>
        public void AdjustAmount()
        {
            //only set average value to 1 if low, high and "average" are null
            if (Numerator == null && High == null && Low == null)
            {
                Numerator = 1;
                isDefaultNumerator = true;
            }
                

            //Denominator of 0 almost certainly meant to be 1
            if (Denominator == 0)
                Denominator = 1;

            if (DenominatorUnit.Equals("1") && Denominator != 1)
            {
                Numerator = Numerator / Denominator;
                if (High != null)
                {
                    Low = Low / Denominator;
                }
                if (Low != null)
                {
                    High = High / Denominator;
                }
                Denominator = 1;
            }

            //YP SRS-372, incorporating Center in the check as well
            //YP SRS-372, removed center from check
            //YP SRS-372 putting center back in check
            //YP SRS-372, removed center from check, I know I know...
            //if ((Low != null || High != null || Center != null) && (isExtentPartial == false))
            if ((Low != null || High != null) && (isExtentPartial == false))
                AmountType = AmountType.Statistical;
        }

        public void DivideBy100()
        {
            if (Numerator != null)
            {
                Numerator = Math.Round((double)Numerator / 100,2);
            }
            if (High != null)
            {
                Low = Math.Round((double)Low / 100, 2); ;
            }
            if (Low != null)
            {
                High = Math.Round((double)High / 100, 2); ;
            }       
        }
    }
}
