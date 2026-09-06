using PhotoGallery.Persistence;
var root=Path.Combine(Path.GetTempPath(),"gallery-meta-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
try
{
    var file=Path.Combine(root,"photos.json");var store=new PhotoMetadataStore(file);
    store.Set("photo-a",new PhotoMetadata("/owned/a.png","Lake",true));
    var restored=new PhotoMetadataStore(file).Get("photo-a");
    if(restored is not {Favorite:true,OwnedPath:"/owned/a.png",Title:"Lake"})throw new Exception("Restart metadata mismatch");
    store.Set("external",new PhotoMetadata("","Garden",true));
    if(store.Get("external")!.OwnedPath!="")throw new Exception("Favorite must not grant ownership");
    Directory.CreateDirectory(file+".pending");
    try { store.Set("external",new PhotoMetadata("","Garden",false));throw new Exception("Expected failed atomic write"); }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    if(store.Get("external")?.Favorite!=true || new PhotoMetadataStore(file).Get("external")?.Favorite!=true)throw new Exception("Failed write changed memory or disk");
    Directory.Delete(file+".pending");
    store.Remove("photo-a");if(new PhotoMetadataStore(file).Get("photo-a") is not null)throw new Exception("Deletion not persisted");
    File.WriteAllText(file,"{broken");try{new PhotoMetadataStore(file);throw new Exception("Corrupt metadata accepted");}catch(System.Text.Json.JsonException){}
    Console.WriteLine("PhotoGallery.Persistence.Tests PASS (restart, ownership, removal, atomic write failure, corrupt state)");
}
finally{Directory.Delete(root,true);}
