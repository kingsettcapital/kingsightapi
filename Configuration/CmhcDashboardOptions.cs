namespace kingsightapi.Configuration
{
    public sealed class CmhcDashboardOptions
    {
        public const string SectionName = "CmhcDashboard";

        /// <summary>Dealpath deal link; <c>{0}</c> is replaced with the DP Deal ID.</summary>
        public string DealpathLinkTemplate { get; set; } = "https://app.dealpath.com/app/deals/{0}";

        /// <summary>Time zone used when <c>asOf</c> is not supplied.</summary>
        public string TimeZoneId { get; set; } = "America/Toronto";
    }
}
