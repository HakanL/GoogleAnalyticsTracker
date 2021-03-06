﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Net;
using System.Text;

namespace GoogleAnalyticsTracker
{
    public partial class Tracker
        : IDisposable
    {
        private const string TrackingAccountConfigurationKey = "GoogleAnalyticsTracker.TrackingAccount";
        private const string TrackingDomainConfigurationKey = "GoogleAnalyticsTracker.TrackingDomain";

        const string BeaconUrl = "http://www.google-analytics.com/__utm.gif";
        const string BeaconUrlSsl = "https://ssl.google-analytics.com/_utm.gif";
        const string AnalyticsVersion = "5.3.4"; // Analytics version - AnalyticsVersion

        private readonly UtmeGenerator _utmeGenerator;

        public string TrackingAccount { get; set; } // utmac
        public string TrackingDomain { get; set; }
        public IAnalyticsSession AnalyticsSession { get; set; }
        public string ScreenResolution { get; set; }
        public string ViewPort { get; set; }
        public bool? JavaEnabled { get; set; }

        public string Hostname { get; set; }
        public string Language { get; set; }
        public string UserAgent { get; set; }
        public string CharacterSet { get; set; }

        internal CustomVariable[] CustomVariables { get; set; }

        public CookieContainer CookieContainer { get; set; }

        public bool UseSsl { get; set; }

#if !WINDOWS_PHONE
        public Tracker()
            : this(new AnalyticsSession())
        {
        }

        public Tracker(IAnalyticsSession analyticsSession)
            : this(ConfigurationManager.AppSettings[TrackingAccountConfigurationKey], ConfigurationManager.AppSettings[TrackingDomainConfigurationKey], analyticsSession)
        {
        }

        public Tracker(string trackingAccount, string trackingDomain)
            : this(trackingAccount, trackingDomain, new AnalyticsSession())
        {
        }
#endif

        public Tracker(string trackingAccount, string trackingDomain, IAnalyticsSession analyticsSession)
        {
            TrackingAccount = trackingAccount;
            TrackingDomain = trackingDomain;
            AnalyticsSession = analyticsSession;

#if !WINDOWS_PHONE
            string hostname = Dns.GetHostName().ToLower();
            string osversionstring = Environment.OSVersion.VersionString;
#else
            string hostname = "Windows Phone";
            string osversionstring = "Windows Phone";
#endif
            Hostname = hostname;
            Language = "en";
            UserAgent = string.Format("Tracker/1.0 ({0}; {1}; {2})", Environment.OSVersion.Platform, Environment.OSVersion.Version, osversionstring);
            CookieContainer = new CookieContainer();

            InitializeCharset();

            CustomVariables = new CustomVariable[5];

            _utmeGenerator = new UtmeGenerator(this);
        }

        private void InitializeCharset()
        {
            CharacterSet = "UTF-8";
        }

        private string GenerateUtmn()
        {
            var random = new Random((int)DateTime.UtcNow.Ticks);
            return random.Next(100000000, 999999999).ToString(CultureInfo.InvariantCulture);
        }

        public void SetCustomVariable(int position, string name, string value)
        {
            if (position < 1 || position > 5)
                throw new ArgumentOutOfRangeException(string.Format("position {0} - {1}", position, "Must be between 1 and 5"));

            CustomVariables[position - 1] = new CustomVariable(name, value);
        }

        private void AddStandardParameters(Dictionary<string, string> parameters)
        {
            parameters.Add("utmwv", AnalyticsVersion);
            parameters.Add("utms", AnalyticsSession.GetSessionCount().ToString());
            parameters.Add("utmn", GenerateUtmn());
            parameters.Add("utmhn", Hostname);
            parameters.Add("utmcs", CharacterSet);
            parameters.Add("utmul", Language);
            parameters.Add("utmhid", AnalyticsSession.GenerateSessionId());
            parameters.Add("utmac", TrackingAccount);
            parameters.Add("utmcc", AnalyticsSession.GenerateCookieValue());
            if(!string.IsNullOrEmpty(ScreenResolution))
                parameters.Add("utmsr", ScreenResolution);
            if (!string.IsNullOrEmpty(ViewPort))
                parameters.Add("utmvp", ViewPort);
            if (JavaEnabled.HasValue)
                parameters.Add("utmje", JavaEnabled.Value ? "1" : "0");
        }

        public void TrackPageView(string pageTitle, string pageUrl)
        {
            var parameters = new Dictionary<string, string>();
            AddStandardParameters(parameters);

            parameters.Add("utmdt", pageTitle);
            parameters.Add("utmp", pageUrl);

            var utme = _utmeGenerator.Generate();
            if (!string.IsNullOrEmpty(utme))
                parameters.Add("utme", utme);

            parameters.Add("utmu", "qB~");

            RequestUrlAsync(UseSsl ? BeaconUrlSsl : BeaconUrl, parameters);
        }

        public void TrackUserTiming(string pageTitle, string pageUrl, string category, string variable, TimeSpan time, string label)
        {
            var parameters = new Dictionary<string, string>();
            AddStandardParameters(parameters);

            parameters.Add("utmt", "event");

            var utme = _utmeGenerator.Generate();
            parameters.Add("utme", string.Format("14(90!{0}*{1}*20*{2})(90!{3:F0})", variable, category, label ?? "", time.TotalMilliseconds) + utme);
            parameters.Add("utmdt", pageTitle);
            parameters.Add("utmp", pageUrl);

            parameters.Add("utmu", "qBAAAAAAAAAAAAAAAAAAQ~");

            RequestUrlAsync(UseSsl ? BeaconUrlSsl : BeaconUrl, parameters);
        }

        public void TrackEvent(string category, string action, string label, int value)
        {
            var parameters = new Dictionary<string, string>();
            AddStandardParameters(parameters);

            parameters.Add("utmni", "1");
            parameters.Add("utmt", "event");

            var utme = _utmeGenerator.Generate();
            parameters.Add("utme", string.Format("5({0}*{1}*{2})({3})", category, action, label ?? "", value) + utme);

            parameters.Add("utmu", "6BAAAAAAAAAAAAAAAAAAQ~");

            RequestUrlAsync(UseSsl ? BeaconUrlSsl : BeaconUrl, parameters);
        }

        public void TrackTransaction(string orderId, string storeName, string total, string tax, string shipping, string city, string region, string country)
        {
            var parameters = new Dictionary<string, string>();
            AddStandardParameters(parameters);

            parameters.Add("utmt", "event");
            parameters.Add("utmtid", orderId);
            parameters.Add("utmtst", storeName);
            parameters.Add("utmtto", total);
            parameters.Add("utmttx", tax);
            parameters.Add("utmtsp", shipping);
            parameters.Add("utmtci", city);
            parameters.Add("utmtrg", region);
            parameters.Add("utmtco", country);

            RequestUrlAsync(UseSsl ? BeaconUrlSsl : BeaconUrl, parameters);
        }

        private void RequestUrlAsync(string url, Dictionary<string, string> parameters)
        {
            // Create GET string
            var parameterStrings = new List<string>();
            foreach(var parameter in parameters)
                parameterStrings.Add(string.Format("{0}={1}", parameter.Key, Uri.EscapeDataString(parameter.Value)));

            var data = string.Join("&", parameterStrings.ToArray());

            // Create request
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(string.Format("{0}?{1}", url, data));
            request.CookieContainer = CookieContainer;

#if !WINDOWS_PHONE
            request.Referer = string.Format("http://{0}/", TrackingDomain);
#endif

            request.UserAgent = UserAgent;

            AnalyticsSession.IncSessionCount();

            request.BeginGetResponse(r =>
            {
                try
                {
                    var response = request.EndGetResponse(r);
                    //ignore response
                }
                catch
                {
                    //suppress error
                }
            }, null);
        }


        #region IDisposable Members

        private bool disposed;

        private void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            if (disposing)
            {
                //TODO: Managed cleanup code here, while managed refs still valid
            }
            //TODO: Unmanaged cleanup code here

            disposed = true;
        }

        public void Dispose()
        {
            // Call the private Dispose(bool) helper and indicate 
            // that we are explicitly disposing
            this.Dispose(true);

            // Tell the garbage collector that the object doesn't require any
            // cleanup when collected since Dispose was called explicitly.
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
