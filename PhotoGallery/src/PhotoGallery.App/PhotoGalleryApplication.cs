using System.Text.Json;
using ActionExamples.ViewAnnotations;
using PhotoGallery.ActionProvider;
using PhotoGallery.Domain;
using PhotoGallery.Persistence;
using PhotoGallery.UseCases;
using PhotoGallery.ViewActionProvider;
using Tizen.NUI;
using Tizen.NUI.BaseComponents;
using Tizen.System;
using NuiButton = Tizen.NUI.Components.Button;

namespace PhotoGallery.App;

// All geometry is in the 1920 x 1080 design canvas. Only _canvas applies display scaling.
internal sealed class PhotoGalleryApplication : NUIApplication
{
    private GalleryLibraryService? _service;
    private SynchronizationContext? _ui;
    private View? _root, _canvas, _activeRoot;
    private GalleryDisplayMetrics? _display;
    private Tizen.NUI.Timer? _annotations, _slides;
    private bool _paused, _busy, _searching;
    private string _tab = "Pictures", _album = "", _query = "", _error = "", _modal = "", _returnFocus = "", _importPath = "";
    private int _page;
    private string _pendingFocus = "";
    private string _queryDraft = "";
    private readonly List<(View View, string PhotoId)> _photos = [];
    private readonly List<View> _controls = [];
    private PhotoRecord? CurrentPhoto { get { try { return _service?.Current(); } catch (InvalidOperationException) { return null; } } }

