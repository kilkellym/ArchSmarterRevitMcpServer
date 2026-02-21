namespace RevitMCP.PDFAddin.Common
{
    /// <summary>
    /// Restricts the Export for Markup button to only be enabled when the active view is a ViewSheet.
    /// </summary>
    internal class SheetViewCommandAvailability : IExternalCommandAvailability
    {
        public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories)
        {
            UIDocument activeDoc = applicationData.ActiveUIDocument;
            if (activeDoc?.Document == null)
                return false;

            return activeDoc.Document.ActiveView is ViewSheet;
        }
    }
}
