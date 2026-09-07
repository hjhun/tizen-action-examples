using PhotoGallery.App;
static void Check(bool yes, string why) { if (!yes) throw new Exception(why); }
foreach (var (w,h,s,x) in new[] { (1920,1080,1f,0f), (3840,2160,2f,0f), (4096,2160,2f,128f), (7680,4320,4f,0f) })
{
 Check(GalleryDisplayMetrics.TryCreate(w,h,w,h,0,0,0,0,out var d), "valid viewport");
 Check(d.Viewport.Scale==s && d.Viewport.OffsetX==x, "uniform centered scaling");
 Check(d.Viewport.ContentHeight==h, "no duplicate scaling");
}
Check(!GalleryDisplayMetrics.TryCreate(0,0,7680,4320,0,0,0,0,out _), "screen cannot replace minimized window");
foreach (var (w,h) in new[] { (1920,1080), (3840,2160), (4096,2160), (7680,4320) })
{
 Check(GalleryDisplayMetrics.TryCreate(w,h,w,h,40,20,100,70,out var expected), "reference drawable with insets");
 foreach (var (sw,sh) in new[] { (0,0), (0,h), (w,0), (-1,h), (w,-1) })
 {
  Check(GalleryDisplayMetrics.TryCreate(w,h,sw,sh,40,20,100,70,out var actual), "optional screen metadata cannot suppress valid drawable");
  Check(!actual.HasScreenSize && actual.ScreenWidth==0 && actual.ScreenHeight==0, "partial or invalid screen metadata normalizes both dimensions");
  Check(actual.WindowWidth==w && actual.WindowHeight==h && actual.Viewport==expected.Viewport, "metadata failure preserves actual window and inset geometry");
 }
 Check(GalleryDisplayMetrics.TryCreate(w,h,1280,720,40,20,100,70,out var mismatched), "mismatched screen capability remains optional");
 Check(mismatched.HasScreenSize && mismatched.ScreenWidth==1280 && mismatched.ScreenHeight==720 && mismatched.Viewport==expected.Viewport, "HAL capability must not shrink the drawable");
 Check(!GalleryDisplayMetrics.TryCreate(w,h,0,0,w,0,0,0,out _), "missing metadata cannot validate exhausted drawable");
}
Check(!ProportionalViewport.TryCreate(1920,1080,float.NaN,0,0,0,out _), "reject invalid inset");
var inset=ProportionalViewport.Create(1920,1080,100,20,100,20);
Check(inset.OffsetX==100 && inset.OffsetY>=20, "safe area remains centered");
foreach (var (w,h,s,x,y) in new[] { (1280,720,2f/3,0f,0f), (1440,1080,.75f,0f,135f), (2560,1080,1f,320f,0f) })
{
 var viewport=ProportionalViewport.Create(w,h);
 Check(Math.Abs(viewport.Scale-s)<.001f && Math.Abs(viewport.OffsetX-x)<.001f && Math.Abs(viewport.OffsetY-y)<.001f, "smaller, letterbox and ultrawide geometry");
}
Check(!ProportionalViewport.TryCreate(-1,1080,out _), "negative window rejected");
Check(!ProportionalViewport.TryCreate(1920,1080,960,0,960,0,out _), "insets cannot consume drawable width");
Check(!ProportionalViewport.TryCreate(1920,1080,0,-1,0,0,out _), "negative inset rejected");
var asymmetric=ProportionalViewport.Create(1920,1080,40,20,100,70);
Check(asymmetric.OffsetX>=40 && asymmetric.OffsetY>=20 && asymmetric.OffsetX+asymmetric.ContentWidth<=1820 && asymmetric.OffsetY+asymmetric.ContentHeight<=1010, "asymmetric insets preserve content boundary");
Check(GalleryNavigation.Next(3,8,"Down",true)==7, "grid down preserves column");
Check(GalleryNavigation.Next(7,8,"Right",true)==7, "grid boundary bounded");
Check(GalleryNavigation.Next(0,2,"Left",false)==0, "modal focus trapped");
Console.WriteLine("PhotoGallery.App.Tests PASS (FHD/UHD/DCI/8K, optional screen metadata, invalid window/insets, bounded focus)");
