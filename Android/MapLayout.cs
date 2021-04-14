﻿namespace Zebble
{
    using Android.Views;
    using Android.Widget;
    using System;
    using Zebble.AndroidOS;

    class MapLayout : FrameLayout
    {
        View View;

        public MapLayout(View view) : base(Renderer.Context) => View = view;

        public override bool OnInterceptTouchEvent(MotionEvent ev)
        {
            var parentGestureView = this.TraverseUpToFind<AndroidGestureView>();
            if (parentGestureView is not null)
            {
                var originPoint = ev.GetPoint();
                var absolutePoint = originPoint.AbsoluteTo(View);

                var relativeEvent = MotionEvent.Obtain(ev.DownTime, ev.EventTime, ev.Action, absolutePoint.X, absolutePoint.Y, ev.MetaState);
                parentGestureView.OnTouchEvent(relativeEvent);
            }

            return base.OnInterceptTouchEvent(ev);
        }
    }
}