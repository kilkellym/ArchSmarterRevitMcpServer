using System.Text.Json.Serialization;

namespace RevitMCP.PDFAddin.Models
{
    /// <summary>
    /// Root object for the sidecar JSON file that accompanies a PDF export.
    /// </summary>
    internal class SidecarDocument
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = "1.0";

        [JsonPropertyName("exportDate")]
        public string ExportDate { get; set; }

        [JsonPropertyName("projectName")]
        public string ProjectName { get; set; }

        [JsonPropertyName("revitFilePath")]
        public string RevitFilePath { get; set; }

        [JsonPropertyName("pdfFileName")]
        public string PdfFileName { get; set; }

        [JsonPropertyName("units")]
        public string Units { get; set; } = "feet";

        [JsonPropertyName("warnings")]
        public List<string> Warnings { get; set; } = new();

        [JsonPropertyName("sheets")]
        public List<SheetData> Sheets { get; set; } = new();
    }

    /// <summary>
    /// Metadata for a single sheet in the exported PDF.
    /// </summary>
    internal class SheetData
    {
        [JsonPropertyName("pdfPageNumber")]
        public int PdfPageNumber { get; set; }

        [JsonPropertyName("sheetId")]
        public long SheetId { get; set; }

        [JsonPropertyName("sheetNumber")]
        public string SheetNumber { get; set; }

        [JsonPropertyName("sheetName")]
        public string SheetName { get; set; }

        [JsonPropertyName("viewports")]
        public List<ViewportData> Viewports { get; set; } = new();
    }

    /// <summary>
    /// Metadata for a single viewport on a sheet.
    /// </summary>
    internal class ViewportData
    {
        [JsonPropertyName("viewportId")]
        public long ViewportId { get; set; }

        [JsonPropertyName("viewId")]
        public long ViewId { get; set; }

        [JsonPropertyName("viewName")]
        public string ViewName { get; set; }

        [JsonPropertyName("viewType")]
        public string ViewType { get; set; }

        [JsonPropertyName("scale")]
        public int Scale { get; set; }

        [JsonPropertyName("sheetCenter")]
        public Point2D SheetCenter { get; set; }

        [JsonPropertyName("sheetBounds")]
        public Bounds2D SheetBounds { get; set; }

        [JsonPropertyName("hasModelCoordinates")]
        public bool HasModelCoordinates { get; set; }

        [JsonPropertyName("cropBoxActive")]
        public bool CropBoxActive { get; set; }

        [JsonPropertyName("viewCropBounds")]
        public Bounds2D ViewCropBounds { get; set; }

        [JsonPropertyName("viewCoordinateSystem")]
        public ViewCoordinateSystem ViewCoordinateSystem { get; set; }
    }

    /// <summary>
    /// A 2D point with X and Y coordinates.
    /// </summary>
    internal class Point2D
    {
        [JsonPropertyName("x")]
        public double X { get; set; }

        [JsonPropertyName("y")]
        public double Y { get; set; }
    }

    /// <summary>
    /// A 3D point with X, Y, and Z coordinates.
    /// </summary>
    internal class Point3D
    {
        [JsonPropertyName("x")]
        public double X { get; set; }

        [JsonPropertyName("y")]
        public double Y { get; set; }

        [JsonPropertyName("z")]
        public double Z { get; set; }
    }

    /// <summary>
    /// A 2D bounding rectangle defined by min and max corners.
    /// </summary>
    internal class Bounds2D
    {
        [JsonPropertyName("minX")]
        public double MinX { get; set; }

        [JsonPropertyName("minY")]
        public double MinY { get; set; }

        [JsonPropertyName("maxX")]
        public double MaxX { get; set; }

        [JsonPropertyName("maxY")]
        public double MaxY { get; set; }
    }

    /// <summary>
    /// The view's coordinate system in model space, used for sheet-to-model coordinate mapping.
    /// </summary>
    internal class ViewCoordinateSystem
    {
        [JsonPropertyName("origin")]
        public Point3D Origin { get; set; }

        [JsonPropertyName("rightDirection")]
        public Point3D RightDirection { get; set; }

        [JsonPropertyName("upDirection")]
        public Point3D UpDirection { get; set; }

        [JsonPropertyName("viewDirection")]
        public Point3D ViewDirection { get; set; }
    }
}
