namespace ApplicationCore.Interfaces.Base
{
    /// <summary>
    /// Interface for Hangfire background jobs.
    /// Implement this interface and add [RecurringJob] attribute for automatic registration.
    /// </summary>
    public interface IHangfireJob
    {
        /// <summary>
        /// The main execution method called by Hangfire.
        /// </summary>
        Task ExecuteAsync();
    }
}
