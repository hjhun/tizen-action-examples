namespace PhotoGallery.App;
internal static class GalleryNavigation
{
    internal static int Next(int index, int count, string key, bool grid)
    {
        if (count == 0) return -1;
        var delta = key switch { "Left" => -1, "Right" => 1, "Up" => grid ? -4 : -1, _ => grid ? 4 : 1 };
        return Math.Clamp(Math.Max(0,index) + delta, 0, count - 1);
    }
}
