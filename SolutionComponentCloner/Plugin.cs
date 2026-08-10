using System.ComponentModel.Composition;
using SolutionComponentCloner.Resources;
using SolutionComponentCloner.UI;
using XrmToolBox.Extensibility;
using XrmToolBox.Extensibility.Interfaces;

namespace SolutionComponentCloner
{
    [Export(typeof(IXrmToolBoxPlugin))]
    [ExportMetadata("Name", "Solution Component Cloner")]
    [ExportMetadata("Description", "Copy components from one solution to another, optionally including required components.")]
    [ExportMetadata("SmallImageBase64", PluginImages.SmallImageBase64)]
    [ExportMetadata("BigImageBase64", PluginImages.BigImageBase64)]
    [ExportMetadata("BackgroundColor", "White")]
    [ExportMetadata("PrimaryFontColor", "Black")]
    [ExportMetadata("SecondaryFontColor", "Gray")]
    public class Plugin : PluginBase
    {
        public override IXrmToolBoxPluginControl GetControl()
        {
            return new PluginControl();
        }
    }
}
