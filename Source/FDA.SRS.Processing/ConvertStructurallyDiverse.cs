using FDA.SRS.ObjectModel;
using FDA.SRS.Services;
using FDA.SRS.Utils;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using System.Text;
using Newtonsoft.Json.Linq;

namespace FDA.SRS.Processing
{
    public static partial class Converter
    {
        private class SDRecord
        {
            public string unii;
            public SdfRecord sdf;
            public XDocument desc;
            public StructurallyDiverse sd;
            public List<SDRecord> refs = new List<SDRecord>();
        }

        private static void hierSdRecordAction(SDRecord sdr, Action<SDRecord> action)
        {
            action(sdr);
            sdr.refs.ForAll(s => hierSdRecordAction(s, action));
        }

        private static void readSDSdf(ImportOptions impOpt, ConvertOptions opt, ExportOptions expOpt, OperationalParameters pars, Dictionary<string, SDRecord> sdRecords, Action<string, SDRecord> action, HashSet<string> erroneous, List<Tuple<OutputFileType, string>> result)
        {
            int nRecords = 0, nErrors = 0;

            using (SdfReader r = new SdfReader(impOpt.InputFile, impOpt.InputFileEncoding) { FieldsMap = impOpt.SdfMapping })
            {
                foreach (SdfRecord sdf in r.Records)
                {
                    string unii = sdf.GetFieldValue("UNII");
                    if (String.IsNullOrEmpty(unii))
                    {
                        if (opt.GenerateMode == GenerateMode.NewSubstance)
                            unii = "_NEW_UNII_";
                        else
                            throw new SrsException("mandatory_field_missing", "UNII is missing and GenerateMode != NewSubstance");
                    }

                    // As we enter this procedure twice - don't waste time failing twice
                    if (erroneous.Contains(unii))
                    {
                        Trace.TraceWarning("Skipping previously identified erroneous record: {0}", unii);
                        continue;
                    }

                    StructurallyDiverse sdu1 = new StructurallyDiverse { UNII = unii };
                    SDRecord sdrec = null;
                    try
                    //if (1==1)
                    {
                        if (sdRecords.Keys.Contains(unii))
                        {
                            sdrec = sdRecords[unii];

                            // This is the second run - we can associate parents now
                            if (sdrec.sd.SourceMaterial.SubstanceId != null)
                            {
                                if (!sdRecords.ContainsKey(sdrec.sd.SourceMaterial.SubstanceId))
                                    throw new SrsException("sd_parent_not_found", String.Format("Referenced parent substance '{0}' not found", sdrec.sd.SourceMaterial.SubstanceId));

                                sdrec.sd.Parent = sdRecords[sdrec.sd.SourceMaterial.SubstanceId].sd;
                                sdrec.refs.Add(sdRecords[sdrec.sd.SourceMaterial.SubstanceId]);
                            }

                            if (sdrec.sd.AgentModification != null && sdrec.sd.AgentModification.AgentId != null && sdRecords.ContainsKey(sdrec.sd.AgentModification.AgentId))
                            {
                                sdrec.sd.Agent = sdRecords[sdrec.sd.AgentModification.AgentId].sd;
                                sdrec.refs.Add(sdRecords[sdrec.sd.AgentModification.AgentId]);
                            }
                        }
                        else
                        {

                            sdrec = new SDRecord
                            {
                                unii = unii,
                                sd = new StructurallyDiverse { UNII = unii },
                                sdf = sdf,
                                desc = sdf.GetDescXml("DESC_PART", SrsDomain.StructurallyDiverse, impOpt)
                            };

                            String jsonRaw = sdf.GetFieldValue("GSRS_JSON");

                            //Added for Ticket 312 : Structurally Diverse
                            JObject json = JObject.Parse(jsonRaw);

                            Optional<JToken>.ofNullable(json.SelectToken("definitionLevel"))
                                                  .map(u => u.ToString())
                                                  .ifPresent(level =>
                                                  {
                                                      if (level.EqualsNoCase("incomplete"))
                                                      {
                                                          throw new SrsException("not_enough_information", String.Format("Record is incomplete"));
                                                      }
                                                      else if (level.EqualsNoCase("representative"))
                                                      {
                                                          throw new SrsException("not_enough_information", String.Format("Record is representative"));
                                                      }
                                                  });
                            if (sdrec.desc != null)
                            {
                                if (sdrec.desc.ToString().ToUpper().IndexOf("REPRESENTATIVE") > 0)
                                {
                                    throw new SrsException("not_enough_information", String.Format("Record is representative"));
                                }
                            }


                            Optional<JToken>.ofNullable(json.SelectToken("$..structurallyDiverse"))
                                .ifPresent(sd =>
                                {
                                    StructurallyDiverse sdu = new StructurallyDiverse { UNII = unii };
                                    SourceMaterial sm = new SourceMaterial();
                                    sdu.SourceMaterial = sm;

                                    AgentModification am = new AgentModification(null);
                                    sdu.AgentModification = am;

                                    SRSReadingUtils.readJsonElement(sd, "$..sourceMaterialClass", null, v => sdu.SourceMaterial.MaterialClass = v, unii);
                                    SRSReadingUtils.readJsonElement(sd, "$..sourceMaterialType", null, v => sdu.SourceMaterial.MaterialType = v, unii);

                                        //Ticket 365,371
                                        // if (!String.IsNullOrEmpty(sdu.SourceMaterial.MaterialType) && !String.Equals(sdu.SourceMaterial.MaterialType, "VIRUS", StringComparison.InvariantCultureIgnoreCase))
                                        //    throw new SrsException("not_enough_information", String.Format("Non-virus fractions and parts not supported at this time"));

                                        //Added for Ticket 352:Invalidate if Developmental stage is not empty
                                        SRSReadingUtils.readJsonElement(sd, "$..developmentalStage", null, v => sdu.SourceMaterial.DevelopmentalStage = v, unii);
                                    if (!String.IsNullOrEmpty(sdu.SourceMaterial.DevelopmentalStage))
                                        throw new SrsException("general_error", "DevelopmentalStage is not empty");

                                        //SRSReadingUtils.readJsonElement(json, "$..parentSubstance..approvalID", null, v => sdu.SourceMaterial.ApprovalId = v, unii);

                                        //Not sure what this part is trying to do ...
                                        //refPname can show up in a lot of places, and can have a lot of meanings
                                        //probably not a good idea to find any/all of them and set them with any meaning
                                        SRSReadingUtils.readJsonElement(sd, "$..parentSubstance..refPname", null, v => sdu.SourceMaterial.refPname = v, unii);
                                    SRSReadingUtils.readJsonElement(json, "$.._name", null, v => sdu.SourceMaterial.Name = v, unii);

                                    SRSReadingUtils.readJsonElement(sd, "$..parentSubstance.approvalID", null, v => sdu.SourceMaterial.ApprovalId = v, unii);

                                    SRSReadingUtils.readJsonElement(sd, "$..infraSpecificType", null, v => sdu.SourceMaterial.InfraspecificType = v, unii);
                                    SRSReadingUtils.readJsonElement(sd, "$..infraSpecificName", null, v => sdu.SourceMaterial.InfraspecificDescription = v, unii);


                                    sdu.SourceMaterial.Organism = StructurallyDiverseExtensions.ReadOrganismJSON(sdu, sd);

                                        //we should really check if the parentSubstance exists, but for now
                                        //we just check if there's a real organism taxonomy associated.
                                        //If there's a real taxonomy, there's no parent record. If taxonomy is missing,
                                        //then there SHOULD have been a parent record. This could be added as a validation
                                        //rule too.
                                        sdu.SourceMaterial.setHasParent(sdu.SourceMaterial.Organism == null);

                                    IEnumerable<JToken> jParts = json.SelectTokens("$..part");
                                    int c = jParts.Count();

                                        //JToken jt;
                                        sdu.SourceMaterial.Part = jParts.Select(j =>
                                              j.JoinToString(";")
                                            ).JoinToString(";");

                                    SRSReadingUtils.readJsonElement(sd, "$..fractionName", null, v => sdu.SourceMaterial.Fraction = v, unii);

                                    IEnumerable<JToken> jAgentMod = json.SelectTokens("$..agentModifications");
                                    foreach (JToken x in jAgentMod)
                                    {
                                        foreach (JToken ag in x)
                                        {
                                            AgentModification am1 = new AgentModification(null);

                                            SRSReadingUtils.readJsonElement(ag, "$..approvalID", null, v => am1.AgentId = v, unii);
                                            SRSReadingUtils.readJsonElement(ag, "$..refPname", null, v => am1.Name = v, unii);
                                            SRSReadingUtils.readJsonElement(ag, "$..agentModificationRole", null, v => am1.Role = v, unii);
                                            SRSReadingUtils.readJsonElement(ag, "$..agentModificationType", null, v => am1.ModificationType = v, unii);
                                            sdu.AgentModifications.Add(am1);

                                            StructurallyDiverseTerms at = new StructurallyDiverseTerms(am1.Role + "###" + am1.ModificationType);
                                            //StructurallyDiverseTerms at = new StructurallyDiverseTerms("MODIFICATION" + "###" + am1.Role);
                                            if (at.Code == null)
                                              throw new SrsException("mandatory_field_missing", "Role/Type don't exist for Agent Modification");

                                        }
                                    }

                                        //Added for Ticket 371
                                        if (String.Equals(sdu.SourceMaterial.Part, "WHOLE", StringComparison.InvariantCultureIgnoreCase) && sdu.AgentModifications.Count() > 0)
                                        throw new SrsException("general_error", "Wholes with AgentModification Exists");

                                    IEnumerable<JToken> jPhysicalMod = json.SelectTokens("$..physicalModifications");
                                    int phyModCount = jPhysicalMod.Where(m => m is PhysicalModification).Count();
                                    if (phyModCount != 0)
                                    {
                                        throw new Exception("physical modifications not supported at this time!");
                                    }
                                    
                                    //End of 312//

                                    /* commented for ticket 312

                                    var xsds = sdrec.desc.XPathSelectElements("//STRUCTURALLY_DIVERSE");
                                    if (xsds.Count() != 1)
                                        throw new SrsException("invalid_srs_xml", "Number of <STRUCTURALLY_DIVERSE> elements != 1");

                                    string refInfo = sdf.GetConcatXmlFields("REF_INFO_PART");
                                    if (!String.IsNullOrEmpty(refInfo))
                                        sdrec.sd.ParseSdfRefInfo(refInfo);

                                    // Read SRS/GSRS XML
                                    sdrec.sd.ReadStructurallyDiverse(xsds.First());

                                    // Authors must always be present - see comments in SRS-101
                                   // if (String.IsNullOrWhiteSpace(sdrec.sd.Authors))
                                     //   throw new SrsException("not_enough_information", "REF_INFO_PART is missing");

                                    // Fail if type is null and description is not - see comments in SRS-101
                                    if (String.IsNullOrWhiteSpace(sdu.SourceMaterial.Organism.IntraspecificType) && !String.IsNullOrWhiteSpace(sdu.SourceMaterial.Organism.IntraspecificDescription))
                                        throw new SrsException("not_enough_information", "INFRASPECIFIC_TYPE is missing while INFRASPECIFIC_DESCRIPTION is not empty");

                                    // Fail if type is not null and description is - see comments in SRS-101
                                    if (!String.IsNullOrWhiteSpace(sdu.SourceMaterial.Organism.IntraspecificType) && String.IsNullOrWhiteSpace(sdu.SourceMaterial.Organism.IntraspecificDescription))
                                        throw new SrsException("not_enough_information", "INFRASPECIFIC_TYPE is given while INFRASPECIFIC_DESCRIPTION is missing");

                                    if (!String.IsNullOrWhiteSpace(sdu.SourceMaterial.Organism.IntraspecificType)){
                                        if ("CELL LINE".Equals(sdu.SourceMaterial.Organism.IntraspecificType.ToUpper().Trim())) {
                                            throw new SrsException("taxonomy_validation", "INFRASPECIFIC_TYPE can not be 'CELL LINE', that value is not allowed");
                                        }
                                    }
                                     */

                                    // Fail if type is null and description is not - see comments in SRS-101
                                    if (String.IsNullOrWhiteSpace(sdu.SourceMaterial.InfraspecificType) && !String.IsNullOrWhiteSpace(sdu.SourceMaterial.InfraspecificDescription))
                                        throw new SrsException("not_enough_information", "INFRASPECIFIC_TYPE is missing while INFRASPECIFIC_DESCRIPTION is not empty");

                                    // Fail if type is not null and description is - see comments in SRS-101
                                    if (!String.IsNullOrWhiteSpace(sdu.SourceMaterial.InfraspecificType) && String.IsNullOrWhiteSpace(sdu.SourceMaterial.InfraspecificDescription))
                                        throw new SrsException("not_enough_information", "INFRASPECIFIC_TYPE is given while INFRASPECIFIC_DESCRIPTION is missing");

                                    if (!String.IsNullOrWhiteSpace(sdu.SourceMaterial.InfraspecificType))
                                    {
                                        if ("CELL LINE".Equals(sdu.SourceMaterial.InfraspecificType.ToUpper().Trim()))
                                        {
                                            throw new SrsException("taxonomy_validation", "INFRASPECIFIC_TYPE can not be 'CELL LINE', that value is not allowed");
                                        }
                                    }



                                    sdrec.sd = sdu;

                                });


                            if (sdrec.sd != null)
                            {
                                SourceMaterial sm1 = sdrec.sd.SourceMaterial;
                                if (sm1.hasParent())
                                {

                                    if (!String.IsNullOrEmpty(sm1.MaterialType) &&
                                       (String.Equals(sm1.MaterialType, "VIRUS", StringComparison.InvariantCultureIgnoreCase) ||
                                        String.Equals(sm1.MaterialType, "PLANT", StringComparison.InvariantCultureIgnoreCase)))
                                    {
                                        StructurallyDiverseTerms at = new StructurallyDiverseTerms(sm1.Fraction + "###" + sm1.MaterialType);
                                        StructurallyDiverseTerms at1 = new StructurallyDiverseTerms(sm1.Part + "###" + sm1.MaterialType);
                                        
                                        if ((sm1.Fraction != null && at.Code == null) || (sm1.Part != null && at1.Code == null))
                                            throw new SrsException("not_enough_information", String.Format("Unknown Part/Fraction"));

                                        if (String.Equals(sm1.MaterialType, "VIRUS", StringComparison.InvariantCultureIgnoreCase))
                                        {
                                            StructurallyDiverseTerms at3 = new StructurallyDiverseTerms(sm1.Fraction + "#####" + sm1.MaterialType);

                                            if (sm1.Fraction != null && at3.Code == null)
                                                throw new SrsException("not_enough_information", String.Format("Unknown Part/Fraction"));
                                        }

                                        if (sdrec.sd.SourceMaterial.InfraspecificType != null && sdrec.sd.SourceMaterial.InfraspecificDescription != null)
                                        {
                                            StructurallyDiverseTerms at2 = new StructurallyDiverseTerms(sdrec.sd.SourceMaterial.InfraspecificType + "###" + "");
                                            if (sdrec.sd.SourceMaterial.InfraspecificType != null && at2.Code == null)
                                                throw new SrsException("not_enough_information", String.Format("Unknown IntraspecificType")+ " - " + sdrec.sd.SourceMaterial.InfraspecificType);
                                        }

                                    }

                                    else
                                    {
                                        //for now, we don't let non-virus' have parts
                                        //we will need to fix this later
                                        //throw new SrsException("not_enough_information", String.Format("Non-virus fractions and parts not supported at this time"));
                                        //Ticket 275
                                        throw new SrsException("not_enough_information", sm1.MaterialType +" - "+ String.Format("Source MaterialType is not supported"));
                                    }


                                }

                                if (!sdrec.sd.SourceMaterial.hasParent())
                                {
                                    if (!String.IsNullOrEmpty(sdrec.sd.SourceMaterial.MaterialType) &&
                                      (!String.Equals(sdrec.sd.SourceMaterial.MaterialType, "VIRUS", StringComparison.InvariantCultureIgnoreCase) &&
                                       !String.Equals(sdrec.sd.SourceMaterial.MaterialType, "PLANT", StringComparison.InvariantCultureIgnoreCase)))
                                        throw new SrsException("not_enough_information", sdrec.sd.SourceMaterial.MaterialType + " - " + String.Format("Source MaterialType is not supported"));


                                    // Validate against taxonomies
                                    if (_terms != null && !_terms.Contains(sdrec.sd.BibReference))
                                        throw new SrsException("taxonomy_validation", String.Format("Taxonomy validation failed for \"{0}\"", sdrec.sd.BibReference));

                                    //Added for Ticket 371
                                    if (!String.Equals(sdrec.sd.SourceMaterial.Part, "WHOLE", StringComparison.InvariantCultureIgnoreCase))
                                        throw new SrsException("general_error", "No Parent Record Exists");

                                    if (sdrec.sd.SourceMaterial.InfraspecificType != null && sdrec.sd.SourceMaterial.InfraspecificDescription != null)
                                    {
                                        StructurallyDiverseTerms at2 = new StructurallyDiverseTerms(sdrec.sd.SourceMaterial.InfraspecificType + "###" + "");
                                        if (sdrec.sd.SourceMaterial.InfraspecificType != null && at2.Code == null)
                                            throw new SrsException("not_enough_information", String.Format("Unknown IntraspecificType") +" - "+ sdrec.sd.SourceMaterial.InfraspecificType);
                                    }

                                   

                                }
                            }

                        }

                        //YP SRS-390 begin block populate internal model with the derivation process data relevant to hashcode calculation


                        PopulateDerivationProcessModel(sdrec.sd);


                        //YP SRS-390 end block

                        action(unii, sdrec);
                    }
                    catch (Exception ex)
                    {

                        erroneous.Add(unii);

                        var info = pars.GetSubstanceInfo(unii);

                        SplDocument splDoc = null;

                        if (sdrec != null)
                        {

                            splDoc = new SplDocument(sdrec.sd)
                            {
                                DocId = expOpt.DocId ?? Guid.NewGuid(),
                                SetId = expOpt.SetId ?? info.SetId,
                                Version = info.VersionNumber
                            };

                        }
                        else
                        {
                            splDoc = new SplDocument(sdu1)
                            {
                                DocId = expOpt.DocId ?? Guid.NewGuid(),
                                SetId = expOpt.SetId ?? info.SetId,
                                Version = info.VersionNumber
                            };
                        }

                        Interlocked.Increment(ref nErrors);
                        writeErrorFiles(pars, expOpt, new List<SdfRecord> { sdf }, splDoc, ex, result);

                    }
                    // Update processed records stats
                    Interlocked.Increment(ref nRecords);
                    if (nRecords % 100 == 0)
                        Console.Out.Write(".");
                    if (nRecords % 1000 == 0)
                        Console.Out.Write(nRecords);

                }
            }
        }

