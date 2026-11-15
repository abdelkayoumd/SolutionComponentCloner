using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using SolutionComponentCloner.Models;
using SolutionComponentCloner.UI;

/// <summary>
/// Hosts the plugin control in a plain form, fills it with sample data through reflection and saves PNGs.
/// Nothing here talks to Dataverse and nothing is saved to the XrmToolBox settings.
/// </summary>
internal static class Program
{
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
    private static readonly StringBuilder Log = new StringBuilder();
    private static readonly Assembly Plugin = typeof(PluginControl).Assembly;

    private static string _outDir;
    private static Form _form;
    private static PluginControl _control;
    private static DataGridView _grid;
    private static List<SolutionComponentItem> _items;
    private static List<SolutionListItem> _solutions;

    [STAThread]
    private static void Main(string[] args)
    {
        _outDir = args.Length > 0 ? args[0] : Path.Combine(Environment.CurrentDirectory, "screenshots");
        Directory.CreateDirectory(_outDir);
        Application.EnableVisualStyles();

        try
        {
            if (args.Length > 1 && args[1] == "readme")
            {
                ReadmeShots();
            }
            else
            {
                ReadmeShots();
                FeatureShots();
            }
        }
        catch (Exception ex)
        {
            Log.AppendLine(ex.ToString());
        }

        File.WriteAllText(Path.Combine(_outDir, "log.txt"), Log.ToString());
    }

    // ---- scenes ------------------------------------------------------------------------------------

    private static void ReadmeShots()
    {
        Open(1200, 700);

        Call(_control, "ShowPickers", true);
        Snap("1-select-solutions");

        Call(_control, "ShowPickers", false);
        foreach (var item in _items.Where(i => i.TableName == "account"))
        {
            item.Selected = true;
        }

        Call(_grid, "NotifySelectionChanged");
        Call(_grid, "ExpandAll");
        _grid.FirstDisplayedScrollingRowIndex = 0;
        Snap("2-components-tree");

        Call(_grid, "CollapseAll");
        Call(_control, "RenderResults", SampleResults(), 12, false, "copied", " Publish your customizations to make them live.");
        Snap("3-copy-results");

        Close();
    }

    private static void FeatureShots()
    {
        Open(1200, 760);
        Call(_control, "ShowPickers", false);

        SetField("_membership", ParseEnum("SolutionComponentCloner.Services.MembershipFilter", "NotInTarget"));
        Call(_control, "ApplyMembership");
        Call(_grid, "ExpandAll");
        Snap("feat-1-not-in-target");

        var results = SampleResults();
        Call(_control, "RenderResults", results, results.Count, false, "removed", "");
        Call(_control, "OfferAddBack", results, _solutions[0], "Add back");
        Call(_control, "ShowResultDetails");
        Snap("feat-2-remove-results-add-back");

        Call(_control, "ResetResults");
        Call(_grid, "SetTargetOnlyItems", TargetOnlyItems());
        SetField("_targetOnlyLoaded", true);
        SetField("_membership", ParseEnum("SolutionComponentCloner.Services.MembershipFilter", "OnlyInTarget"));
        Call(_control, "ApplyMembership");
        Call(_grid, "ExpandAll");
        Call(_control, "UpdateSelectionUi");
        Snap("feat-3-only-in-target");

        SnapDialog("feat-4-modified-since", (Form)Create("SolutionComponentCloner.UI.DatePromptDialog",
            "Select modified since", "Select components modified on or after:", DateTime.Today.AddDays(-7),
            new List<string> { "Alex Roy", "Jo Park", "Sam Doe" }, "Sam Doe"));

        SnapDialog("feat-5-solutions-containing", (Form)Create("SolutionComponentCloner.UI.SolutionListDialog",
            _items.First(i => i.DisplayName == "Account Name (name)"), _solutions.Take(3).ToList()));

        var missing = new SolutionComponentLoadResult { Items = TargetOnlyItems() };
        missing.Items[0].IsManaged = true;
        SnapDialog("feat-6-missing-dependencies", (Form)Create("SolutionComponentCloner.UI.MissingDependenciesDialog",
            missing, "Service Extensions", false));

        Close();
    }

