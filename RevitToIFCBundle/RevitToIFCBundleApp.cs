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
            string buildDate = Properties.Resources.BuildDate;
            // Hook up the CustomFailureHandling failure processor.
            RevitToIFCFailuresProcessor revitToIFCFailuresProcessor = new RevitToIFCFailuresProcessor();
            app.FailuresProcessing += revitToIFCFailuresProcessor.FailureProcessor;
            RevitToIFCBundleApp.LogTrace("Custom Failures Processor registered.");

            DesignAutomationBridge.DesignAutomationReadyEvent += HandleDesignAutomationReadyEvent;
            return ExternalDBApplicationResult.Succeeded;
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

        private void ExportToIFC(DesignAutomationData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            Application rvtApp = data.RevitApp;
            if (rvtApp == null) throw new InvalidDataException(nameof(rvtApp));

            Document inputDoc = data.RevitDoc;
            Document projectDocument = null;

            if (inputDoc.IsFamilyDocument)
            {
                projectDocument = LoadFamilyInDocument(rvtApp, inputDoc);
            }
            else
            {
                projectDocument = inputDoc;
            }

            if (projectDocument == null) throw new InvalidOperationException("Could not open document.");

            // This part should be used to open a workshared model (maybe)
            //ModelPath path = ModelPathUtils.ConvertUserVisiblePathToModelPath(data.FilePath);
            //var opts = new OpenOptions
            //{
            //    DetachFromCentralOption = DetachFromCentralOption.DetachAndPreserveWorksets
            //};

            // doc = rvtApp.OpenDocumentFile(path, opts);

            ExportDocumentToIFC(projectDocument);

        }

        private Document LoadFamilyInDocument(Application rvtApp, Document inputDoc)
        {
            // We must load the family in a new Revit doc
            Document projectDocument = rvtApp.NewProjectDocument(UnitSystem.Metric);
            Family family = inputDoc.LoadFamily(projectDocument);
            FamilyPlacementType familyPlacementType = family.FamilyPlacementType;
            ElementId familySymbolId = family.GetFamilySymbolIds().FirstOrDefault();

            // Find the level whose Name is "Level 1"
            FilteredElementCollector collector = new FilteredElementCollector(projectDocument);
            List<Level> levels = collector.OfClass(typeof(Level)).ToElements().Cast<Level>().ToList();
            Level firstLevel = levels.Where(l => l.Elevation == 0).FirstOrDefault();
            ElementId levelId = firstLevel.Id;
            Reference levelReference = new Reference(firstLevel);
            Line line = Line.CreateBound(new XYZ(-5, 0, 0), new XYZ(5, 0, 0));

            using (Transaction tx = new Transaction(projectDocument))
            {
                tx.Start("Create family instance");

                if (familySymbolId != null)
                {
                    FamilySymbol familySymbol = projectDocument.GetElement(familySymbolId) as FamilySymbol;

                    if (!familySymbol.IsActive) { familySymbol.Activate(); projectDocument.Regenerate(); }

                    if (familySymbol != null)
                    {
                        switch (familyPlacementType)
                        {
                            case FamilyPlacementType.OneLevelBased:
                                projectDocument.Create.NewFamilyInstance(new XYZ(), familySymbol, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                                break;
                            case FamilyPlacementType.OneLevelBasedHosted:

                                Wall wall = Wall.Create(projectDocument, line, levelId, false);

                                tx.Commit();

                                tx.Start("Keep going");
                                projectDocument.Create.NewFamilyInstance(new XYZ(0, 0, 0), familySymbol, wall, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                                break;
                            case FamilyPlacementType.TwoLevelsBased:
                                projectDocument.Create.NewFamilyInstance(new XYZ(), familySymbol, firstLevel, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                                break;
                            case FamilyPlacementType.ViewBased:
                                throw new InvalidOperationException("This Revit family is an annotation, it cannot be exported to IFC.");
                                break;
                            case FamilyPlacementType.WorkPlaneBased:
                                projectDocument.Create.NewFamilyInstance(levelReference, new XYZ(), new XYZ(), familySymbol);
                                break;
                            case FamilyPlacementType.CurveBased:
                                projectDocument.Create.NewFamilyInstance(line, familySymbol, firstLevel, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                                break;
                            case FamilyPlacementType.CurveBasedDetail:
                                throw new InvalidOperationException("This Revit family is an annotation, it cannot be exported to IFC.");
                                break;
                            case FamilyPlacementType.CurveDrivenStructural:
                                projectDocument.Create.NewFamilyInstance(new XYZ(), familySymbol, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                                break;
                            case FamilyPlacementType.Adaptive:
                                AdaptiveComponentInstanceUtils.CreateAdaptiveComponentInstance(projectDocument, familySymbol);
                                break;
                            case FamilyPlacementType.Invalid:
                                throw new InvalidOperationException("This Revit family is invalid, it cannot be exported to IFC.");
                                break;
                            default:
                                break;
                        }
                    }
                }

                tx.Commit();
            }

            return projectDocument;
        }



        private void ExportDocumentToIFC(Document projectDocument)
        {
            using (Transaction transaction = new Transaction(projectDocument))
            {
                transaction.Start("Export to IFC");

                IFCExportOptions opt = new IFCExportOptions();

                projectDocument.Export(@".\", "output.ifc", opt);
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
