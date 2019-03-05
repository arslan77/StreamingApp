using StreamingApp.Utility;
using Topshelf;

namespace StreamingApp
{
    internal class Program
    {
        private static void Main()
        {
            ConfigureService.Configure();
        }
    }
    internal static class ConfigureService
    {
        internal static void Configure()
        {
            var configuration = new ConfigurationManager();

            HostFactory.Run(configure =>
            {
                configure.StartAutomatically(); // Start the service automatically
                configure.EnableServiceRecovery(src =>
                {
                    src.RestartService(0);
                    src.RestartService(1);
                    // Corresponds to ‘Subsequent failures: Restart the Service’
                    src.RestartService(5);
                });
                configure.Service<VideoStreamingService>(service =>
                {
                    service.ConstructUsing(s => new VideoStreamingService(configuration.Get("VideosPath")));
                    service.WhenStarted(s => s.Start());
                    service.WhenStopped(s => s.Stop());
                });
                configure.RunAsLocalSystem();
                configure.SetServiceName(configuration.Get("ServiceName"));
                configure.SetDisplayName(configuration.Get("DisplayName"));
                configure.SetDescription(configuration.Get("ServiceDescription"));
            });
        }
    }
}
