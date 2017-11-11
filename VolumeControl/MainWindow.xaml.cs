using CSCore.CoreAudioAPI;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Web.Script.Serialization;
using Newtonsoft.Json;
using CSCore.Win32;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;

namespace VolumeControl
{
    public partial class MainWindow : Window, ClientListener
    {
        MMDeviceEnumerator m_deviceEnumerator;

        AudioSessionManager2 m_audioManager;

        EndpointCallback m_endpointCallback;
        AudioSessionNotificationListener m_audioSessionListener;

        PcAudio m_audioState;

        Server m_server;

        static Object m_lock = new Object();

        bool m_updating = false;

        Dictionary<int, AudioSessionKeeper> m_sessions = new Dictionary<int, AudioSessionKeeper>();

        private string m_defaultDeviceId;

        public MainWindow()
        {
            InitializeComponent();

            m_endpointCallback = new EndpointCallback(this);

            string ipAddress = GetLocalIPAddress();
            server_ip.Content = ipAddress;
            Console.WriteLine("ipAddress: " + ipAddress);

            updateConnectionStatus();

            m_audioSessionListener = new AudioSessionNotificationListener(this);

            m_deviceEnumerator = new MMDeviceEnumerator();
            m_deviceEnumerator.RegisterEndpointNotificationCallback(m_endpointCallback);

            new Thread(() =>
            {
                updateDefaultAudioDevice();

                update(null);
            }).Start();
        }

        private void updateDefaultAudioDevice()
        {
            if(m_audioManager != null)
            {
                m_audioManager.UnregisterSessionNotificationNative(m_audioSessionListener);
            }

            m_audioManager = GetDefaultAudioSessionManager2(DataFlow.Render);
            m_audioManager.RegisterSessionNotificationNative(m_audioSessionListener);

            m_defaultDeviceId = GetDefaultAudioDevice().DeviceID;
        }