    protected override void OnCreate()
    {
        base.OnCreate();
        _ui = SynchronizationContext.Current;
        var data = Tizen.Applications.Application.Current.DirectoryInfo.Data;
        var storages = StorageManager.Storages.ToArray();
        var internalStorage = storages.First(x => x.StorageType == StorageArea.Internal);
        var library = new MediaContentPhotoLibrary(data, internalStorage.GetAbsolutePath(DirectoryType.Images), storages.Where(x => x.StorageType == StorageArea.Internal || x.DeviceType == StorageDevice.ExternalUSBMassStorage).Select(x => (x.RootDirectory, x.StorageType == StorageArea.Internal ? "internal" : "usb")));
        _service = new GalleryLibraryService(library);
        _service.Changed += OnLibraryChanged;
        PhotoGalleryActionProviderHost.Start(_service);
        PhotoGalleryViewActionProviderHost.Start();
        _annotations = new Tizen.NUI.Timer(50);
        _annotations.Tick += (_, _) => { PublishAnnotations(); return false; };
        _slides = new Tizen.NUI.Timer(3000);
        _slides.Tick += (_, _) =>
        {
            if (!_paused && !_busy && _modal.Length == 0 && _service.Slideshow) _service.Advance(1);
            return true;
        };
        Window.Default.KeyEvent += OnKey;
        Window.Default.Resized += OnResize;
        Window.Default.InsetsChanged += OnResize;
        Window.Default.Moved += OnMove;
        FocusManager.Instance.FocusChanged += OnFocus;
        RunCommand(() => _service.RefreshAsync(CancellationToken.None));
    }
    protected override void OnPause()
    {
        _paused = true; _annotations?.Stop(); _slides?.Stop();
        PhotoGalleryViewActionProviderHost.ClearPublishedViews(); base.OnPause();
    }
    protected override void OnResume() { base.OnResume(); _paused = false; Render(); }
    protected override void OnTerminate()
    {
        _paused = true;
        if (_service is not null) _service.Changed -= OnLibraryChanged;
        _annotations?.Dispose(); _slides?.Dispose();
        Window.Default.KeyEvent -= OnKey; Window.Default.Resized -= OnResize; Window.Default.InsetsChanged -= OnResize; Window.Default.Moved -= OnMove;
        FocusManager.Instance.FocusChanged -= OnFocus;
        PhotoGalleryViewActionProviderHost.ClearPublishedViews(); base.OnTerminate();
    }
    private void OnLibraryChanged() => _ui?.Post(_ => Render(), null);
    private void OnFocus(object? sender, FocusManager.FocusChangedEventArgs args) => QueueAnnotations();
    private void OnMove(object? sender, EventArgs args) => QueueAnnotations();
    private void OnResize(object? sender, EventArgs args)
    {
        if (_paused || !ReadDisplay(out var d)) return;
        if (_root is null || _canvas is null) { Render(); return; }
        _root.Size = new Size(d.WindowWidth, d.WindowHeight);
        _canvas.Position = new Position(d.Viewport.OffsetX, d.Viewport.OffsetY);
        _canvas.Scale = new Vector3(d.Viewport.Scale, d.Viewport.Scale, 1);
        QueueAnnotations();
    }
    private bool ReadDisplay(out GalleryDisplayMetrics display)
    {
        display = default;
        int width = 0, height = 0;
        try
        {
            var hasWidth = Information.TryGetValue("http://tizen.org/feature/screen.width", out width);
            var hasHeight = Information.TryGetValue("http://tizen.org/feature/screen.height", out height);
            if (!hasWidth || !hasHeight || width <= 0 || height <= 0) width = height = 0;
        }
        catch (Exception ex)
        {
            width = height = 0;
            Tizen.Log.Warn("PhotoGallery", "Screen size unavailable: " + ex.GetType().Name);
        }
        try
        {
            var size = Window.Default.WindowSize; var insets = Window.Default.GetInsets();
            if (!GalleryDisplayMetrics.TryCreate(size.Width,size.Height,width,height,insets.Start,insets.Top,insets.End,insets.Bottom,out display)) return false;
            if (_display != display)
            {
                _display = display;
                Tizen.Log.Info("PhotoGallery", $"Screen={width}x{height} Window={size.Width}x{size.Height} Scale={display.Viewport.Scale} Offset={display.Viewport.OffsetX},{display.Viewport.OffsetY}");
            }
            return true;
        }
        catch (Exception ex) { Tizen.Log.Warn("PhotoGallery", "Display unavailable: " + ex.GetType().Name); return false; }
    }
    private async void RunCommand(Func<Task> action, Action? after = null)
    {
        if (_busy) return;
        _busy = true; _error = ""; Render();
        try { await action(); after?.Invoke(); }
        catch (Exception ex)
        {
            _error = ex is ArgumentException or InvalidOperationException ? ex.Message.Split('\n')[0] : "Photos are unavailable. Check storage access and refresh.";
            Tizen.Log.Warn("PhotoGallery", "Media operation: " + ex.GetType().Name);
        }
        finally { _busy = false; Render(); }
    }
    private void Render()
    {
        if (_paused || _service is null || !ReadDisplay(out var d)) return;
        var previousFocus = FocusManager.Instance.GetCurrentFocusView()?.Name;
        _activeRoot = null; PhotoGalleryViewActionProviderHost.ClearPublishedViews();
        if (_root is not null) { Window.Default.GetDefaultLayer().Remove(_root); _root.Dispose(); }
        _controls.Clear(); _photos.Clear();
        _root = Surface("GalleryWindow",0,0,d.WindowWidth,d.WindowHeight,"#f7f7f8");
        _canvas = Surface("GalleryCanvas",0,0,1920,1080,CurrentPhoto is null ? "#f7f7f8" : "#111111");
        _canvas.ParentOrigin = ParentOrigin.TopLeft; _canvas.PivotPoint = PivotPoint.TopLeft;
        _canvas.Position = new Position(d.Viewport.OffsetX,d.Viewport.OffsetY);
        _canvas.Scale = new Vector3(d.Viewport.Scale,d.Viewport.Scale,1); _root.Add(_canvas);
        if (CurrentPhoto is { } photo) DrawViewer(_canvas,photo); else DrawLibrary(_canvas);
        _activeRoot = _canvas;
        if (_modal.Length > 0) DrawModal(_canvas);
        Window.Default.GetDefaultLayer().Add(_root);
        NuiViewAnnotations.Observe(_root,QueueAnnotations);
        if (_modal.Length > 0) Focus("ModalCancel");
        else if (!Focus(_pendingFocus) && !Focus(previousFocus ?? ""))
        {
            if (CurrentPhoto is not null) Focus("ViewerBack");
            else if (_photos.Count > 0) Focus(_photos[0].View.Name);
            else Focus("Import");
        }
        _pendingFocus = "";
        if (_service.Slideshow && !_paused && _modal.Length == 0) _slides?.Start(); else _slides?.Stop();
        QueueAnnotations();
    }
    private void DrawLibrary(View root)
    {
        root.Add(Label("Gallery",42,64,38,800,70));
        AddButton(root,"Search","Search",1330,50,160,64,()=> { _searching=true; _queryDraft=_query; _page=0; Render(); Focus("SearchInput"); });
        AddButton(root,"Import","Import",1506,50,160,64,()=>OpenModal("import","Import"));
        AddButton(root,"Refresh",_busy?"Loading…":"Refresh",1682,50,174,64,()=>RunCommand(()=>_service!.RefreshAsync(CancellationToken.None)));
        if (_searching)
        {
            var field=Field("SearchInput",_queryDraft,"Search names, albums or dates",64,160,1300,66,256);
            field.TextChanged+=(_,_)=>_queryDraft=field.Text;
            root.Add(field);_controls.Add(field);
            AddButton(root,"SearchApply","Search",1380,160,220,66,()=>{_query=_queryDraft;_page=0;_pendingFocus="SearchApply";Render();});
            AddButton(root,"SearchClose","Close",1616,160,240,66,()=>{_searching=false;_query="";_page=0;Render();});
        }
        else root.Add(Label(_album.Length>0?_album:_tab,60,64,145,1700,84));
        var source=_service!.Snapshot.Where(p=> _tab!="Favorites" || p.Favorite)
            .Where(p=>_album.Length==0 || p.Album==_album)
            .Where(p=>_query.Length==0 || p.Title.Contains(_query,StringComparison.OrdinalIgnoreCase) || p.Album.Contains(_query,StringComparison.OrdinalIgnoreCase) || p.CapturedAt.ToString("yyyy-MM-dd").Contains(_query,StringComparison.Ordinal)).ToArray();
        var albums=_tab=="Albums" && _album.Length==0 && !_searching;
        var items=albums?source.GroupBy(p=>p.Album).Select(g=>g.First()).ToArray():source;
        _page=Math.Clamp(_page,0,Math.Max(0,(items.Length-1)/8));
        root.Add(Label(_busy?"Loading pictures…":$"{items.Length} {(albums?"albums":"pictures")}",24,64,227,1600,40,"#717175"));
        if (_error.Length>0 || !_service.Ready || items.Length==0)
        {
            var emptyTitle=Label(_error.Length>0?"Photos are unavailable":_busy?"Loading pictures…":_query.Length>0?"No results":"No pictures yet",44,240,435,1440,90);emptyTitle.HorizontalAlignment=HorizontalAlignment.Center;root.Add(emptyTitle);
            var emptyBody=Label(_error.Length>0?_error:_query.Length>0?"Try another name or return to Pictures.":"Import a photo to get started.",28,240,535,1440,140,"#717175");emptyBody.HorizontalAlignment=HorizontalAlignment.Center;root.Add(emptyBody);
        }
        else
        {
            var visible=items.Skip(_page*8).Take(8).ToArray();
            for(var i=0;i<visible.Length;i++)
            {
                var p=visible[i]; var x=64+(i%4)*453; var y=278+(i/4)*310;
                var b=AddButton(root,"Photo-"+p.Id,"",x,y,433,284,()=>
                {
                    if(albums){_album=p.Album;_page=0;Render();} else _service.Show(p.Id);
                },"#f7f7f8");
                b.AccessibilityName=albums?p.Album:p.Title;
                b.Add(new ImageView { Name="Thumbnail",ExcludeLayouting=true,ResourceUrl=p.Path,Position=new Position(0,0),Size=new Size(433,230),FittingMode=FittingModeType.ScaleToFill,CornerRadius=12 });
                var caption=Label(albums?p.Album:(p.Favorite?"♥ ":"")+p.Title,25,0,236,433,42);caption.ExcludeLayouting=true;b.Add(caption);
                _photos.Add((b,p.Id));
            }
            var prev=AddButton(root,"PagePrevious","Previous",1536,909,120,54,()=>{_page--;Render();});prev.IsEnabled=_page>0;
            root.Add(Label($"{_page+1} / {Math.Max(1,(items.Length+7)/8)}",24,1664,909,64,54));
            var next=AddButton(root,"PageNext","Next",1736,909,120,54,()=>{_page++;Render();});next.IsEnabled=(_page+1)*8<items.Length;
        }
        var tabs=new[]{"Pictures","Albums","Favorites"};
        for(var i=0;i<tabs.Length;i++)
        {
            var tab=tabs[i];var b=AddButton(root,"Tab"+tab,tab,650+i*165,971,150,62,()=>{_tab=tab;_album="";_query="";_searching=false;_page=0;Render();},"#f7f7f8");
            b.TextLabel.PixelSize=30;
            if(_tab==tab){var line=Surface("Selected",8,57,134,4,"#222222");line.ExcludeLayouting=true;b.Add(line);}
        }
    }
    private void DrawViewer(View root,PhotoRecord p)
    {
        AddButton(root,"ViewerBack","Back",64,48,130,64,()=>{_pendingFocus="Photo-"+p.Id;_service!.CloseViewer();},"#292929",true);
        root.Add(Label(p.Title,32,234,48,1580,64,"#ffffff"));
        var image=new ImageView { Name="DetailPhoto",AccessibilityName=p.Title,ResourceUrl=p.Path,Position=new Position(140,150),Size=new Size(1640,690),FittingMode=FittingModeType.ShrinkToFit };
        root.Add(image);_photos.Add((image,p.Id));
        var entries=new (string Name,string Text,Action Click)[]
        {
            ("ViewerPrevious","Previous",()=>_service!.Advance(-1)), ("ViewerNext","Next",()=>_service!.Advance(1)),
            ("ViewerFavorite",p.Favorite?"♥ Favorite":"♡ Favorite",()=>RunCommand(()=>_service!.SetFavoriteAsync(p.Id,!p.Favorite,CancellationToken.None))),
            ("ViewerInfo","Info",()=>OpenModal("info","ViewerInfo")),
            ("ViewerSlideshow",_service!.Slideshow?"Stop":"Slideshow",()=>{if(_service.Slideshow)_service.StopSlideshow();else _service.StartSlideshow();}),
            ("ViewerDelete","Delete",()=>OpenModal("delete","ViewerDelete")),
        };
        for(var i=0;i<entries.Length;i++)
        {
            var e=entries[i];var b=AddButton(root,e.Name,e.Text,368+i*196,889,180,64,e.Click,"#292929",true);
            if(e.Name=="ViewerDelete") {b.TextColor=new Color("#ff7474");b.IsEnabled=p.Owned;}
        }
        if(_error.Length>0)root.Add(Label(_error,25,234,985,1450,65,"#ffb5b5"));
    }
    private void OpenModal(string modal,string returnFocus)
    { _modal=modal;_error="";_returnFocus=returnFocus;_slides?.Stop();Render(); }
    private void CloseModal()
    { _modal="";_error="";Render();Focus(_returnFocus); }
    private void DrawModal(View root)
    {
        foreach(var v in _controls){v.Focusable=false;v.FocusableChildren=false;}
        root.Add(Surface("ModalShade",0,0,1920,1080,"#00000088"));
        var panel=Surface("Modal",520,240,880,570,"#ffffff",28);root.Add(panel);_activeRoot=panel;
        var p=CurrentPhoto;
        panel.Add(Label(_modal=="delete"?"Delete photo?":_modal=="info"?"Details":"Import a photo",44,44,32,792,86));
        var body=_modal=="delete"?$"{p?.Title}\nThis removes the copy imported into Gallery.":_modal=="info"?$"{p?.Title}\nAlbum: {p?.Album}\nDate: {p?.CapturedAt:yyyy-MM-dd}\nFavorite: {(p?.Favorite==true?"Yes":"No")}\n{(p?.Owned==true?"Imported photo":"Device library photo")}":"Enter the path of a JPEG or PNG image.\nGallery keeps the original photo.";
        var message=Label(body,28,44,120,792,_modal=="info"?275:160);message.VerticalAlignment=VerticalAlignment.Top;panel.Add(message);
        if(_modal=="import")
        {
            var field=Field("ImportPath",_importPath,"Image file path",44,280,792,66,2048);
            field.TextChanged+=(_,_)=>_importPath=field.Text;panel.Add(field);_controls.Add(field);
        }
        if(_error.Length>0)panel.Add(Label(_error,24,44,350,792,98,"#bd2424"));
        AddButton(panel,"ModalCancel",_modal=="info"?"Close":"Cancel",456,466,180,64,CloseModal);
        if(_modal=="delete")
            AddButton(panel,"ModalConfirm",_busy?"Deleting…":"Delete",656,466,180,64,()=>RunCommand(()=>_service!.DeleteAsync(p?.Id??"",CancellationToken.None),()=>{_modal="";_service!.CloseViewer();})).TextColor=new Color("#c62222");
        if(_modal=="import")
            AddButton(panel,"ModalConfirm",_busy?"Importing…":"Import",656,466,180,64,()=>RunCommand(()=>_service!.ImportAsync(_importPath,"",CancellationToken.None),()=>{_modal="";_tab="Pictures";_album="";_query="";_page=0;_importPath="";}));
    }
    private void OnKey(object? sender,Window.KeyEventArgs e)
    {
        if(e.Key.State!=Key.StateType.Down)return;
        var key=e.Key.KeyPressedName;
        if(key is "XF86Back" or "Escape")
        {
            if(_busy)return;
            if(_modal.Length>0)CloseModal();
            else if(CurrentPhoto is not null){var id=CurrentPhoto.Id;_service!.CloseViewer();_pendingFocus="Photo-"+id;}
            else if(_searching){_query="";_searching=false;Render();}
            else if(_album.Length>0){_album="";_page=0;Render();}
            else Exit();
            return;
        }
        if(key is not ("Left" or "Right" or "Up" or "Down" or "Tab"))return;
        var current=FocusManager.Instance.GetCurrentFocusView();
        if(current is TextField && key is "Left" or "Right")return;
        var controls=_controls.Where(v=>v.Focusable&&v.IsEnabled&&_activeRoot is not null&&NuiViewAnnotations.BelongsTo(v,_activeRoot)).ToArray();
        var index=Array.IndexOf(controls,current);
        var grid=current?.Name.StartsWith("Photo-",StringComparison.Ordinal)==true;
        var next=GalleryNavigation.Next(index,controls.Length,key,grid);
        if(next>=0)Focus(controls[next].Name);
    }
    private bool Focus(string name)
    {
        var view=_controls.FirstOrDefault(x=>x.Name==name&&x.Focusable&&x.IsEnabled&&_activeRoot is not null&&NuiViewAnnotations.BelongsTo(x,_activeRoot));
        if(view is null)return false;
        foreach(var field in _controls.OfType<TextField>())if(field!=view)field.KeyInputFocus = false;
        FocusManager.Instance.SetCurrentFocusView(view);return true;
    }
    private NuiButton AddButton(View root,string name,string text,float x,float y,float width,float height,Action clicked,string color="#ededf0",bool light=false)
    {
        var b=new NuiButton {Name=name,AccessibilityName=text,Text=text,Position=new Position(x,y),Size=new Size(width,height),BackgroundColor=new Color(color),TextColor=new Color(light?"#ffffff":"#222222"),CornerRadius=18,Focusable=true};
        b.TextLabel.PixelSize=28;
        b.FocusGained+=(_,_)=>{b.BorderlineWidth=4;b.BorderlineColor=new Color(light?"#ffffff":"#2265c9");b.Scale=new Vector3(1.015f,1.015f,1);};
        b.FocusLost+=(_,_)=>{b.BorderlineWidth=0;b.Scale=Vector3.One;};
        b.Clicked+=(_,_)=>{if(!_busy&&_activeRoot is not null&&NuiViewAnnotations.BelongsTo(b,_activeRoot))clicked();};
        root.Add(b);_controls.Add(b);return b;
    }
    private static View Surface(string name,float x,float y,float w,float h,string color,float radius=0)=>new(){Name=name,Position=new Position(x,y),Size=new Size(w,h),BackgroundColor=new Color(color),CornerRadius=radius,FocusableChildren=true};
    private static TextLabel Label(string text,float size,float x,float y,float w,float h,string color="#222222")=>new(){Text=text,PixelSize=size,FontStyle=new PropertyMap().Add("weight",new PropertyValue(size>=42?"bold":"regular")),Position=new Position(x,y),Size=new Size(w,h),TextColor=new Color(color),MultiLine=true,VerticalAlignment=VerticalAlignment.Center,Ellipsis=true};
    private static TextField Field(string name,string text,string hint,float x,float y,float w,float h,int limit)=>new(){Name=name,AccessibilityName=hint,Text=text,PlaceholderText=hint,EnableEditing=true,Focusable=true,PixelSize=28,Position=new Position(x,y),Size=new Size(w,h),BackgroundColor=Color.White,MaxLength=limit};
    private void QueueAnnotations()
    { _annotations?.Stop();if(_paused||_activeRoot is null)return;PublishAnnotations();_annotations?.Start(); }
    private void PublishAnnotations()
    {
        if(_paused||_activeRoot is null||_service is null)return;
        var current=CurrentPhoto;
        var screen=_modal.Length>0?_modal:current is not null?"detail":_searching?"search":_tab.ToLowerInvariant();
        var pageId="gallery:"+screen+(current is null?"":":"+current.Id);
        var page=new {screen,tab=_tab,album=_album,query=_query,page=_page+1,loading=_busy,error=_error,photoId=current?.Id??"",slideshow=_service.Slideshow};
        var snapshots=new List<CurrentViewSnapshot>();var captured=new HashSet<View>();var focused=NuiViewAnnotations.Focused(_activeRoot);
        void Capture(View v,CurrentViewSnapshot s){var measured=NuiViewAnnotations.Measure(v,_activeRoot,focused,s);if(measured is not null){snapshots.Add(measured);captured.Add(v);}}
        CurrentViewSnapshot Context(string id,string description,object state)
        {
            var entity=new RPCPort.PhotoGalleryActionProvider.TizenEntity {Id=pageId,Extra=JsonSerializer.Serialize(state)};
            return new(id,"PhotoGallery.Control",description,"Tizen.Entity",pageId,entity.ToJson());
        }
        Capture(_activeRoot,Context(pageId,"Gallery "+screen,new{schemaVersion=1,page}));
        if(_modal.Length==0)
            foreach(var(v,id) in _photos)
            {
                var p=_service.Snapshot.FirstOrDefault(x=>x.Id==id);if(p is null)continue;
                Capture(v,new CurrentViewSnapshot(pageId+":photo:"+id,"PhotoGallery.Photo",p.Title,"Tizen.Entity.Photo",p.Id,PhotoGalleryService.ToEntity(p).ToJson()));
            }
        foreach(var(v,key) in NuiViewAnnotations.Controls(_activeRoot))
        {
            if(captured.Contains(v))continue;
            // Import path is a user draft, never disclose it through View annotations.
            var draft=v.Name=="ImportPath"?(object)new{control="ImportPath",text=""}:NuiViewAnnotations.ControlState(v,key);
            Capture(v,Context(pageId+":control:"+key,NuiViewAnnotations.Description(v,key),new{schemaVersion=1,page,draft}));
        }
        PhotoGalleryViewActionProviderHost.Publish(snapshots);
    }
    private static void Main(string[] args)=>new PhotoGalleryApplication().Run(args);
}
