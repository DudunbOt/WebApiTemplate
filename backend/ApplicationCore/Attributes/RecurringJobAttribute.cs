namespace ApplicationCore.Attributes
{
    /// <summary>
    /// Marks a Hangfire job for automatic recurring registration.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class RecurringJobAttribute : Attribute
    {
        /// <summary>
        /// Unique identifier for the recurring job.
        /// </summary>
        public string JobId { get; }

        /// <summary>
        /// Cron expression for the schedule (e.g., "*/5 * * * *" for every 5 minutes).
        /// </summary>
        public string CronExpression { get; }

        /// <summary>
        /// Optional queue name. Defaults to "default".
        /// </summary>
        public string Queue { get; set; } = "default";

        /// <summary>
        /// Optional timezone ID (e.g., "UTC", "America/New_York"). Defaults to UTC.
        /// </summary>
        public string TimeZone { get; set; } = "UTC";

        public RecurringJobAttribute(string jobId, string cronExpression)
        {
            JobId = jobId;
            CronExpression = cronExpression;
        }
    }
}
