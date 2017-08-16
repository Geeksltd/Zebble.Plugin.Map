namespace Zebble
{
    using CoreLocation;
    using MapKit;

    internal class BasicMapAnnotation : MKAnnotation
    {
        public Map.Annotation View { get; private set; }
        CLLocationCoordinate2D coordinate;

        public BasicMapAnnotation(Map.Annotation view)
        {
            View = view;
            View.Native = this;
            coordinate = new CLLocationCoordinate2D(view.Location.Latitude, view.Location.Longitude);
        }

        public override void SetCoordinate(CLLocationCoordinate2D value) => coordinate = value;
        public override CLLocationCoordinate2D Coordinate => coordinate;
        public override string Title => View.Title;

        public override string Subtitle => View.Subtitle;

        protected override void Dispose(bool disposing) { View = null; base.Dispose(disposing); }
    }
}