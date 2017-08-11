namespace Zebble.Plugin.Renderer
{
    using System;
    using System.ComponentModel;
    using System.Threading.Tasks;
    using Android.Gms.Maps;
    using Android.Gms.Maps.Model;
    using Android.Widget;
    using Zebble;
    using static Zebble.Plugin.Map;

    [EditorBrowsable(EditorBrowsableState.Never)]
    internal class MapRenderer : INativeRenderer
    {
        Map View;
        FrameLayout Container; // The map will be drawn onto this after the page is rendered.
        MapFragment Fragment;
        GoogleMap Map;
        const double DEGREE360 = 360;

        public async Task<Android.Views.View> Render(Renderer renderer)
        {
            View = (Map)renderer.View;

            View.ZoomEnabledChanged.HandleOn(Device.UIThread,
                () => Map.UiSettings.ZoomControlsEnabled = View.ZoomEnable);

            View.ScrollEnabledChanged.HandleOn(Device.UIThread,
                () => Map.UiSettings.ScrollGesturesEnabled = View.ScrollEnabled);

            View.ApiZoomChanged.HandleOn(Device.UIThread,
                () => Map.AnimateCamera(CameraUpdateFactory.ZoomBy(View.ZoomLevel)));

            View.AnnotationsChanged.HandleOn(Device.UIThread, UpdateAnnotations);
            View.NativeRefreshControl = MoveToRegion;

            Container = new FrameLayout(Renderer.Context) { Id = Android.Views.View.GenerateViewId() };

            await View.WhenShown(() => Device.UIThread.Run(LoadMap));
            return Container;
        }

        Task FixThread() => Task.Delay(Animation.OneFrame);

        async Task LoadMap()
        {
            await Task.Delay(Animation.OneFrame);

            Fragment = CreateFragment(Container, View.RenderOptions());

            await Task.Delay(Animation.OneFrame); // Wait for the fragment to be created.

            await CreateMap();

            Device.UIThread.RunAction(async () => await UpdateAnnotations());

            await Task.CompletedTask;
        }

        MapFragment CreateFragment(FrameLayout view, GoogleMapOptions options)
        {
            var fragment = MapFragment.NewInstance(options);
            var transaction = UIRuntime.CurrentActivity.FragmentManager.BeginTransaction();
            view.Id = Android.Views.View.GenerateViewId();
            transaction.Add(view.Id, fragment);
            transaction.Commit();
            return fragment;
        }

        void Map_CameraChange(object _, GoogleMap.CameraChangeEventArgs args) => UpdateVisibleRegion();

        async Task UpdateAnnotations()
        {
            foreach (var annotation in View.Annotations)
            {
                var markerOptions = new MarkerOptions();
                markerOptions.SetPosition(annotation.Location.Render());
                markerOptions.SetTitle(annotation.Title.OrEmpty());
                markerOptions.SetSnippet(annotation.Content.OrEmpty());
                if (annotation.Flat) markerOptions.Flat(annotation.Flat);
                if (annotation.IconPath.HasValue())
                {
                    var provider = await annotation.GetPinImageProvider();
                    var image = await provider.Result() as Android.Graphics.Bitmap;
                    markerOptions.SetIcon(BitmapDescriptorFactory.FromBitmap(image));
                }

                var marker = Map.AddMarker(markerOptions);
                marker.Tag = new AnnotationRef(annotation);
            }
        }

        async Task MoveToRegion()
        {
            var update = CameraUpdateFactory.NewCameraPosition(
                CameraPosition.FromLatLngZoom((await View.GetCenter()).Render(),
                View.ZoomLevel));

            try
            {
                Device.UIThread.RunAction(() => Map?.AnimateCamera(update));
            }
            catch (Java.Lang.IllegalStateException exc)
            {
                Device.Log.Error("MoveToRegion exception: " + exc);
                Device.Log.Warning($"Zebble AndroidMapView MoveToRegion exception: {exc}");
            }
        }

        void UpdateVisibleRegion()
        {
            var map = Map;
            if (map == null) return;

            var projection = map.Projection;
            var width = Fragment.View.Width;
            var height = Fragment.View.Height;
            var topLeft = projection.FromScreenLocation(new Android.Graphics.Point(0, 0));
            var bottomLeft = projection.FromScreenLocation(new Android.Graphics.Point(0, height));
            var bottomRight = projection.FromScreenLocation(new Android.Graphics.Point(width, height));
            View.VisibleRegion = new Span(topLeft.ToZebble(), bottomLeft.ToZebble(), bottomRight.ToZebble());
        }

        void OnApiZoomChanged() => Map.AnimateCamera(CameraUpdateFactory.ZoomBy(View.ZoomLevel));

        async Task CreateMap()
        {
            var source = new TaskCompletionSource<GoogleMap>();
            Fragment.GetMapAsync(new MapReadyCallBack(source.SetResult));

            //await FixThread();
            Map = await source.Task;
            // await FixThread();

            Map.CameraChange += Map_CameraChange;
            Map.MarkerClick += Map_MarkerClick;
            Map.InfoWindowClick += Map_InfoWindowClick;

            Map.AnimateCamera(CameraUpdateFactory.NewLatLngZoom(View.Center.Render(), View.ZoomLevel));
        }

        void Map_InfoWindowClick(object _, GoogleMap.InfoWindowClickEventArgs e) => RaiseTapped(e.Marker);

        void Map_MarkerClick(object _, GoogleMap.MarkerClickEventArgs e) => RaiseTapped(e.Marker);

        void RaiseTapped(Marker marker)
        {
            var annotation = (marker?.Tag as AnnotationRef)?.Annotation;
            if (annotation == null)
                Device.Log.Error("No map annotation was found for the tapped annotation!");
            else annotation.RaiseTapped();
        }

        public void Dispose()
        {
            Map.Perform(m => m.CameraChange -= Map_CameraChange);
            Map.Perform(m => m.InfoWindowClick -= Map_InfoWindowClick);
            Map.Perform(m => m.MarkerClick -= Map_MarkerClick);
            Map = null;
            Fragment?.Dispose();
            Fragment = null;
            View = null;
            Container?.Dispose();
            Container = null;
        }
    }
}