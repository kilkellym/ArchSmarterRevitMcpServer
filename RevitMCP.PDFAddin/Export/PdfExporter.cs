using System.Collections.Generic;

namespace RevitMCP.PDFAddin.Export
{
    /// <summary>
    /// Handles PDF export of a Revit sheet using Document.Export with PDFExportOptions.
    /// </summary>
    internal static class PdfExporter
    {
        /// <summary>
        /// Exports the given sheet to a PDF file.
        /// </summary>
        /// <param name="doc">The Revit document.</param>
        /// <param name="sheet">The sheet to export.</param>
        /// <param name="folderPath">Output folder path.</param>
        /// <param name="fileName">Base file name (without extension).</param>
        /// <param name="paperFormat">The paper format to use.</param>
        /// <returns>True if the export succeeded.</returns>
        public static bool Export(Document doc, ViewSheet sheet, string folderPath, string fileName, ExportPaperFormat paperFormat)
        {
            var viewIds = new List<ElementId> { sheet.Id };

            var options = new PDFExportOptions
            {
                FileName = fileName,
                Combine = true,
                ColorDepth = ColorDepthType.Color,
                RasterQuality = RasterQualityType.Medium,
                PaperFormat = paperFormat,
                ZoomType = ZoomType.Zoom,
                ZoomPercentage = 100
            };

            return doc.Export(folderPath, viewIds, options);
        }
    }
}
