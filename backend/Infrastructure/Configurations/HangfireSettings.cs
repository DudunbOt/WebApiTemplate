namespace Infrastructure.Configurations
{
    public class HangfireSettings
    {
        public int WorkerCount { get; set; } = 5;
        public string[] Queues { get; set; } = ["default"];
        public HangfireDashboardSettings Dashboard { get; set; } = new();
    }

    public class HangfireDashboardSettings
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