    // ---- set-up -----------------------------------------------------------------------------------

    private static void Open(int width, int height)
    {
        _control = new PluginControl();
        _form = new Form { ClientSize = new Size(width, height), StartPosition = FormStartPosition.Manual, Location = new Point(20, 20) };
        _form.Controls.Add(_control);
        _form.Show();
        Pump();

        GetField<Label>("_lblConnection").Text = "Connected to Contoso Dev";
        SetField("_currentUserName", "Sam Doe");

        _solutions = new List<SolutionListItem>
        {
            Solution("Sales Customizations", "SalesCustomizations", 4),
            Solution("Service Extensions", "ServiceExtensions", 21),
            Solution("Contoso Core", "ContosoCore", 75),
            Solution("Marketing Toolkit", "MarketingToolkit", 140),
            Solution("Partner Pack", "PartnerPack", 300, managed: true)
        };
        var source = GetField<object>("_cmbSource");
        var target = GetField<object>("_cmbTarget");
        Call(source, "SetSolutions", _solutions);
        Call(target, "SetSolutions", _solutions);

        _grid = GetField<DataGridView>("_grid");
        _items = SourceItems();
        Call(_grid, "SetItems", _items);
        Call(source, "SelectByUniqueName", "SalesCustomizations");
        Call(target, "SelectByUniqueName", "ServiceExtensions");

        var targetKeys = new HashSet<(int Type, Guid Id)>(_items.Where((item, index) => index % 5 == 2).Select(i => (i.ComponentType, i.ObjectId)));
        SetField("_targetKeys", targetKeys);
        SetField("_onlyInTargetCount", 5);
        Call(_grid, "ApplyTargetKeys", targetKeys);
        Call(_control, "ApplyMembership");
        Call(_control, "UpdateSelectionUi");
    }

    private static void Close()
    {
        _form.Close();
        _form.Dispose();
    }

    private static SolutionListItem Solution(string name, string uniqueName, int daysOld, bool managed = false) => new SolutionListItem
    {
        SolutionId = Guid.NewGuid(),
        FriendlyName = name,
        UniqueName = uniqueName,
        IsManaged = managed,
        Version = "1.0.0.0",
        CreatedOn = DateTime.UtcNow.AddDays(-daysOld)
    };

    private static List<SolutionComponentItem> SourceItems()
    {
        var items = new List<SolutionComponentItem>();
        foreach (var (table, label, complete) in new[] { ("account", "Account (account)", true), ("contact", "Contact (contact)", false), ("lead", "Lead (lead)", false) })
        {
            var root = Item(1, "Entity", label, "Tables", table, label);
            root.RootComponentBehavior = complete ? 0 : 1;
            items.Add(root);

            foreach (var column in new[] { "Account Name (name)", "Annual Revenue (revenue)", "Email (emailaddress1)", "Main Phone (telephone1)" })
            {
                items.Add(Item(2, "Attribute", table == "account" ? column : column.Replace("Account Name", "Full Name"), "Tables", table, label, "Columns"));
            }

            items.Add(Item(60, "Form", "Main form", "Tables", table, label, "Forms"));
            items.Add(Item(60, "Form", "Quick create", "Tables", table, label, "Forms"));
            items.Add(Item(26, "View", "Active records", "Tables", table, label, "Views"));
            items.Add(Item(26, "View", "My records", "Tables", table, label, "Views"));
        }

        items.Add(Item(80, "Model-driven App", "Sales Hub", "Model-driven apps"));
        items.Add(Item(80, "Model-driven App", "Field Service", "Model-driven apps"));
        items.Add(Item(20, "Security Role", "Sales Manager", "Security roles"));
        items.Add(Item(20, "Security Role", "Salesperson", "Security roles"));
        foreach (var (name, folder) in new[] { ("contoso_/js/account.js", "Code"), ("contoso_/js/lead.js", "Code"), ("contoso_/css/site.css", "Code"), ("contoso_/img/logo.png", "Images") })
        {
            var resource = Item(61, "Web Resource", name, "Web resources");
            resource.SubGroup = folder;
            items.Add(resource);
        }

        var people = new[] { "Sam Doe", "Alex Roy", "Jo Park" };
        for (var n = 0; n < items.Count; n++)
        {
            items[n].ModifiedOn = DateTime.Now.AddDays(-n).AddHours(-n * 3);
            items[n].ModifiedBy = people[n % people.Length];
            items[n].IsManaged = false;
        }

        return items;
    }

