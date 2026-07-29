using ApplicationCore.Attributes;
using ApplicationCore.Interfaces.Base;
using Hangfire;
using System.Reflection;

namespace Infrastructure.Extensions
{
    public static class HangfireJobExtensions
    {
        /// <summary>
        /// Automatically registers all recurring jobs that implement IHangfireJob
        /// and are decorated with [RecurringJob] attribute.
        /// </summary>
        /// <param name="assembly">The assembly to scan for jobs</param>
        public static void RegisterRecurringJobs(Assembly assembly)
        {
            var jobTypes = assembly.GetTypes()
                .Where(t => typeof(IHangfireJob).IsAssignableFrom(t)
                         && !t.IsInterface
                         && !t.IsAbstract
                         && t.GetCustomAttribute<RecurringJobAttribute>() != null);

            foreach (var jobType in jobTypes)
            {
                var attribute = jobType.GetCustomAttribute<RecurringJobAttribute>()!;
                var timeZone = TimeZoneInfo.FindSystemTimeZoneById(attribute.TimeZone);

                // Use reflection to call the generic AddOrUpdate method
                var method = typeof(HangfireJobExtensions)
                    .GetMethod(nameof(RegisterJob), BindingFlags.NonPublic | BindingFlags.Static)!
                    .MakeGenericMethod(jobType);

                method.Invoke(null, [attribute.JobId, attribute.CronExpression, timeZone, attribute.Queue]);
            }
        }

        private static void RegisterJob<T>(string jobId, string cronExpression, TimeZoneInfo timeZone, string queue)
            where T : IHangfireJob
        {
            RecurringJob.AddOrUpdate<T>(
                jobId,
                queue,
                job => job.ExecuteAsync(),
                cronExpression,
                new RecurringJobOptions { TimeZone = timeZone });
        }
    }
}
