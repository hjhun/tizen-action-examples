using System.Text.RegularExpressions;
using System.Xml.Linq;

static void Assert(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
var root = Directory.GetCurrentDirectory();
var catalog = Path.GetFullPath(Path.Combine(root, "../..", "appfw/tizen-action/default-actions"));
var sections = new Dictionary<string, List<string>>();
string? category = null;
foreach (var raw in File.ReadLines(Path.Combine(catalog, "action.seq")))
{
    var line = raw.Trim();
    if (line.StartsWith('[')) { category = line[1..^1]; sections[category] = []; }
    else if (category is not null && line.Length > 0 && !line.StartsWith('#')) sections[category].Add(line);
}
var manifest = XDocument.Load(Path.Combine(root, "src/PhotoGallery.App/tizen-manifest.xml"));
XNamespace ns = "http://tizen.org/ns/packages";
var app = manifest.Root!.Element(ns + "ui-application")!;
Assert((string?)app.Attribute("type") == "dotnet" && (string?)app.Attribute("api-version") == "14", "Require .NET API14.");
var metadata = app.Elements(ns + "metadata").Where(e => (string?)e.Attribute("key") == "http://tizen.org/metadata/action/provider")
    .Select(e => (string)e.Attribute("value")!).ToHashSet();
var custom = Directory.GetFiles(Path.Combine(root, "actions"), "*.action").Select(Path.GetFileNameWithoutExtension).Order(StringComparer.Ordinal).Cast<string>().ToList();
foreach (var (name, project, binding, names) in new[]
{
    ("PhotoGallery", "PhotoGallery.ActionProvider", "PhotoGalleryActionProvider", sections["Tizen.Action.Photo"]),
    ("PhotoGalleryCustom", "PhotoGallery.ActionProvider", "PhotoGalleryCustomActionProvider", custom),
    ("View", "PhotoGallery.ViewActionProvider", "PhotoGalleryViewActionProvider", sections["Tizen.Action.View"]),
})
{
    var generated = File.ReadAllText(Path.Combine(root, "src", project, "Generated", binding + ".cs"));
    var methods = Regex.Match(generated, @"private enum MethodId\s*:\s*int\s*\{(.*?)\}", RegexOptions.Singleline).Groups[1].Value;
    for (var index = 0; index < names.Count; index++)
    {
        var method = names[index][(names[index].LastIndexOf('_') + 1)..];
        Assert(Regex.IsMatch(methods, $@"\b{method}\s*=\s*{index + 2},"), name + " method order differs from runtime contract.");
        Assert(metadata.Remove(names[index]), "Missing/duplicate advertised Action " + names[index]);
    }
    Assert(!generated.Contains("TIZEN_RPCPORT_HAS_PRIVILEGE_LOCAL") && !generated.Contains("Disabled for compatibility"), "Generated output was patched.");
}
Assert(metadata.Count == 0, "Manifest advertises Actions outside generated categories.");
Console.WriteLine("PhotoGallery.ActionProvider.Tests: PASS (14 standard/custom/View method IDs and API14 metadata)");