        private void updateConnectionStatus()
        {
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                if (m_server == null || !m_server.isRunning())
                {
                    server_status.Content = "Offline";
                    start_button.IsEnabled = true;
                    stop_button.IsEnabled = false;
                    server_port.IsEnabled = true;
                }
                else
                {
                    server_status.Content = "Online";
                    start_button.IsEnabled = false;
                    stop_button.IsEnabled = true;
                    server_port.IsEnabled = false;
                }
            }));
        }

        public static string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            throw new Exception("No network adapters with an IPv4 address in the system!");
        }

        private class AudioSessionNotificationListener : IAudioSessionNotification
        {
            private MainWindow m_mainWindow;

            public AudioSessionNotificationListener(MainWindow mainWindow)
            {
                m_mainWindow = mainWindow;
            }

            public int OnSessionCreated(IntPtr newSession)
            {
                Console.WriteLine("OnSessionCreated");
                m_mainWindow.updateAndDispatchAudioState();
                return 0;
            }
        }

        private class EndpointCallback : IMMNotificationClient
        {
            private MainWindow m_mainWindow;

            public EndpointCallback(MainWindow mainWindow)
            {
                m_mainWindow = mainWindow;
            }

            void IMMNotificationClient.OnDefaultDeviceChanged(DataFlow dataFlow, Role role, string deviceId)
            {
                if (!deviceId.Equals(m_mainWindow.m_defaultDeviceId))
                {
                    m_mainWindow.m_defaultDeviceId = deviceId;

                    Console.WriteLine("OnDefaultDeviceChanged: " + deviceId);

                    m_mainWindow.updateDefaultAudioDevice();
                    m_mainWindow.m_sessions.Clear();
                    m_mainWindow.updateAndDispatchAudioState();
                }
            }

            void IMMNotificationClient.OnDeviceAdded(string deviceId)
            {
                Console.WriteLine("OnDeviceAdded: " + deviceId);
            }

            void IMMNotificationClient.OnDeviceRemoved(string deviceId)
            {
                Console.WriteLine("OnDeviceRemoved: " + deviceId);
                m_mainWindow.updateDefaultAudioDevice();
            }

            void IMMNotificationClient.OnDeviceStateChanged(string deviceId, DeviceState deviceState)
            {
                Console.WriteLine("OnDeviceStateChanged: " + deviceId);
            }

            void IMMNotificationClient.OnPropertyValueChanged(string deviceId, PropertyKey key)
            {
                if (deviceId.Equals(m_mainWindow.m_defaultDeviceId))
                {
                    Console.WriteLine("OnPropertyValueChanged: " + deviceId + " Property: " + key);
                }
            }
        }

        private void update(AudioSession sessionUpdate)
        {
            lock (m_lock)
            {
                m_updating = true;
                Console.WriteLine("Starting update: " + m_updating);

                try
                {
                    PcAudio audioState = new PcAudio();
                    AudioDevice audioDevice = new AudioDevice(GetDefaultAudioDevice().FriendlyName);
                    audioState.devices.Add(audioDevice);

                    foreach (var session in m_audioManager.GetSessionEnumerator())
                    {
                        var simpleVolume = session.QueryInterface<SimpleAudioVolume>();
                        var audioMeterInformation = session.QueryInterface<AudioMeterInformation>();
                        var session2 = session.QueryInterface<AudioSessionControl2>();
                        
                        // If we haven't seen this before, create our book keeper
                        if (!m_sessions.ContainsKey(session2.ProcessID))
                        {
                            Console.WriteLine("Found new audio session");
                            AudioSessionListener listener = new AudioSessionListener(this, session2.ProcessID);

                            AudioSessionKeeper sessionKeeper = new AudioSessionKeeper(simpleVolume, audioMeterInformation, session2, listener);
                            m_sessions.Add(session2.ProcessID, sessionKeeper);

                            session2.RegisterAudioSessionNotificationNative(listener);
                        }

                        try
                        {
                            var process = Process.GetProcessById(session2.ProcessID);

                            Console.WriteLine(process.ProcessName);
                            Console.WriteLine(audioMeterInformation.PeakValue);

                            if (sessionUpdate != null)
                            {
                                Console.WriteLine("sessionUpdate?: " + (sessionUpdate != null));

                                if (sessionUpdate.name.Equals(process.ProcessName, StringComparison.InvariantCultureIgnoreCase))
                                {
                                    Console.WriteLine("Adjusting volume: " + sessionUpdate.name + " - " + sessionUpdate.volume);
                                    Console.WriteLine("In update?: " + m_updating);

                                    simpleVolume.MasterVolume = sessionUpdate.volume;
                                }
                            }

                            AudioSession audioSession = new AudioSession(process.ProcessName, simpleVolume.MasterVolume);
                            audioDevice.sessions.Add(audioSession);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("Proccess in audio session no longer alive");
                        }
                    }

                    m_audioState = audioState;
                }
                finally
                {
                    m_updating = false;
                    Console.WriteLine("Update complete!");
                }
            }
        }

        private class AudioSessionKeeper
        {
            SimpleAudioVolume m_simpleAudioVolume;
            AudioMeterInformation m_audioMeterInformation;
            public AudioSessionControl2 m_session2;
            public AudioSessionListener m_listener;

            public AudioSessionKeeper(SimpleAudioVolume simpleAudioVolume,
                                AudioMeterInformation audioMeterInformation,
                                AudioSessionControl2 session2,
                                AudioSessionListener listener)
            {
                m_simpleAudioVolume = simpleAudioVolume;
                m_audioMeterInformation = audioMeterInformation;
                m_session2 = session2;
                m_listener = listener;
            }
        }

        private class AudioSessionListener : IAudioSessionEvents
        {
            private int m_proccessId;
            private MainWindow m_mainWindow;

            public AudioSessionListener(MainWindow mainWindow, int proccessid)
            {
                m_mainWindow = mainWindow;
                m_proccessId = proccessid;
            }

            void IAudioSessionEvents.OnChannelVolumeChanged(int channelCount, float[] newChannelVolumeArray, int changedChannel, ref Guid eventContext)
            {
                Console.WriteLine("OnChannelVolumeChanged: " + channelCount);
                //m_mainWindow.updateAndDispatchAudioState();
            }

            void IAudioSessionEvents.OnDisplayNameChanged(string newDisplayName, ref Guid eventContext)
            {
                Console.WriteLine("OnDisplayNameChanged: " + newDisplayName);
                //m_mainWindow.updateAndDispatchAudioState();
            }

            void IAudioSessionEvents.OnGroupingParamChanged(ref Guid newGroupingParam, ref Guid eventContext)
            {
                Console.WriteLine("OnGroupingParamChanged: " + newGroupingParam);
                //m_mainWindow.updateAndDispatchAudioState();
            }

            void IAudioSessionEvents.OnIconPathChanged(string newIconPath, ref Guid eventContext)
            {
                Console.WriteLine("OnIconPathChanged: " + newIconPath);
                //m_mainWindow.updateAndDispatchAudioState();
            }

            void IAudioSessionEvents.OnSessionDisconnected(AudioSessionDisconnectReason disconnectReason)
            {
                Console.WriteLine("OnSessionDisconnected: " + disconnectReason);

                m_mainWindow.m_sessions.Remove(m_proccessId);
                m_mainWindow.updateAndDispatchAudioState();
            }

            void IAudioSessionEvents.OnSimpleVolumeChanged(float newVolume, bool newMute, ref Guid eventContext)
            {
                if ( !m_mainWindow.m_updating && !eventContext.Equals(Guid.Empty) )
                {
                    Console.WriteLine("OnSimpleVolumeChanged: " + newVolume + " " + newMute + " eventContext " + eventContext);
                    ThreadPool.QueueUserWorkItem(o =>
                    {
                        m_mainWindow.updateAndDispatchAudioState();
                    });
                }
                else
                {
                    //Console.WriteLine("OnSimpleVolumeChanged: skipping update because currently inside update");
                }
            }

            void IAudioSessionEvents.OnStateChanged(AudioSessionState newState)
            {
                Console.WriteLine("OnStateChanged: " + newState);
                
                if( newState == AudioSessionState.AudioSessionStateExpired )
                {
                    ThreadPool.QueueUserWorkItem(o =>
                    {
                        Console.WriteLine("Removing audio session");

                        m_mainWindow.m_sessions.Remove(m_proccessId);
                        m_mainWindow.updateAndDispatchAudioState();
                    });
                }
            }
        }

        private static MMDevice GetDefaultAudioDevice()
        {
            using (var enumerator = new MMDeviceEnumerator())
            {
                return enumerator.GetDefaultAudioEndpoint(DataFlow.Render, CSCore.CoreAudioAPI.Role.Multimedia);
            }
        }

        private static MMDevice GetAudioDevice( string deviceId )
        {
            using (var enumerator = new MMDeviceEnumerator())
            {
                using (var device = enumerator.GetDevice(deviceId))
                {
                    return device;
                }
            }
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
                ThreadPool.QueueUserWorkItem(o =>
                {
                    var audioSession = JsonConvert.DeserializeObject<AudioSession>(message);
                    update(audioSession);
                });
            }
        }

        public void onServerStart()
        {
            updateConnectionStatus();
        }

        public void onServerEnd()
        {
            updateConnectionStatus();
        }

        public void onClientConnect()
        {
            updateAndDispatchAudioState();
        }

        public void updateAndDispatchAudioState()
        {
            update(null);

            if (m_audioState != null)
            {
                dispatchAudioState();
            }
        }

        public void dispatchAudioState()
        {
            if(m_server != null)
            {
                ThreadPool.QueueUserWorkItem(o =>
                {
                    var json = JsonConvert.SerializeObject(m_audioState);
                    //Console.WriteLine("Sending audio state: " + json);
                    Console.WriteLine("Sending audio state");
                    m_server.sendData(json);
                });
            }
        }

        public bool stopServer()
        {
            bool stopped = false;
            if (m_server != null)
            {
                m_server.stop();
                m_server = null;

                stopped = true;
            }

            updateConnectionStatus();

            return stopped;
        }

        private void start_button_Click(object sender, RoutedEventArgs e)
        {
            m_server = new Server(this);
        }

        private void stop_button_Click(object sender, RoutedEventArgs e)
        {
            stopServer();
        }

        private void exit_button_Click(object sender, RoutedEventArgs e)
        {
            stopServer();

            System.Windows.Application.Current.Shutdown();
        }

        private void MainWindow_Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            stopServer();
        }
    }
}
