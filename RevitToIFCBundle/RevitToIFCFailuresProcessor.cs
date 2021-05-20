using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RevitToIFCBundle
{
    class RevitToIFCFailuresProcessor
    {
        public void Dismiss(Document document)
        {
            throw new NotImplementedException();
        }

        const int MAX_RESOLUTION_ATTEMPTS = 3;

        public void FailureProcessor(object sender, FailuresProcessingEventArgs e)
        {
            RevitToIFCBundleApp.LogTrace("Enter FailureProcessor event.");


            FailuresAccessor failureAccessor = e.GetFailuresAccessor();

            e.SetProcessingResult(ProcessFailure(failureAccessor));
        }

        private FailureProcessingResult ProcessFailure(FailuresAccessor failureAccessor)
        {
            RevitToIFCBundleApp.LogTrace("Enter ProcessFailure function - Ligne 30."); 
            bool hasFailure = false;
            IList<FailureResolutionType> resolutionTypeList = new List<FailureResolutionType>();

            List<FailureMessageAccessor> failureMessagesList = failureAccessor.GetFailureMessages().ToList();
            List<ElementId> elementsToDelete = new List<ElementId>();
            foreach (FailureMessageAccessor failureMessageAccessor in failureMessagesList)
            {
                // Log the failure encountered
                RevitToIFCBundleApp.LogTrace("Default Resolution Caption: " + failureMessageAccessor.GetDefaultResolutionCaption());

                // Check how many resolution types were attempted to try to prevent
                // the application from entering an infinite loop.
                resolutionTypeList = failureAccessor.GetAttemptedResolutionTypes(failureMessageAccessor);

                if (resolutionTypeList.Count >= MAX_RESOLUTION_ATTEMPTS)
                {
                    RevitToIFCBundleApp.LogTrace("Failure: Attempted to resolve the failure "
                       + failureMessageAccessor.GetDescriptionText() + " " + resolutionTypeList.Count
                       + " times with resolution " + failureMessageAccessor.GetCurrentResolutionType()
                       + ". Rolling back transaction.");
                    return FailureProcessingResult.ProceedWithRollBack;
                }
                else
                {
                    FailureSeverity failureSeverity = failureAccessor.GetSeverity();
                    string message = failureMessageAccessor.GetDefaultResolutionCaption();

                    if (failureSeverity == FailureSeverity.Error)
                    {
                        // If the default resolution for the error results in deleting the elements delete them
                        if (failureMessageAccessor.GetDefaultResolutionCaption().Equals("Delete Element(s)"))
                        {
                            //use the following lines to delete the warning elements
                            List<ElementId> FailingElementIds = failureMessageAccessor.GetFailingElementIds().ToList();

                            if (FailingElementIds.Count > 0)
                            {
                                ElementId FailingElementId = FailingElementIds[0];
                                if (!elementsToDelete.Contains(FailingElementId))
                                {
                                    elementsToDelete.Add(FailingElementId);
                                }

                                hasFailure = true;

                            }
                        }
                        else
                        {
                            if (failureMessageAccessor.HasResolutions())
                            {
                                failureAccessor.ResolveFailure(failureMessageAccessor);
                                hasFailure = true;
                            }
                            else
                            {
                                failureAccessor.DeleteWarning(failureMessageAccessor);
                            }
                        }
                    }

                    if (failureSeverity == FailureSeverity.Warning)
                    {
                        failureAccessor.DeleteWarning(failureMessageAccessor);
                    }
                }
            }

            if (elementsToDelete.Count > 0)
            {
                failureAccessor.DeleteElements(elementsToDelete);
            }

            //use the following line to disable the message supressor after the external command ends
            //CachedUiApp.Application.FailuresProcessing -= FaliureProcessor;
            if (hasFailure)
            {
                return FailureProcessingResult.ProceedWithCommit;
            }
            return FailureProcessingResult.Continue;
        }

        public FailureProcessingResult ProcessFailures(FailuresAccessor failuresAccessor)
        {
            RevitToIFCBundleApp.LogTrace("Enter ProcessFailures function - Ligne 115.");

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
                    RevitToIFCBundleApp.LogTrace("ProceedWithCommit : Failure " + errorCount + ": " + " Severity: " + failure.GetSeverity() + " - " + failure.GetDescriptionText());
                    return FailureProcessingResult.ProceedWithCommit;
                }

                RevitToIFCBundleApp.LogTrace("Failure " + errorCount + ": " + " Severity: " + failure.GetSeverity() + " - " + failure.GetDescriptionText());
            }

            // Default: try continuing.
            RevitToIFCBundleApp.LogTrace("Attempting to continue.");
            return FailureProcessingResult.Continue;
        }
    }
}