        static private HashSet<string> _terms;

        public static void PopulateDerivationProcessModel(StructurallyDiverse sd)
        {

            if (sd.SourceMaterial.hasParent() && sd.SourceMaterial.ApprovalId != null) //second clause added by L
            {

                sd.derivation_process = new StructurallyDiverse.DerivationProcess();
                sd.derivation_process.DisplayName = "Modification";
                sd.derivation_process.code = "C25572";
                sd.derivation_process.codeSystem = "2.16.840.1.113883.3.26.1.1";
                sd.derivation_process.interactors.Add(
                    new StructurallyDiverse.DerivationProcess.Interactor
                    {
                        typeCode = "CSM",
                        unii = sd.SourceMaterial.ApprovalId,
                        name = sd.SourceMaterial.refPname,
                        codeSystem = "2.16.840.1.113883.4.9"
                    }
                );

                int SeqCount = 1;

                //Material Type:Virus
                if ((!String.IsNullOrEmpty(sd.SourceMaterial.MaterialType)) && (String.Equals(sd.SourceMaterial.MaterialType, "VIRUS", StringComparison.InvariantCultureIgnoreCase)))
                {
                    //Process Step - 1 :Antigen presentation
                    if (!String.IsNullOrEmpty(sd.SourceMaterial.Fraction))
                    {
                        StructurallyDiverseTerms at = new StructurallyDiverseTerms(sd.SourceMaterial.Fraction + "#####" + sd.SourceMaterial.MaterialType);

                        if (at != null && at.isReal())
                        {

                            sd.SourceMaterial.Code = at.Code;
                            sd.SourceMaterial.CodeSystem = at.CodeSystem;
                            sd.SourceMaterial.DispName = at.DisplayName;
                            sd.SourceMaterial.InteractorCode = at.InteractorCode;
                            sd.SourceMaterial.InteractorCodeSystem = at.InteractorCodeSystem;
                            sd.SourceMaterial.InteractorDisplayName = at.InteractorDisplayName;

                            if (sd.SourceMaterial.InteractorCode != null && sd.SourceMaterial.InteractorCodeSystem != null
                                         && sd.SourceMaterial.InteractorDisplayName != null)
                            {

                                StructurallyDiverse.DerivationProcess component_dp = new StructurallyDiverse.DerivationProcess();
                                component_dp.code = sd.SourceMaterial.Code;
                                component_dp.DisplayName = sd.SourceMaterial.DispName;
                                component_dp.interactors.Add(
                                    new StructurallyDiverse.DerivationProcess.Interactor
                                    {
                                        typeCode = "CSM",
                                        unii = at.InteractorCode,
                                        name = at.InteractorDisplayName,
                                        codeSystem = at.InteractorCodeSystem
                                    });
                                sd.derivation_process.components.Add(new Tuple<StructurallyDiverse.DerivationProcess, int>(component_dp, SeqCount));
                                ++SeqCount;
                            }

                        }
                    }

                    //Take agent as definition of the process
                    //Process Step - 2 :Attenuation Agent
                    if (sd.AgentModifications.Count > 0)
                    {
                        foreach (var s in sd.AgentModifications)
                        {
                            StructurallyDiverseTerms at = new StructurallyDiverseTerms(s.Role + "###" + s.ModificationType);
                            //StructurallyDiverseTerms at = new StructurallyDiverseTerms("MODIFICATION" + "###" + "INACTIVATION");
                            if (at != null && at.isReal())
                            {
                                sd.SourceMaterial.Code = at.Code;
                                sd.SourceMaterial.CodeSystem = at.CodeSystem;
                                sd.SourceMaterial.DispName = at.DisplayName;

                                StructurallyDiverse.DerivationProcess component_dp = new StructurallyDiverse.DerivationProcess();
                                component_dp.DisplayName = at.DisplayName;
                                /*
                                 * component_dp.interactors.Add(
                                    new StructurallyDiverse.DerivationProcess.Interactor
                                    {
                                        typeCode = "CSM",
                                        unii = at.InteractorCode,
                                        name = at.InteractorDisplayName,
                                        codeSystem = at.InteractorCodeSystem
                                    });
                                */
                                if (s.AgentId != null)
                                {

                                    component_dp.DisplayName = at.DisplayName;
                                    component_dp.interactors.Add(
                                        new StructurallyDiverse.DerivationProcess.Interactor
                                        {
                                            typeCode = "CSM",
                                            unii = s.AgentId,
                                            name = s.Name,
                                            codeSystem = at.InteractorCodeSystem
                                        });
                                    sd.derivation_process.components.Add(new Tuple<StructurallyDiverse.DerivationProcess, int>(component_dp, SeqCount));

                                }
                                else
                                {
                                    StructurallyDiverseTerms at1 = new StructurallyDiverseTerms(s.Name + "###" + "");

                                    if (at1 != null && at1.isReal())
                                    {
                                        StructurallyDiverse.DerivationProcess dp1 = new StructurallyDiverse.DerivationProcess();
                                        dp1.DisplayName = at1.DisplayName;
                                        dp1.code = at1.Code;
                                        dp1.codeSystem = at1.CodeSystem;

                                        sd.derivation_process.components.Add(new Tuple<StructurallyDiverse.DerivationProcess, int>(dp1, SeqCount));

                                    }
                                }


                                //xDerivationProcessOuter.Add(xComponent);
                                //xDerivationProcessOuter.Add(xComponentI);

                            }
                        }
                        ++SeqCount;
                    }

                    //Process Step - 3 :Hemagglutinin - Neuraminidase
                    //Purification - Based on part/fraction
                    if (!String.IsNullOrEmpty(sd.SourceMaterial.Part) && !String.Equals(sd.SourceMaterial.Part, "WHOLE", StringComparison.InvariantCultureIgnoreCase))
                    {


                        //if (String.Equals(sd.SourceMaterial.Part, "ENVELOPE", StringComparison.InvariantCultureIgnoreCase) && String.IsNullOrEmpty(sd.SourceMaterial.Fraction))
                        if (String.IsNullOrEmpty(sd.SourceMaterial.Fraction))
                        {
                            StructurallyDiverseTerms at = new StructurallyDiverseTerms(sd.SourceMaterial.Part + "###" + sd.SourceMaterial.MaterialType);

                            if (at != null && at.isReal())
                            {
                                sd.SourceMaterial.Code = at.Code;
                                sd.SourceMaterial.CodeSystem = at.CodeSystem;
                                sd.SourceMaterial.DispName = at.DisplayName;

                                StructurallyDiverse.DerivationProcess component_dp = new StructurallyDiverse.DerivationProcess();
                                component_dp.DisplayName = sd.SourceMaterial.DispName;
                                component_dp.codeSystem = sd.SourceMaterial.CodeSystem;
                                component_dp.code = sd.SourceMaterial.Code;
                                sd.derivation_process.components.Add(new Tuple<StructurallyDiverse.DerivationProcess, int>(component_dp, SeqCount));
                            }

                        }

                        else if (!String.IsNullOrEmpty(sd.SourceMaterial.Fraction))
                        {

                            //StructurallyDiverseTerms at = new StructurallyDiverseTerms(sd.SourceMaterial.Fraction + "#####" + sd.SourceMaterial.MaterialType);
                            StructurallyDiverseTerms at = null;

                            at = new StructurallyDiverseTerms(sd.SourceMaterial.Fraction + "###" + sd.SourceMaterial.MaterialType);
                            
                            if (at != null && at.isReal())
                            {
                                sd.SourceMaterial.Code = at.Code;
                                sd.SourceMaterial.CodeSystem = at.CodeSystem;
                                sd.SourceMaterial.DispName = at.DisplayName;

                                StructurallyDiverse.DerivationProcess component_dp = new StructurallyDiverse.DerivationProcess();
                                component_dp.DisplayName = sd.SourceMaterial.DispName;
                                component_dp.codeSystem = sd.SourceMaterial.CodeSystem;
                                component_dp.code = sd.SourceMaterial.Code;
                                sd.derivation_process.components.Add(new Tuple<StructurallyDiverse.DerivationProcess, int>(component_dp, SeqCount));
                            }

                        }
                    }
                }

                //Added for ticket 275
                //Material Type:Plant
                if ((!String.IsNullOrEmpty(sd.SourceMaterial.MaterialType)) && (String.Equals(sd.SourceMaterial.MaterialType, "PLANT", StringComparison.InvariantCultureIgnoreCase)))
                {
                    //Process Step - 1 
                    if (!String.IsNullOrEmpty(sd.SourceMaterial.Part) &&
                        !String.Equals(sd.SourceMaterial.Part, "whole", StringComparison.InvariantCultureIgnoreCase))
                    {
                        
                        StructurallyDiverseTerms at1 = new StructurallyDiverseTerms(sd.SourceMaterial.Part + "###" + sd.SourceMaterial.MaterialType);

                        if (at1 != null && at1.Code != null)
                        {
                            sd.SourceMaterial.Code = at1.Code;
                            sd.SourceMaterial.CodeSystem = at1.CodeSystem;
                            sd.SourceMaterial.DispName = at1.DisplayName;

                            StructurallyDiverse.DerivationProcess component_dp = new StructurallyDiverse.DerivationProcess();
                            component_dp.DisplayName = sd.SourceMaterial.DispName;
                            component_dp.codeSystem = sd.SourceMaterial.CodeSystem;
                            component_dp.code = sd.SourceMaterial.Code;
                            sd.derivation_process.components.Add(new Tuple<StructurallyDiverse.DerivationProcess, int>(component_dp, SeqCount));
                            ++SeqCount;
                        }
                    }
                    else if (String.IsNullOrEmpty(sd.SourceMaterial.Part) ||
                                String.Equals(sd.SourceMaterial.Part, "whole", StringComparison.InvariantCultureIgnoreCase)
                                && !String.IsNullOrEmpty(sd.SourceMaterial.Fraction))

                    {
                        StructurallyDiverseTerms at2 = new StructurallyDiverseTerms(sd.SourceMaterial.Fraction + "###" + sd.SourceMaterial.MaterialType);
                        if (at2 != null || at2.Code != null)
                        {

                            sd.SourceMaterial.Code = at2.Code;
                            sd.SourceMaterial.CodeSystem = at2.CodeSystem;
                            sd.SourceMaterial.DispName = at2.DisplayName;

                            StructurallyDiverse.DerivationProcess component_dp = new StructurallyDiverse.DerivationProcess();
                            component_dp.DisplayName = sd.SourceMaterial.DispName;
                            component_dp.codeSystem = sd.SourceMaterial.CodeSystem;
                            component_dp.code = sd.SourceMaterial.Code;
                            sd.derivation_process.components.Add(new Tuple<StructurallyDiverse.DerivationProcess, int>(component_dp, SeqCount));
                            ++SeqCount;
                        }
                    }

                    //Take agent as definition of the process
                    //Process Step - 2 :Attenuation Agent
                    if (sd.AgentModifications.Count > 0)
                    {
                        foreach (var s in sd.AgentModifications)
                        {
                            StructurallyDiverseTerms at = new StructurallyDiverseTerms(s.Role + "###" + s.ModificationType);
                            //StructurallyDiverseTerms at = new StructurallyDiverseTerms("MODIFICATION" + "###" + "INACTIVATION");
                            if (at != null && at.isReal())
                            {
                                sd.SourceMaterial.Code = at.Code;
                                sd.SourceMaterial.CodeSystem = at.CodeSystem;
                                sd.SourceMaterial.DispName = at.DisplayName;

                                StructurallyDiverse.DerivationProcess component_dp = new StructurallyDiverse.DerivationProcess();
                                component_dp.DisplayName = at.DisplayName;
                                component_dp.codeSystem = at.CodeSystem;
                                component_dp.code = at.Code;
                                //sd.derivation_process.components.Add(new Tuple<StructurallyDiverse.DerivationProcess, int>(component_dp, SeqCount));
                                //component_dp.interactors.Add(
                                //    new StructurallyDiverse.DerivationProcess.Interactor
                                //    {
                                //        typeCode = "CSM",
                                //        unii = at.InteractorCode,
                                //        name = at.InteractorDisplayName,
                                //        codeSystem = at.InteractorCodeSystem
                                //    });

                                
                                if (s.AgentId != null)
                                {

                                    //component_dp.DisplayName = at.DisplayName;
                                    component_dp.interactors.Add(
                                        new StructurallyDiverse.DerivationProcess.Interactor
                                        {
                                            typeCode = "CSM",
                                            unii = s.AgentId,
                                            name = s.Name,
                                            codeSystem = "2.16.840.1.113883.4.9"
                                        });
                                    sd.derivation_process.components.Add(new Tuple<StructurallyDiverse.DerivationProcess, int>(component_dp, SeqCount));
                                    
                                }
                                else
                                {
                                    StructurallyDiverseTerms at1 = new StructurallyDiverseTerms("ULTRAVIOLET RADIATION" + "###" + "");
                                    if (at1 != null && at1.isReal())
                                    {
                                        StructurallyDiverse.DerivationProcess dp1 = new StructurallyDiverse.DerivationProcess();
                                        dp1.DisplayName = at1.DisplayName;
                                        dp1.code = at1.Code;
                                        dp1.codeSystem = at1.CodeSystem;

                                        sd.derivation_process.components.Add(new Tuple<StructurallyDiverse.DerivationProcess, int>(dp1, SeqCount));
                                    }
                                }
                            }

                        }
                        ++SeqCount;
                    }

                    //Process Step - 3 
                    if (!String.Equals(sd.SourceMaterial.Part, "whole", StringComparison.InvariantCultureIgnoreCase)
                        && !String.IsNullOrEmpty(sd.SourceMaterial.Fraction)
                        && !String.IsNullOrEmpty(sd.SourceMaterial.Part))
                    {
                        StructurallyDiverseTerms at3 = new StructurallyDiverseTerms(sd.SourceMaterial.Fraction + "###" + sd.SourceMaterial.MaterialType);
                        if (at3 != null && at3.Code != null)
                        {
                            sd.SourceMaterial.Code = at3.Code;
                            sd.SourceMaterial.CodeSystem = at3.CodeSystem;
                            sd.SourceMaterial.DispName = at3.DisplayName;

                            StructurallyDiverse.DerivationProcess component_dp = new StructurallyDiverse.DerivationProcess();
                            component_dp.DisplayName = at3.DisplayName;
                            component_dp.codeSystem = at3.CodeSystem;
                            component_dp.code = at3.Code;
                            sd.derivation_process.components.Add(new Tuple<StructurallyDiverse.DerivationProcess, int>(component_dp, SeqCount));
                        }

                    }

                }
                //End 275
            }
        }

