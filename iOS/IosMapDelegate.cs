﻿namespace Zebble.Plugin
{
    using System;
    using System.Linq;
    using MapKit;
    using ObjCRuntime;
    using UIKit;

    internal class IosMapDelegate : MKMapViewDelegate
    {
        Map Map;
        object LastTouchedView;

        internal IosMapDelegate(Map map) => Map = map;

        public override MKAnnotationView GetViewForAnnotation(MKMapView mapView, IMKAnnotation annotation)
        {
            if (Runtime.GetNSObject(annotation.Handle) is MKUserLocation userLocationAnnotation)
                return default(MKAnnotationView);

            var pin = (MKPinAnnotationView)mapView.DequeueReusableAnnotation("defaultPin");
            if (pin == null)
                pin = new MKPinAnnotationView(annotation, "defaultPin") { CanShowCallout = true };

            pin.Annotation = annotation;
            AttachGestureToPin(pin, annotation);

            var pinImage = GetPinImage(annotation);
            if (pinImage.HasValue())
            {
                try
                {
                    var provider = Services.ImageService.GetImageProvider(pinImage, new Size(48, 92), Stretch.Fit);
                    Device.UIThread.RunAction(async () => pin.Image = await provider.Result() as UIImage);
                }
                catch (Exception ex)
                {
                    Device.Log.Error("An error happened loading annotation pin image:");
                    Device.Log.Error(ex.Message);
                    Device.Log.Message(ex.StackTrace);
                }
            }

            Device.Log.Warning(pin);
            return pin;
        }

        void AttachGestureToPin(MKPinAnnotationView mapPin, IMKAnnotation annotation)
        {
            var recognizers = mapPin.GestureRecognizers;

            if (recognizers != null)
                foreach (var r in recognizers)
                    mapPin.RemoveGestureRecognizer(r);

            void action(UITapGestureRecognizer g) => OnClick(annotation);

            var recognizer = new UITapGestureRecognizer(action)
            {
                ShouldReceiveTouch = (gestureRecognizer, touch) => { LastTouchedView = touch.View; return true; }
            };

            mapPin.AddGestureRecognizer(recognizer);
        }

        void OnClick(IMKAnnotation nativeAnnotation)
        {
            if (LastTouchedView is MKAnnotationView)
                // Ignore it as it means the callout was shown (which is another view).
                return;

            var annotation = Runtime.GetNSObject(nativeAnnotation.Handle);
            if (annotation == null) return;

            var pin = Map.Annotations.FirstOrDefault(a => a.Native == annotation);
            if (pin != null) pin.Tapped.RaiseOn(Device.ThreadPool, pin);
        }

        string GetPinImage(IMKAnnotation nativeAnnotation)
        {
            var annotation = Runtime.GetNSObject(nativeAnnotation.Handle);
            if (annotation != null)
            {
                var pin = Map.Annotations.FirstOrDefault(a => a.Native == annotation);
                if (pin != null) return pin.IconPath;
            }
            return null;
        }
    }
}