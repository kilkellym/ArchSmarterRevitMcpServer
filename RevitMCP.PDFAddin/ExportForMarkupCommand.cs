using RevitMCP.PDFAddin.Export;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace RevitMCP.PDFAddin
{
    /// <summary>
    /// Revit external command that exports the active sheet as a PDF with a sidecar JSON file
    /// containing viewport metadata for the markup editor.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class ExportForMarkupCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            // 1. Validate the active view is a ViewSheet
            if (doc.ActiveView is not ViewSheet sheet)
            {
                TaskDialog.Show("Export for Markup",
                    "Please navigate to a sheet view before exporting.");
                return Result.Cancelled;
            }

            // 2. Prompt user for output location
            var saveDialog = new SaveFileDialog
            {
                Title = "Export Sheet for Markup",
                Filter = "PDF Files (*.pdf)|*.pdf",
                FileName = SanitizeFileName(sheet.SheetNumber),
                DefaultExt = ".pdf"
            };

            if (saveDialog.ShowDialog() != true)
                return Result.Cancelled;

            string pdfFullPath = saveDialog.FileName;
            string folderPath = Path.GetDirectoryName(pdfFullPath);
            string baseName = Path.GetFileNameWithoutExtension(pdfFullPath);
            string pdfFileName = Path.GetFileName(pdfFullPath);
            string jsonFullPath = Path.Combine(folderPath, baseName + ".json");

            // 3. Detect paper size from title block
            var paperFormat = PaperSizeDetector.Detect(doc, sheet, out string paperWarning);

            // 4. Export PDF
            bool pdfSuccess = false;
            try
            {
                pdfSuccess = PdfExporter.Export(doc, sheet, folderPath, baseName, paperFormat);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Export for Markup",
                    $"PDF export failed:\n{ex.Message}\n\nThe sidecar JSON will still be generated.");
            }

            // 5. Build and write sidecar JSON
            try
            {
                var builder = new SidecarBuilder(doc);
                var sidecar = builder.Build(sheet, pdfFileName);

                if (paperWarning != null)
                    sidecar.Warnings.Insert(0, paperWarning);

                if (!pdfSuccess)
                    sidecar.Warnings.Insert(0, "PDF export failed. Sidecar JSON was generated without a corresponding PDF.");

                SidecarBuilder.WriteToFile(sidecar, jsonFullPath);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Export for Markup",
                    $"Failed to write sidecar JSON:\n{ex.Message}");
                return Result.Failed;
            }

            // 6. Show success
            string resultMessage = pdfSuccess
                ? $"Export complete!\n\nPDF: {pdfFullPath}\nJSON: {jsonFullPath}"
                : $"Sidecar JSON written (PDF export failed).\n\nJSON: {jsonFullPath}";

            TaskDialog.Show("Export for Markup", resultMessage);

            return Result.Succeeded;
        }

        internal static PushButtonData GetButtonData()
        {
            string buttonInternalName = "btnExportForMarkup";
            string buttonTitle = "Export for\nMarkup";

            Common.ButtonDataClass myButtonData = new Common.ButtonDataClass(
                buttonInternalName,
                buttonTitle,
                typeof(ExportForMarkupCommand).FullName,
                RevitMCP.Addin.PDFExport.Properties.Resources.Blue_32,
                RevitMCP.Addin.PDFExport.Properties.Resources.Blue_16,
                "Export the active sheet as a PDF with a sidecar JSON file for the markup editor. Navigate to a sheet view first.");

            // Override default availability to only enable on sheet views
            myButtonData.Data.AvailabilityClassName = typeof(Common.SheetViewCommandAvailability).FullName;

            return myButtonData.Data;
        }

        private static string SanitizeFileName(string name)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            foreach (char c in invalid)
                name = name.Replace(c, '_');
            return name;
        }
    }
}