        public static List<Tuple<OutputFileType, string>> SD2Spl(ImportOptions impOpt, ConvertOptions opt, ExportOptions expOpt)
        {
            List<Tuple<OutputFileType, string>> result = new List<Tuple<OutputFileType, string>>();
            try
            {
                SplOptions.ImportOptions = impOpt;
                SplOptions.ConvertOptions = opt;
                SplOptions.ExportOptions = expOpt;
                OperationalParameters pars = impOpt.PrepareConversion(expOpt);

                //Added for Ticket 390
                if (pars == null)
                    throw new SrsException("general_error", "File not found");


                if (impOpt.TermsFiles != null && impOpt.TermsFiles.Any())
                {
                    _terms = new HashSet<string>();
                    foreach (var f in impOpt.TermsFiles)
                    {
                        using (StreamReader r = new StreamReader(f))
                        {
                            var line = r.ReadLine();
                            while (line != null)
                            {
                                string term = line.Trim();
                                if (!_terms.Contains(term))
                                    _terms.Add(term);
                                line = r.ReadLine();
                            }
                        }
                    }
                }

                Log.Logger.Information("Converting structurally diverse records using {@ImportOptions}, {@ConvertOptions}, {@ExportOptions}, {@OperationalParameters}", impOpt, opt, expOpt, pars);
                TraceUtils.WriteUNIITrace(TraceEventType.Information, null, null, "Processing file {0} with output into directory {1}...", impOpt.InputFile, expOpt.OutDir);

                Dictionary<string, SDRecord> sdRecords = new Dictionary<string, SDRecord>();
                HashSet<string> erroneous = new HashSet<string>();

                // First pass - collect all records for fetching parents later
                Console.Out.Write("1st pass...");

                readSDSdf(impOpt, opt, expOpt, pars, sdRecords, (unii, sdrec) =>
                {
                    sdRecords.Add(unii, sdrec);
                }, erroneous, result);

                Console.Out.WriteLine("{0} records parsed", sdRecords.Count());

                // Second pass - run through all records again and export SPL
                int nRecords = 0;
                Console.Out.Write("2nd pass...");

                readSDSdf(impOpt, opt, expOpt, pars, sdRecords, (unii, sdrec) =>
                {
                    if (isRecordOfInterest(impOpt, opt, pars, unii))
                    {
                        var info = pars.GetSubstanceInfo(unii);
                        SplDocument splDoc = new SplDocument(sdrec.sd)
                        {
                            DocId = expOpt.DocId ?? Guid.NewGuid(),
                            SetId = expOpt.SetId ?? info.SetId,
                            Version = info.VersionNumber
                        };

                        // Create SPL document and write result files
                        writeResultFiles(opt, expOpt, pars, new List<SdfRecord> { sdrec.sdf }, splDoc, result);
                    }
                }, erroneous, result);

                TraceUtils.WriteUNIITrace(TraceEventType.Information, null, null, "Processed file {0}, {1} records successfully converted", impOpt.InputFile, nRecords);
                Log.Logger.Information("Result {@Result}", result);
            }
            catch (Exception ex) //Added for Ticket 390
            {
                writeError_FileNotFound(new OperationalParameters(), expOpt, null, null,
                new FileNotFoundException(impOpt.InputFile),
                new List<Tuple<OutputFileType, string>>());
            }
            return result;
        }

