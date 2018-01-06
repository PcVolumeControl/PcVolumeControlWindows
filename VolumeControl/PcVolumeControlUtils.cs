using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace VolumeControl
{
    class PcVolumeControlUtils
    {
        private static DateTime m_lastVersionCheck;

        public static async Task<Octokit.Release> getLatestVersion()
        {
            var github = new Octokit.GitHubClient(new Octokit.ProductHeaderValue("PcVolumeControlWindows"));
            var releases = await github.Repository.Release.GetAll("PcVolumeControl", "PcVolumeControlWindows");
            return releases[0];
        }

        public static async void checkVersion()
        {
            if (m_lastVersionCheck == null || m_lastVersionCheck.AddHours(12).CompareTo(DateTime.Now) < 0)
            {
                Console.Out.WriteLine("Checking GitHub for newer version");

                try
                {
                    var latest = await getLatestVersion();

                    if (!latest.Name.Equals(App.APPLICATION_VERSION, StringComparison.Ordinal))
                    {
                        MessageBox.Show("New version avalible:\nLatest: " + latest.Name + "\nCurrent: " + App.APPLICATION_VERSION + "\n\nPlease download the latest version.", "PcVolumeControl", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Octokit.RateLimitExceededException e)
                {
                    Console.Out.WriteLine("Rate limited by GitHub, backing off...");
                }
                finally
                {
                    m_lastVersionCheck = DateTime.Now;
                }
            }
            else
            {
                Console.Out.WriteLine("Not checking GitHub for newer version. Too soon.");
            }
        }
    }
}
