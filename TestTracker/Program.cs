using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TestTracker
{
    class Program
    {
        private static readonly GoogleAnalyticsTracker.Tracker tracker = new GoogleAnalyticsTracker.Tracker("UA-33268885-2", "TestApp");

        public static GoogleAnalyticsTracker.Tracker Tracker
        {
            get { return tracker; }
        }

        static void Main(string[] args)
        {
            Program.Tracker.ScreenResolution = string.Format("{0}x{1}", 1024, 768);

            Program.Tracker.TrackPageView("Start Up", "/main");

            Program.Tracker.TrackEvent("TheCat", "Do It", "Label1", 50);

            Program.Tracker.TrackUserTiming("Start Up", "/main", "TimingCat", "MainTime", TimeSpan.FromMilliseconds(120), "Label2");

            System.Threading.Thread.Sleep(1000);
        }
    }
}