        private static void RegenerateSDSpl(ImportOptions impOpt, ConvertOptions opt, ExportOptions expOpt, OperationalParameters pars)
        {
            XmlDocument SDSpl_in = new XmlDocument();

            SDSpl_in.Load(impOpt.InputFile);
            XmlElement root = SDSpl_in.DocumentElement;
            var bib_reference = "";
            bib_reference = SDSpl_in.GetElementsByTagName("bibliographicDesignationText")[0].InnerText;

            string characteristic_value = "";
            try
            {
                characteristic_value = SDSpl_in.GetElementsByTagName("characteristic")[0].InnerText;
            }
            catch (Exception ex)
            {
            }


            StringBuilder sb = new StringBuilder(bib_reference);
            if (characteristic_value != "")
                sb.Append(characteristic_value);
            string spl_uid = sb.ToString().GetMD5String();
            string formatted_guid = UIDUtils.FormatAsGuid(spl_uid);
            SplHash splhash = new SplHash(formatted_guid);
            Guid DocId = Guid.NewGuid();

            //YP this block removes existing hashcode element(s)
            XmlNodeList hash_elements = SDSpl_in.GetElementsByTagName("asEquivalentSubstance");
            while (hash_elements.Count > 0)
            {
                hash_elements[0].ParentNode.RemoveChild(hash_elements[0]);
                hash_elements = SDSpl_in.GetElementsByTagName("asEquivalentSubstance");
            }

            //YP this block adds recalculated hashcode element,

            //first let's update document id
            XmlNodeList id_elements = SDSpl_in.GetElementsByTagName("id");
            foreach (XmlNode id_element in id_elements)
            {
                if (id_element.ParentNode.Name == "document")
                {
                    id_element.Attributes["root"].Value = DocId.ToString();
                    break;
                }
            }

            XmlNodeList identified_substances = SDSpl_in.GetElementsByTagName("identifiedSubstance");
            foreach (XmlNode identified_substance in identified_substances)
            {
                //YP identify <identifiedSubstance> node that should contain moieties
                if (identified_substance.ParentNode.Name == "identifiedSubstance")
                {
                    //YP add the hashcode containing element
                    identified_substance.InnerXml = identified_substance.InnerXml + splhash.SPL.ToString(SaveOptions.DisableFormatting);
                }
            }

            string dir = Path.Combine(expOpt.OutDir, OUT_DIR, DocId.ToString(), "a");
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, DocId + ".xml");
            SDSpl_in.Save(file);


        }