    private static List<SolutionComponentItem> TargetOnlyItems()
    {
        var items = new List<SolutionComponentItem>
        {
            Item(1, "Entity", "Case (incident)", "Tables", "incident", "Case (incident)"),
            Item(2, "Attribute", "Case Title (title)", "Tables", "incident", "Case (incident)", "Columns"),
            Item(60, "Form", "Case for Interactive experience", "Tables", "incident", "Case (incident)", "Forms"),
            Item(20, "Security Role", "Customer Service Representative", "Security roles"),
            Item(61, "Web Resource", "contoso_/js/case.js", "Web resources")
        };
        items[4].SubGroup = "Code";
        foreach (var item in items)
        {
            item.ModifiedOn = DateTime.Now.AddDays(-9);
            item.ModifiedBy = "Jo Park";
            item.IsManaged = false;
        }

        return items;
    }

    private static SolutionComponentItem Item(int type, string typeName, string name, string group, string table = null, string tableLabel = null, string category = null) =>
        new SolutionComponentItem
        {
            ObjectId = Guid.NewGuid(),
            ComponentType = type,
            ComponentTypeName = typeName,
            DisplayName = name,
            GroupName = group,
            TableName = table,
            TableLabel = tableLabel,
            Category = category
        };

    private static List<ComponentCopyResult> SampleResults()
    {
        return _items.Where(i => i.TableName == "account").Take(12).Select((component, index) => new ComponentCopyResult
        {
            Component = component,
            Outcome = index == 4 || index == 7 ? CopyOutcome.Failed : CopyOutcome.Success,
            Message = index == 4 || index == 7 ? "The component is required by another component of this solution." : "Done."
        }).ToList();
    }

    // ---- plumbing ---------------------------------------------------------------------------------

    private static void Pump()
    {
        for (var i = 0; i < 6; i++)
        {
            Application.DoEvents();
            System.Threading.Thread.Sleep(30);
        }
    }

    private static void Snap(string name)
    {
        Pump();
        using (var bitmap = new Bitmap(_control.Width, _control.Height))
        {
            _control.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            bitmap.Save(Path.Combine(_outDir, name + ".png"), ImageFormat.Png);
        }
    }

    private static void SnapDialog(string name, Form dialog)
    {
        dialog.StartPosition = FormStartPosition.Manual;
        dialog.Location = new Point(60, 60);
        dialog.Show();
        Pump();
        using (var bitmap = new Bitmap(dialog.Width, dialog.Height))
        {
            dialog.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            bitmap.Save(Path.Combine(_outDir, name + ".png"), ImageFormat.Png);
        }

        dialog.Close();
        dialog.Dispose();
    }

    private static T GetField<T>(string name) => (T)typeof(PluginControl).GetField(name, Any).GetValue(_control);

    private static void SetField(string name, object value) => typeof(PluginControl).GetField(name, Any).SetValue(_control, value);

    private static object ParseEnum(string typeName, string value) => Enum.Parse(Plugin.GetType(typeName, true), value);

    private static object Create(string typeName, params object[] args) =>
        Activator.CreateInstance(Plugin.GetType(typeName, true), Any, null, args, null);

    private static object Call(object target, string method, params object[] args)
    {
        var candidates = target.GetType().GetMethods(Any).Where(m => m.Name == method && m.GetParameters().Length == args.Length).ToList();
        if (candidates.Count == 0)
        {
            throw new MissingMethodException(target.GetType().Name, method);
        }

        return candidates[0].Invoke(target, args);
    }
}
