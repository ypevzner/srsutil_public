using System;
using System.Diagnostics;
using System.IO;

namespace FDA.SRS.Utils
{
	public static class CvspValidation
	{
		public static bool Validate(this SdfRecord sdf, string unii, string ValidationRulesXMLFilePath, string AcidBaseRulesXMLFilePath)
		{
			bool isValid = true;
			if ( !String.IsNullOrEmpty(ValidationRulesXMLFilePath) && File.Exists(ValidationRulesXMLFilePath) && !String.IsNullOrEmpty(AcidBaseRulesXMLFilePath) && File.Exists(AcidBaseRulesXMLFilePath) ) {

				try {
                    //
					/*
					 Acidity ac = new Acidity(AcidBaseRulesXMLFilePath);
					Validation va = new Validation(ValidationRulesXMLFilePath, ac);
					ProcessedSDFRecord processed_sdf_rec = new ProcessedSDFRecord(sdf);
					processed_sdf_rec.tagModifiedSdf = sdf.ToString();
					Validation.runValidation(processed_sdf_rec.ToString()); // TOFIX: semantics changed
					foreach ( var issue in processed_sdf_rec.validation_issues ) {
						if ( issue.issue_severity == ChemValidator.ChemValidatorEnums.CVSP_IssueSeverity.Information )
							TraceUtils.WriteUNIITrace(TraceEventType.Information, unii, null, issue.issue_description);
						else if ( issue.issue_severity == ChemValidator.ChemValidatorEnums.CVSP_IssueSeverity.Warning )
							TraceUtils.WriteUNIITrace(TraceEventType.Warning, unii, null, issue.issue_description);
						else if ( issue.issue_severity == ChemValidator.ChemValidatorEnums.CVSP_IssueSeverity.Error ) {
							TraceUtils.WriteUNIITrace(TraceEventType.Error, unii, null, issue.issue_description);
							isValid = false;
						}
					}
					 */
				}
				catch ( Exception ex ) {
					TraceUtils.WriteUNIITrace(TraceEventType.Error, unii, null, "Validation failed: {0}", ex.Message);
					isValid = false;
				}
			}
			else
				isValid = false;

			return isValid;
		}
	}
}
