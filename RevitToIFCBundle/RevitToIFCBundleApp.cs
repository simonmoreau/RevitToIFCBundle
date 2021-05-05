using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using DesignAutomationFramework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitToIFCBundle
{
    [Autodesk.Revit.Attributes.Regeneration(Autodesk.Revit.Attributes.RegenerationOption.Manual)]
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class RevitToIFCBundleApp : IExternalDBApplication
    {
        public ExternalDBApplicationResult OnStartup(Autodesk.Revit.ApplicationServices.ControlledApplication app)
        {
            // Hook up the CustomFailureHandling failure processor.
            app.FailuresProcessing += FailureProcessor;

            // Application.RegisterFailuresProcessor(new RevitToIFCFailuresProcessor());
            RevitToIFCBundleApp.LogTrace("Custom Failures Processor registered.");

            DesignAutomationBridge.DesignAutomationReadyEvent += HandleDesignAutomationReadyEvent;
            return ExternalDBApplicationResult.Succeeded;
        }

        private void FailureProcessor(object sender, FailuresProcessingEventArgs e)
        {
            RevitToIFCBundleApp.LogTrace("Enter Failures Processor event.");
            bool hasFailure = false;
            FailuresAccessor failureAccessor = e.GetFailuresAccessor();

            List<FailureMessageAccessor> fma = failureAccessor.GetFailureMessages().ToList();
            List<ElementId> elementsToDelete = new List<ElementId>();
            foreach (FailureMessageAccessor failureMessageAccessor in fma)
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

            if (elementsToDelete.Count > 0)
            {
                failureAccessor.DeleteElements(elementsToDelete);
            }

            //use the following line to disable the message supressor after the external command ends
            //CachedUiApp.Application.FailuresProcessing -= FaliureProcessor;
            if (hasFailure)
            {
                e.SetProcessingResult(FailureProcessingResult.ProceedWithCommit);
            }
            e.SetProcessingResult(FailureProcessingResult.Continue);
        }

        public ExternalDBApplicationResult OnShutdown(Autodesk.Revit.ApplicationServices.ControlledApplication app)
        {
            return ExternalDBApplicationResult.Succeeded;
        }

        public void HandleDesignAutomationReadyEvent(object sender, DesignAutomationReadyEventArgs e)
        {
            ExportToIFC(e.DesignAutomationData);

            e.Succeeded = true;
        }

        public static void ExportToIFC(DesignAutomationData data)
        {

            if (data == null) throw new ArgumentNullException(nameof(data));

            Application rvtApp = data.RevitApp;
            if (rvtApp == null) throw new InvalidDataException(nameof(rvtApp));

            string modelPath = data.FilePath;
            if (String.IsNullOrWhiteSpace(modelPath)) throw new InvalidDataException(nameof(modelPath));

            Document doc = data.RevitDoc;

            if (doc == null) throw new InvalidOperationException("Could not open document.");

            // This part should be used to open a workshared model (maybe)
            //ModelPath path = ModelPathUtils.ConvertUserVisiblePathToModelPath(data.FilePath);
            //var opts = new OpenOptions
            //{
            //    DetachFromCentralOption = DetachFromCentralOption.DetachAndPreserveWorksets
            //};

            //doc = rvtApp.OpenDocumentFile(path, opts);

            if (doc == null) throw new InvalidOperationException("Could not open document.");

            using (Transaction transaction = new Transaction(doc))
            {
                transaction.Start("Export to IFC");

                IFCExportOptions opt = new IFCExportOptions();

                doc.Export(@".\", "output.ifc", opt);
                transaction.Commit();
            }

            LogTrace("Saving IFC file...");
        }

        /// <summary>
        /// This will appear on the Design Automation output
        /// </summary>
        public static void LogTrace(string format, params object[] args)
        {
            Debug.WriteLine(format, args);
            System.Console.WriteLine(format, args);
        }
    }
}
