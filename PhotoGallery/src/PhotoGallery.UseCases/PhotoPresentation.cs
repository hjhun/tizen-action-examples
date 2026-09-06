using System.Text.Json;
using PhotoGallery.Domain;
namespace PhotoGallery.UseCases;

// Explicit legacy A2UI v0.8 split Template/Document compatibility profile.
public static class PhotoPresentation
{
    public static (string Template, string Document) Create(PhotoRecord photo)
    {
        var values = new Dictionary<string, string>
        {
            ["title"] = photo.Title, ["date"] = photo.CapturedAt.ToString("yyyy-MM-dd"),
            ["album"] = photo.Album, ["favorite"] = photo.Favorite ? "Favorite" : "Not a favorite",
            ["ownership"] = photo.Owned ? "Imported copy" : "Device library photo",
        };
        var components = new List<object> { new { id = "root", component = new { Column = new { children = new { explicitList = values.Keys.ToArray() } } } } };
        components.AddRange(values.Keys.Select(key => (object)new { id = key, component = new { Text = new { text = new { path = "/" + key } } } }));
        const string surfaceId = "photogallery-photo";
        return (JsonSerializer.Serialize(new { surfaceUpdate = new { surfaceId, components } }),
            JsonSerializer.Serialize(new { dataModelUpdate = new { surfaceId, path = "/", value = values } }));
    }
}