        public static List<Tuple<OutputFileType, string>> SDSpl2Spl(ImportOptions impOpt, ConvertOptions opt, ExportOptions expOpt)
        {
            List<Tuple<OutputFileType, string>> result = new List<Tuple<OutputFileType, string>>();


            SplOptions.ImportOptions = impOpt;
            SplOptions.ConvertOptions = opt;
            SplOptions.ExportOptions = expOpt;
            OperationalParameters pars = impOpt.PrepareConversion(expOpt);

            if (impOpt.TermsFiles != null && impOpt.TermsFiles.Any())
            {
                _terms = new HashSet<string>();
                foreach (var f in impOpt.TermsFiles)
                {
                    using (StreamReader r = new StreamReader(f))
                    {
                        var line = r.ReadLine();
                        while (line != null)
                        {
                            string term = line.Trim();
                            if (!_terms.Contains(term))
                                _terms.Add(term);
                            line = r.ReadLine();
                        }
                    }
                }
            }

            Log.Logger.Information("Regenerating structuraly diverse using {@ImportOptions}, {@ConvertOptions}, {@ExportOptions}, {@OperationalParameters}", impOpt, opt, expOpt, pars);
            TraceUtils.WriteUNIITrace(TraceEventType.Information, null, null, "Processing file {0} with output into directory {1}...", impOpt.InputFile, expOpt.OutDir);

            Dictionary<string, SDRecord> sdRecords = new Dictionary<string, SDRecord>();
            HashSet<string> erroneous = new HashSet<string>();


            Console.Out.Write("Regenerating SPL");

            RegenerateSDSpl(impOpt, opt, expOpt, pars);



            return result;
        }

    }
}