using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitToIFCBundle
{
    class RevitToIFCFailuresProcessor : IFailuresProcessor
    {
        public void Dismiss(Document document)
        {
            throw new NotImplementedException();
        }

        const int MAX_RESOLUTION_ATTEMPTS = 3;

        public FailureProcessingResult ProcessFailures(FailuresAccessor failuresAccessor)
        {
            IList<FailureMessageAccessor> failList = new List<FailureMessageAccessor>();
            failList = failuresAccessor.GetFailureMessages();
            int errorCount = 0;
            bool hasError = false;
            bool hasWarning = false;
            IList<FailureResolutionType> resolutionTypeList = new List<FailureResolutionType>();

            foreach (FailureMessageAccessor failure in failList)
            {
                // Check how many resolution types were attempted to try to prevent
                // the application from entering an infinite loop.
                resolutionTypeList = failuresAccessor.GetAttemptedResolutionTypes(failure);

                // Log the failure encountered
                RevitToIFCBundleApp.LogTrace("Default Resolution Caption: " + failure.GetDefaultResolutionCaption());

                if (resolutionTypeList.Count >= MAX_RESOLUTION_ATTEMPTS)
                {
                    RevitToIFCBundleApp.LogTrace("Failure: Attempted to resolve the failure "
                       + failure.GetDescriptionText() + " " + resolutionTypeList.Count
                       + " times with resolution " + failure.GetCurrentResolutionType()
                       + ". Rolling back transaction.");
                    return FailureProcessingResult.ProceedWithRollBack;
                }

                // If the default resolution for the error results in deleting the elements then
                // just skip and proceed with rollback.
                if (failure.GetDefaultResolutionCaption().Equals("Delete Element(s)"))
                {
                    hasError = true;
                    ++errorCount;
                    RevitToIFCBundleApp.LogTrace("Resolving Failure: Delete Element(s).");
                    failuresAccessor.ResolveFailure(failure);
                    RevitToIFCBundleApp.LogTrace("Resolved Failure: Delete Element(s).");
                    //return FailureProcessingResult.ProceedWithRollBack;
                }

                if (failure.GetSeverity() == FailureSeverity.Error && failure.GetFailingElementIds().Count > 0)
                {
                    hasError = true;
                    ++errorCount;
                    failuresAccessor.ResolveFailure(failure);
                }

                if (failure.GetSeverity() == FailureSeverity.Warning)
                {
                    hasWarning = true;
                    failuresAccessor.DeleteWarning(failure);
                }

                // If an attempt to resolve failures are made then return the result with ProceedWithCommit
                // Errors are not removed by resolveErrors - only subsequent regeneration will actually remove them.
                // The removal may also fail - resolution is not guaranteed to succeed. So returning with 
                // FailureProcessingResult.ProceedWithCommit is required
                if (hasWarning || hasError)
                {
                    return FailureProcessingResult.ProceedWithCommit;
                }

                RevitToIFCBundleApp.LogTrace("Failure " + errorCount + ": " + " Severity: " + failure.GetSeverity() + " " + failure.GetDescriptionText());
            }

            // Default: try continuing.
            RevitToIFCBundleApp.LogTrace("Attempting to continue.");
            return FailureProcessingResult.Continue;
        }
    }
}
