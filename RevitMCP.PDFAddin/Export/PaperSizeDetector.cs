namespace RevitMCP.PDFAddin.Export
{
    /// <summary>
    /// Detects the paper size from a sheet's title block and maps it to a PDFExportOptions paper format.
    /// </summary>
    internal static class PaperSizeDetector
    {
        /// <summary>
        /// Known paper sizes in feet (width x height) mapped to ExportPaperFormat values.
        /// Tolerance is used for fuzzy matching since title block dimensions may not be exact.
        /// </summary>
        private static readonly (double WidthFt, double HeightFt, ExportPaperFormat Format, string Name)[] KnownSizes =
        {
            // ANSI sizes
            (2.8333, 2.1667, ExportPaperFormat.ANSI_A, "ANSI A (11x8.5)"),       // 11" x 8.5" landscape
            (1.4167, 0.9167, ExportPaperFormat.ANSI_A, "ANSI A (11x8.5)"),        // 11" x 8.5" portrait (feet)
            (1.4167, 0.7083, ExportPaperFormat.ANSI_B, "ANSI B (17x11)"),         // 17" x 11"
            (2.0,    1.4167, ExportPaperFormat.ANSI_C, "ANSI C (22x17)"),         // 22" x 17"
            (2.8333, 2.0,    ExportPaperFormat.ANSI_D, "ANSI D (34x22)"),         // 34" x 22"
            (3.6667, 2.8333, ExportPaperFormat.ANSI_E, "ANSI E (44x34)"),         // 44" x 34"

            // ARCH sizes
            (1.0,    0.75,   ExportPaperFormat.ARCH_A, "ARCH A (12x9)"),          // 12" x 9"
            (1.5,    1.0,    ExportPaperFormat.ARCH_B, "ARCH B (18x12)"),         // 18" x 12"
            (2.0,    1.5,    ExportPaperFormat.ARCH_C, "ARCH C (24x18)"),         // 24" x 18"
            (3.0,    2.0,    ExportPaperFormat.ARCH_D, "ARCH D (36x24)"),         // 36" x 24"
            (4.0,    3.0,    ExportPaperFormat.ARCH_E, "ARCH E (48x36)"),         // 48" x 36"
            (3.5,    2.5,    ExportPaperFormat.ARCH_E1, "ARCH E1 (42x30)"),       // 42" x 30"

            // ISO sizes (in feet)
            (3.2742, 2.3106, ExportPaperFormat.ISO_A0, "ISO A0 (1189x841mm)"),    // 1189mm x 841mm
            (2.3106, 1.6371, ExportPaperFormat.ISO_A1, "ISO A1 (841x594mm)"),     // 841mm x 594mm
            (1.6371, 1.1548, ExportPaperFormat.ISO_A2, "ISO A2 (594x420mm)"),     // 594mm x 420mm
            (1.1548, 0.8186, ExportPaperFormat.ISO_A3, "ISO A3 (420x297mm)"),     // 420mm x 297mm
            (0.8186, 0.5774, ExportPaperFormat.ISO_A4, "ISO A4 (297x210mm)"),     // 297mm x 210mm
        };

        /// <summary>
        /// Tolerance in feet for matching title block dimensions to known paper sizes.
        /// ~0.5 inch tolerance to account for rounding and non-exact title blocks.
        /// </summary>
        private const double ToleranceFt = 0.042;

        /// <summary>
        /// Attempts to detect the paper format from the title block on the given sheet.
        /// </summary>
        /// <param name="doc">The Revit document.</param>
        /// <param name="sheet">The sheet to inspect.</param>
        /// <param name="warning">Set to a warning message if detection fails; null on success.</param>
        /// <returns>The detected ExportPaperFormat, or the default ARCH_D if detection fails.</returns>
        public static ExportPaperFormat Detect(Document doc, ViewSheet sheet, out string warning)
        {
            warning = null;

            try
            {
                // Find the title block on this sheet
                using var collector = new FilteredElementCollector(doc, sheet.Id);
                var titleBlock = collector
                    .OfCategory(BuiltInCategory.OST_TitleBlocks)
                    .WhereElementIsNotElementType()
                    .FirstElement();

                if (titleBlock == null)
                {
                    warning = "No title block found on sheet. Used default ARCH D.";
                    return ExportPaperFormat.ARCH_D;
                }

                // Try to read width and height from the title block
                double? widthFt = GetParameterValueAsDouble(titleBlock, "Sheet Width")
                               ?? GetParameterValueAsDouble(titleBlock, "Width");
                double? heightFt = GetParameterValueAsDouble(titleBlock, "Sheet Height")
                                ?? GetParameterValueAsDouble(titleBlock, "Height");

                if (widthFt == null || heightFt == null)
                {
                    // Fallback: try bounding box of the title block
                    var bbox = titleBlock.get_BoundingBox(sheet);
                    if (bbox != null)
                    {
                        widthFt = Math.Abs(bbox.Max.X - bbox.Min.X);
                        heightFt = Math.Abs(bbox.Max.Y - bbox.Min.Y);
                    }
                }

                if (widthFt == null || heightFt == null)
                {
                    warning = "Paper size detection failed for title block. Used default ARCH D.";
                    return ExportPaperFormat.ARCH_D;
                }

                double w = widthFt.Value;
                double h = heightFt.Value;

                // Ensure width >= height for consistent matching (landscape orientation)
                if (h > w)
                    (w, h) = (h, w);

                foreach (var size in KnownSizes)
                {
                    double sw = size.WidthFt;
                    double sh = size.HeightFt;
                    if (sh > sw)
                        (sw, sh) = (sh, sw);

                    if (Math.Abs(w - sw) <= ToleranceFt && Math.Abs(h - sh) <= ToleranceFt)
                    {
                        return size.Format;
                    }
                }

                warning = $"Title block size ({w:F2}' x {h:F2}') does not match a known paper format. Used default ARCH D.";
                return ExportPaperFormat.ARCH_D;
            }
            catch (Exception ex)
            {
                warning = $"Paper size detection failed: {ex.Message}. Used default ARCH D.";
                return ExportPaperFormat.ARCH_D;
            }
        }

        private static double? GetParameterValueAsDouble(Element element, string paramName)
        {
            var param = element.LookupParameter(paramName);
            if (param != null && param.HasValue && param.StorageType == StorageType.Double)
                return param.AsDouble();
            return null;
        }
    }
}
