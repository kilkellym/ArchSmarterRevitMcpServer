using System.Collections.Generic;
using System.Text.Json;
using RevitMCP.PDFAddin.Models;

namespace RevitMCP.PDFAddin.Export
{
    /// <summary>
    /// Builds the sidecar JSON document by inspecting a Revit sheet and its viewports.
    /// </summary>
    internal class SidecarBuilder
    {
        private readonly Document _doc;
        private readonly List<string> _warnings = new();

        public SidecarBuilder(Document doc)
        {
            _doc = doc;
        }

        /// <summary>
        /// Builds the complete sidecar document for the given sheet.
        /// </summary>
        /// <param name="sheet">The sheet to gather metadata from.</param>
        /// <param name="pdfFileName">The PDF file name (e.g., "E-101.pdf").</param>
        /// <returns>A populated SidecarDocument ready for serialization.</returns>
        public SidecarDocument Build(ViewSheet sheet, string pdfFileName)
        {
            var sidecar = new SidecarDocument
            {
                ExportDate = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                ProjectName = GetProjectName(),
                RevitFilePath = _doc.PathName ?? "",
                PdfFileName = pdfFileName
            };

            var sheetData = BuildSheetData(sheet, pdfPageNumber: 1);
            sidecar.Sheets.Add(sheetData);
            sidecar.Warnings = _warnings;

            return sidecar;
        }

        /// <summary>
        /// Serializes the sidecar document to JSON and writes it to disk.
        /// </summary>
        public static void WriteToFile(SidecarDocument sidecar, string filePath)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };

            string json = JsonSerializer.Serialize(sidecar, options);
            File.WriteAllText(filePath, json);
        }

        private SheetData BuildSheetData(ViewSheet sheet, int pdfPageNumber)
        {
            var sheetData = new SheetData
            {
                PdfPageNumber = pdfPageNumber,
                SheetId = sheet.Id.Value,
                SheetNumber = sheet.SheetNumber,
                SheetName = sheet.Name
            };

            ICollection<ElementId> viewportIds = sheet.GetAllViewports();
            foreach (var vpId in viewportIds)
            {
                var viewport = _doc.GetElement(vpId) as Viewport;
                if (viewport == null)
                {
                    _warnings.Add($"Viewport {vpId.Value}: Could not retrieve viewport element. Skipped.");
                    continue;
                }

                var view = _doc.GetElement(viewport.ViewId) as View;
                if (view == null)
                {
                    _warnings.Add($"Viewport {vpId.Value}: No associated view found (orphaned viewport). Skipped.");
                    continue;
                }

                var vpData = BuildViewportData(viewport, view);
                sheetData.Viewports.Add(vpData);
            }

            return sheetData;
        }

        private ViewportData BuildViewportData(Viewport viewport, View view)
        {
            var vpData = new ViewportData
            {
                ViewportId = viewport.Id.Value,
                ViewId = view.Id.Value,
                ViewName = GetViewName(view),
                ViewType = view.ViewType.ToString(),
                Scale = view.Scale
            };

            // Sheet-space center and bounds
            try
            {
                XYZ center = viewport.GetBoxCenter();
                vpData.SheetCenter = new Point2D { X = Math.Round(center.X, 6), Y = Math.Round(center.Y, 6) };

                Outline outline = viewport.GetBoxOutline();
                vpData.SheetBounds = new Bounds2D
                {
                    MinX = Math.Round(outline.MinimumPoint.X, 6),
                    MinY = Math.Round(outline.MinimumPoint.Y, 6),
                    MaxX = Math.Round(outline.MaximumPoint.X, 6),
                    MaxY = Math.Round(outline.MaximumPoint.Y, 6)
                };
            }
            catch (Exception ex)
            {
                _warnings.Add($"Viewport {viewport.Id.Value}: Could not read sheet-space bounds. {ex.Message}");
            }

            // Determine if this view type supports model coordinates
            bool supportsModelCoords = SupportsModelCoordinates(view.ViewType);

            if (!supportsModelCoords)
            {
                vpData.HasModelCoordinates = false;
                vpData.CropBoxActive = false;
                return vpData;
            }

            // Crop box data
            try
            {
                vpData.CropBoxActive = view.CropBoxActive;
                if (view.CropBoxActive)
                {
                    BoundingBoxXYZ cropBox = view.CropBox;
                    vpData.ViewCropBounds = new Bounds2D
                    {
                        MinX = Math.Round(cropBox.Min.X, 6),
                        MinY = Math.Round(cropBox.Min.Y, 6),
                        MaxX = Math.Round(cropBox.Max.X, 6),
                        MaxY = Math.Round(cropBox.Max.Y, 6)
                    };
                }
            }
            catch (Exception)
            {
                _warnings.Add($"Viewport {viewport.Id.Value}: Could not read crop box for view '{vpData.ViewName}'. Model coordinates unavailable for this viewport.");
                vpData.HasModelCoordinates = false;
                vpData.CropBoxActive = false;
                return vpData;
            }

            // View coordinate system
            try
            {
                vpData.ViewCoordinateSystem = new ViewCoordinateSystem
                {
                    Origin = ToPoint3D(view.Origin),
                    RightDirection = ToPoint3D(view.RightDirection),
                    UpDirection = ToPoint3D(view.UpDirection),
                    ViewDirection = ToPoint3D(view.ViewDirection)
                };
                vpData.HasModelCoordinates = true;
            }
            catch (Exception)
            {
                _warnings.Add($"Viewport {viewport.Id.Value}: Could not read coordinate system for view '{vpData.ViewName}'. Model coordinates unavailable for this viewport.");
                vpData.HasModelCoordinates = false;
                vpData.ViewCoordinateSystem = null;
            }

            return vpData;
        }

        /// <summary>
        /// Determines whether a view type supports model coordinate mapping.
        /// For the prototype, only plan views get full coordinate support.
        /// </summary>
        private static bool SupportsModelCoordinates(ViewType viewType)
        {
            return viewType switch
            {
                ViewType.FloorPlan => true,
                ViewType.CeilingPlan => true,
                ViewType.AreaPlan => true,
                ViewType.Elevation => true,
                ViewType.Section => true,
                // DraftingView, Legend, Schedule, ThreeD, etc. — no model coordinates
                _ => false
            };
        }

        private string GetProjectName()
        {
            try
            {
                return _doc.ProjectInformation?.Name ?? "";
            }
            catch
            {
                return "";
            }
        }

        private static string GetViewName(View view)
        {
            try
            {
                return view.Name;
            }
            catch
            {
                return $"View {view.Id.Value}";
            }
        }

        private static Point3D ToPoint3D(XYZ xyz)
        {
            return new Point3D
            {
                X = Math.Round(xyz.X, 6),
                Y = Math.Round(xyz.Y, 6),
                Z = Math.Round(xyz.Z, 6)
            };
        }
    }
}
