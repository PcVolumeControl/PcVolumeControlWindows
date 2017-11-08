using CSCore.CoreAudioAPI;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Web.Script.Serialization;
using Newtonsoft.Json;

namespace VolumeControl
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, ClientListener
    {
        PcAudio m_audioState;

        Server m_server;

        public MainWindow()
        {
            InitializeComponent();

            m_server = new Server( this );

            new Thread(() =>
            {
                update(null);

            }).Start();
        }

        private void update( AudioSession sessionUpdate )
        {
            PcAudio audioState = new PcAudio();
            AudioDevice audioDevice = new AudioDevice("default");
            audioState.devices.Add(audioDevice);

            using (var sessionManager = GetDefaultAudioSessionManager2(DataFlow.Render))
            {
                using (var sessionEnumerator = sessionManager.GetSessionEnumerator())
                {
                    foreach (var session in sessionEnumerator)
                    {
                        using (var simpleVolume = session.QueryInterface<SimpleAudioVolume>())
                        using (var audioMeterInformation = session.QueryInterface<AudioMeterInformation>())
                        using (var session2 = session.QueryInterface<AudioSessionControl2>())
                        {
                            var process = Process.GetProcessById(session2.ProcessID);

                            Console.WriteLine(process.ProcessName);
                            Console.WriteLine(audioMeterInformation.PeakValue);

                            if(sessionUpdate != null)
                            {
                                if (sessionUpdate.name.Equals(process.ProcessName, StringComparison.InvariantCultureIgnoreCase))
                                {
                                    Console.WriteLine("Adjusting volume: " + sessionUpdate.name + " - " + sessionUpdate.volume);
                                    simpleVolume.MasterVolume = sessionUpdate.volume;
                                }
                            }

                            AudioSession audioSession = new AudioSession(process.ProcessName, simpleVolume.MasterVolume);
                            audioDevice.sessions.Add(audioSession);
                        }
                    }
                }
            }

            m_audioState = audioState;
        }

        private static AudioSessionManager2 GetDefaultAudioSessionManager2(DataFlow dataFlow)
        {
            using (var enumerator = new MMDeviceEnumerator())
            {
                using (var device = enumerator.GetDefaultAudioEndpoint(dataFlow, CSCore.CoreAudioAPI.Role.Multimedia))
                {
                    Console.WriteLine("DefaultDevice: " + device.FriendlyName);
                    var sessionManager = AudioSessionManager2.FromMMDevice(device);
                    return sessionManager;
                }
            }
        }

        public void onClientMessage(string message)
        {
            Console.WriteLine("client message: " + message);

            if(message != null)
            {
                var audioSession = JsonConvert.DeserializeObject<AudioSession>(message);
                update(audioSession);
            }
        }

        public void onClientConnect()
        {
            if(m_audioState != null)
            {
                update(null);

                var json = JsonConvert.SerializeObject(m_audioState);
                Console.WriteLine("Sending audio state: " + json);
                m_server.sendData(json);
            }
        }
    }
}
