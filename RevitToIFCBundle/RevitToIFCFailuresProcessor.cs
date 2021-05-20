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
    }
}
