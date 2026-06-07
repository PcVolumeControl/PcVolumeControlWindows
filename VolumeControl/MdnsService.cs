using System;
using System.Net;
using System.Threading.Tasks;
using System.Timers;
using Makaretu.Dns;

namespace VolumeControl
{
    public class MdnsService : IDisposable
    {
        private const string ServiceName = "_pcvolumecontrol._tcp.";
        private MulticastService mdns;
        private ServiceDiscovery serviceDiscovery;
        private ServiceProfile serviceProfile;
        private string instanceName;
        private string hostName;
        private IPAddress address;
        private int port;
        private bool isAnnounced = false;
        private Timer announcementTimer;

        public MdnsService()
        {
            try
            {
                Console.WriteLine("Attempting to create MulticastService...");
                mdns = new MulticastService();
                Console.WriteLine("MulticastService created successfully");
                
                Console.WriteLine("Attempting to create ServiceDiscovery...");
                serviceDiscovery = new ServiceDiscovery(mdns);
                Console.WriteLine("ServiceDiscovery created successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize mDNS service: {ex.Message}");
                Console.WriteLine($"Exception type: {ex.GetType().Name}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                    Console.WriteLine($"Inner exception type: {ex.InnerException.GetType().Name}");
                    if (ex.InnerException.InnerException != null)
                    {
                        Console.WriteLine($"Nested inner exception: {ex.InnerException.InnerException.Message}");
                    }
                }
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                // Set to null to indicate failure
                mdns = null;
                serviceDiscovery = null;
            }
        }

        public async Task<bool> StartAsync(IPAddress serverAddress, int serverPort)
        {
            if (mdns == null || serviceDiscovery == null)
            {
                Console.WriteLine("mDNS service not initialized, skipping announcement");
                return false;
            }

            this.address = serverAddress;
            this.port = serverPort;
            this.hostName = $"{Environment.MachineName}.local.";

            // Generate instance name with random suffix to avoid collisions
            string baseInstanceName = GenerateInstanceName();
            
            // For simplicity, we'll skip the probing phase for now and just use the generated name
            instanceName = baseInstanceName;

            // Create service profile for announcement
            CreateServiceProfile();

            // Start multicast service and announce
            mdns.Start();
            serviceDiscovery.Advertise(serviceProfile);
            isAnnounced = true;

            // Start periodic announcements every 10 seconds
            StartPeriodicAnnouncements();

            Console.WriteLine($"mDNS service announced: {instanceName}");
            return true;
        }

        public void Stop()
        {
            // Stop periodic announcements
            announcementTimer?.Stop();
            announcementTimer?.Dispose();
            announcementTimer = null;

            if (isAnnounced && serviceProfile != null && serviceDiscovery != null)
            {
                serviceDiscovery.Unadvertise(serviceProfile);
                isAnnounced = false;
                Console.WriteLine($"mDNS service withdrawn: {instanceName}");
            }

            mdns?.Stop();
        }

        private string GenerateInstanceName()
        {
            var random = new Random();
            var suffix = random.Next(0, 10000).ToString("D4");
            return $"{Environment.MachineName}-pcvolumecontrol-{suffix}";
        }

        private void CreateServiceProfile()
        {
            serviceProfile = new ServiceProfile(instanceName, ServiceName, (ushort)port);
            serviceProfile.AddProperty("version", "1");
            serviceProfile.AddProperty("protocol", "7");
            
            // Set the target hostname for the service
            serviceProfile.HostName = hostName;
        }

        private void StartPeriodicAnnouncements()
        {
            // Create timer for 10-second announcements
            announcementTimer = new Timer(10000); // 10 seconds
            announcementTimer.Elapsed += OnAnnouncementTimer;
            announcementTimer.AutoReset = true;
            announcementTimer.Enabled = true;
            
            Console.WriteLine("Started periodic mDNS announcements every 30 seconds");
        }

        private void OnAnnouncementTimer(object sender, ElapsedEventArgs e)
        {
            try
            {
                if (isAnnounced && serviceProfile != null && serviceDiscovery != null)
                {
                    // Re-advertise the service (this sends unsolicited announcements)
                    //serviceDiscovery.Unadvertise(serviceProfile);
                    serviceDiscovery.Advertise(serviceProfile);
                    Console.WriteLine($"Sent periodic mDNS announcement for {instanceName}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during periodic mDNS announcement: {ex.Message}");
            }
        }

        public void Dispose()
        {
            Stop();
            serviceDiscovery?.Dispose();
            mdns?.Dispose();
        }
    }
}