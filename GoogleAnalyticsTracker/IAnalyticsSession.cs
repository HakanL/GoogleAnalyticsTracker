namespace GoogleAnalyticsTracker
{
    public interface IAnalyticsSession
    {
        string GenerateSessionId();
        string GenerateCookieValue();
        int GetSessionCount();
        void IncSessionCount();
    }
}