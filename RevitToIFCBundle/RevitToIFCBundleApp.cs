using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
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
            DesignAutomationBridge.DesignAutomationReadyEvent += HandleDesignAutomationReadyEvent;
            return ExternalDBApplicationResult.Succeeded;
        }

        public ExternalDBApplicationResult OnShutdown(Autodesk.Revit.ApplicationServices.ControlledApplication app)
        {
            return ExternalDBApplicationResult.Succeeded;
        }

        public void HandleDesignAutomationReadyEvent(object sender, DesignAutomationReadyEventArgs e)
        {
            // Hook up the CustomFailureHandling failure processor.
            Application.RegisterFailuresProcessor(new RevitToIFCFailuresProcessor());
            
            ExportToIFC(e.DesignAutomationData);

            e.Succeeded = true;
        }

        public static void ExportToIFC(DesignAutomationData data)
        {

            if (data == null) throw new ArgumentNullException(nameof(data));

            // Document doc = data.RevitDoc;
            Application application = data.RevitApp;

            ModelPath path = ModelPathUtils.ConvertUserVisiblePathToModelPath(data.FilePath);
            var opts = new OpenOptions
            {
                DetachFromCentralOption = DetachFromCentralOption.DetachAndPreserveWorksets
            };

            Document doc = application.OpenDocumentFile(path, opts);

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
        public static void LogTrace(string format, params object[] args) {
            Debug.WriteLine(format, args);
            System.Console.WriteLine(format, args); 
        }
    }
}
