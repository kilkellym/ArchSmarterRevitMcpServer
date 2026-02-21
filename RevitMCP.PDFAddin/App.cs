namespace RevitMCP.PDFAddin
{
    internal class App : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication app)
        {
            // Create ribbon tab (may already exist from another ArchSmarter add-in)
            string tabName = "ArchSmarter";
            try
            {
                app.CreateRibbonTab(tabName);
            }
            catch (Exception)
            {
                // Tab already exists
            }

            // Create ribbon panel
            RibbonPanel panel = Common.Utils.CreateRibbonPanel(app, tabName, "PDF Export");

            // Add the Export for Markup button
            PushButtonData btnData = ExportForMarkupCommand.GetButtonData();
            panel.AddItem(btnData);

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication a)
        {
            return Result.Succeeded;
        }
    }
}
