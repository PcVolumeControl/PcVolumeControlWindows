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
using AudioSwitcher.AudioApi.CoreAudio;
using AudioSwitcher.AudioApi.Observables;
using AudioSwitcher.AudioApi;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using System.Reactive.Subjects;
using System.Reactive.Concurrency;
using System.Windows.Navigation;

namespace VolumeControl
{
    public partial class MainWindow : Window, ClientListener
    {
        public int VERSION = 1;

        MMDeviceEnumerator m_deviceEnumerator;

        AudioSessionManager2 m_audioManager;

        EndpointCallback m_endpointCallback;
        AudioSessionNotificationListener m_audioSessionListener;
        CoreAudioController m_coreAudioController;

        PcAudio m_audioState;

        Server m_server;

        static Object m_lock = new Object();

        Subject<bool> m_updateSubject = new Subject<bool>();

        Dictionary<int, AudioSessionKeeper> m_sessions = new Dictionary<int, AudioSessionKeeper>();

        private string m_defaultDeviceId;

        private UpdateListener m_updateListener;

        JsonSerializerSettings m_jsonsettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Ignore
        };

        public MainWindow()
        {
            InitializeComponent();

            version_view.Content = "V " + VERSION;

            m_updateListener = new UpdateListener(this);
            m_updateSubject
                .Throttle(TimeSpan.FromMilliseconds(10))
                .SubscribeOn(NewThreadScheduler.Default)
                .Subscribe(m_updateListener);

            m_endpointCallback = new EndpointCallback(this);

            string ipAddress = GetLocalIPAddress();
            server_ip.Content = ipAddress;
            Console.WriteLine("ipAddress: " + ipAddress);

            updateConnectionStatus();

            m_audioSessionListener = new AudioSessionNotificationListener(this);

            m_deviceEnumerator = new MMDeviceEnumerator();
            m_deviceEnumerator.RegisterEndpointNotificationCallback(m_endpointCallback);

            m_coreAudioController = new CoreAudioController();

            MasterVolumeListener masterVolumeListener = new MasterVolumeListener(this);

            m_coreAudioController.DefaultPlaybackDevice.VolumeChanged
                                .Throttle(TimeSpan.FromMilliseconds(100))
                                .Subscribe(masterVolumeListener);

            m_coreAudioController.DefaultPlaybackDevice.MuteChanged
                                .Throttle(TimeSpan.FromMilliseconds(100))
                                .Subscribe(masterVolumeListener);

            new Thread(() =>
            {
                updateDefaultAudioDevice();

                update(null);

                m_server = new Server(this);
            }).Start();
        }

        private void DownloadLatest_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }

        private void requestUpdate()
        {
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                m_updateSubject.OnNext(true);
            }));
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
                m_mainWindow.requestUpdate();

                return 0;
            }
        }

        private class UpdateListener : IObserver<bool>
        {
            private MainWindow m_mainWindow;

            public UpdateListener(MainWindow mainWindow)
            {
                m_mainWindow = mainWindow;
            }

            public void OnCompleted()
            {
                Console.WriteLine("OnCompleted");
            }

            public void OnError(Exception error)
            {
                Console.WriteLine("OnError");
            }

            public void OnNext(bool value)
            {
                m_mainWindow.updateAndDispatchAudioState();
            }
        }

        private class MasterVolumeListener : IObserver<DeviceVolumeChangedArgs>, IObserver<DeviceMuteChangedArgs>
        {
            private MainWindow m_mainWindow;

            public MasterVolumeListener(MainWindow mainWindow)
            {
                m_mainWindow = mainWindow;
            }

            public void OnCompleted()
            {
                
            }

            public void OnError(Exception error)
            {
                
            }

            public void OnNext(DeviceMuteChangedArgs value)
            {
                Console.WriteLine("Master volume mute changed");
                m_mainWindow.requestUpdate();
            }

            public void OnNext(DeviceVolumeChangedArgs value)
            {
                Console.WriteLine("Master volume changed");
                m_mainWindow.requestUpdate();
            }
        }

        private class EndpointCallback : IMMNotificationClient
        {
            private MainWindow m_mainWindow;

            public EndpointCallback(MainWindow mainWindow)
            {
                m_mainWindow = mainWindow;
            }

            void IMMNotificationClient.OnDefaultDeviceChanged(DataFlow dataFlow, CSCore.CoreAudioAPI.Role role, string deviceId)
            {
                if (!deviceId.Equals(m_mainWindow.m_defaultDeviceId))
                {
                    m_mainWindow.m_defaultDeviceId = deviceId;

                    lock (m_lock)
                    {
                        Console.WriteLine("OnDefaultDeviceChanged: " + deviceId);

                        m_mainWindow.updateDefaultAudioDevice();
                        m_mainWindow.m_sessions.Clear();

                        m_mainWindow.requestUpdate();
                    }
                }
            }

            void IMMNotificationClient.OnDeviceAdded(string deviceId)
            {
                Console.WriteLine("OnDeviceAdded: " + deviceId);
                m_mainWindow.requestUpdate();
            }

            void IMMNotificationClient.OnDeviceRemoved(string deviceId)
            {
                Console.WriteLine("OnDeviceRemoved: " + deviceId);
                m_mainWindow.requestUpdate();
            }

            void IMMNotificationClient.OnDeviceStateChanged(string deviceId, CSCore.CoreAudioAPI.DeviceState deviceState)
            {
                Console.WriteLine("OnDeviceStateChanged: " + deviceId);
            }

            void IMMNotificationClient.OnPropertyValueChanged(string deviceId, CSCore.Win32.PropertyKey key)
            {
                if (deviceId.Equals(m_mainWindow.m_defaultDeviceId))
                {
                    Console.WriteLine("OnPropertyValueChanged: " + deviceId + " Property: " + key);
                }
            }
        }

        private void update(PcAudio audioUpdate)
        {
            Console.WriteLine("update");
            lock (m_lock)
            {
                try
                {
                    PcAudio audioState = new PcAudio();
                    audioState.version = VERSION;

                    // Add all avalible audio devices to our list of device IDs
                    foreach ( var device in m_deviceEnumerator.EnumAudioEndpoints(DataFlow.Render, CSCore.CoreAudioAPI.DeviceState.Active) )
                    {
                        audioState.deviceIds.Add(device.DeviceID, device.FriendlyName);
                    }

                    // Master device updates
                    if (audioUpdate != null && audioUpdate.defaultDevice != null)
                    {
                        if (!audioUpdate.defaultDevice.deviceId.Equals(m_defaultDeviceId))
                        {
                            string pattern = @"\{.*\}\.\{(.*)\}";
                            Match match = Regex.Match(audioUpdate.defaultDevice.deviceId, pattern);
                            
                            Console.WriteLine("Device ID: " + match.Groups[1].Value);

                            // Switch default device
                            Guid deviceId = Guid.Parse(match.Groups[1].Value);
                            CoreAudioDevice newDefaultAudioDevice = m_coreAudioController.GetDevice(deviceId);
                            if (newDefaultAudioDevice != null)
                            {
                                Console.WriteLine("Updated default audio device: " + audioUpdate.defaultDevice.deviceId);

                                m_coreAudioController.SetDefaultDeviceAsync(newDefaultAudioDevice);
                            }
                            else
                            {
                                Console.WriteLine("Failed to update default audio device. Could not find device for ID: " + audioUpdate.defaultDevice.deviceId);
                            }
                        }
                        else
                        {
                            if (audioUpdate.defaultDevice.masterMuted != null)
                            {
                                bool muted = audioUpdate.defaultDevice.masterMuted ?? m_coreAudioController.DefaultPlaybackDevice.IsMuted;
                                Console.WriteLine("Updating master mute: " + muted);

                                m_coreAudioController.DefaultPlaybackDevice.Mute(muted);
                            }

                            if (audioUpdate.defaultDevice.masterVolume != null)
                            {
                                float volume = audioUpdate.defaultDevice.masterVolume ?? (float)m_coreAudioController.DefaultPlaybackDevice.Volume;
                                Console.WriteLine("Updating master volume: " + volume);

                                m_coreAudioController.DefaultPlaybackDevice.Volume = volume * 100;
                            }
                        }
                    }

                    // Create our default audio device and populate it's volume and mute status
                    AudioDevice audioDevice = new AudioDevice(GetDefaultAudioDevice().FriendlyName, m_defaultDeviceId);
                    audioState.defaultDevice = audioDevice;

                    CoreAudioDevice defaultPlaybackDevice = m_coreAudioController.DefaultPlaybackDevice;
                    audioDevice.masterVolume = (float)defaultPlaybackDevice.Volume / 100f;
                    audioDevice.masterMuted = defaultPlaybackDevice.IsMuted;

                    // Go through all audio sessions
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

                            //Console.WriteLine(process.ProcessName);
                            //Console.WriteLine(audioMeterInformation.PeakValue);

                            // Audio session update
                            if (audioUpdate != null && audioUpdate.defaultDevice != null && audioUpdate.defaultDevice.deviceId != null)
                            {
                                if(audioUpdate.defaultDevice.deviceId.Equals(m_defaultDeviceId))
                                {
                                    if (audioUpdate.defaultDevice.sessions != null && audioUpdate.defaultDevice.sessions.Count > 0)
                                    {
                                        foreach( AudioSession sessionUpdate in audioUpdate.defaultDevice.sessions )
                                        {
                                            Console.WriteLine("sessionUpdate?: " + (audioUpdate != null));

                                            if (sessionUpdate.name.Equals(process.ProcessName, StringComparison.InvariantCultureIgnoreCase))
                                            {
                                                Console.WriteLine("Adjusting volume: " + sessionUpdate.name + " - " + sessionUpdate.volume);
                                                Console.WriteLine("Adjusting mute: " + sessionUpdate.muted + " - " + sessionUpdate.muted);

                                                simpleVolume.MasterVolume = sessionUpdate.volume;
                                                simpleVolume.IsMuted = sessionUpdate.muted;

                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                            
                            AudioSession audioSession = new AudioSession(process.ProcessName, simpleVolume.MasterVolume, simpleVolume.IsMuted);
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
                m_mainWindow.requestUpdate();
            }

            void IAudioSessionEvents.OnDisplayNameChanged(string newDisplayName, ref Guid eventContext)
            {
                Console.WriteLine("OnDisplayNameChanged: " + newDisplayName);
                m_mainWindow.requestUpdate();
            }

            void IAudioSessionEvents.OnGroupingParamChanged(ref Guid newGroupingParam, ref Guid eventContext)
            {
                Guid guid = newGroupingParam;
                Console.WriteLine("OnGroupingParamChanged: " + guid);
                m_mainWindow.requestUpdate();
            }

            void IAudioSessionEvents.OnIconPathChanged(string newIconPath, ref Guid eventContext)
            {
                Console.WriteLine("OnIconPathChanged: " + newIconPath);
                m_mainWindow.requestUpdate();
            }

            void IAudioSessionEvents.OnSessionDisconnected(AudioSessionDisconnectReason disconnectReason)
            {
                Console.WriteLine("OnSessionDisconnected: " + disconnectReason);
                lock (m_lock)
                {
                    m_mainWindow.m_sessions.Remove(m_proccessId);
                }
                m_mainWindow.requestUpdate();
            }

            void IAudioSessionEvents.OnSimpleVolumeChanged(float newVolume, bool newMute, ref Guid eventContext)
            {
                if ( !eventContext.Equals(Guid.Empty) )
                {
                    Console.WriteLine("OnSimpleVolumeChanged: " + newVolume + " " + newMute + " eventContext " + eventContext);
                    m_mainWindow.requestUpdate();
                }
                else
                {
                    Console.WriteLine("OnSimpleVolumeChanged: skipping update because currently inside update");
                }
            }

            void IAudioSessionEvents.OnStateChanged(AudioSessionState newState)
            {
                if( newState == AudioSessionState.AudioSessionStateExpired )
                {
                    Console.WriteLine("OnStateChanged: " + newState);
                    lock (m_lock)
                    {
                        m_mainWindow.m_sessions.Remove(m_proccessId);
                    }
                    m_mainWindow.requestUpdate();
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
            if(message != null)
            {
                ThreadPool.QueueUserWorkItem(o =>
                {
                    Console.WriteLine("client message: " + message);
                    var pcAudio = JsonConvert.DeserializeObject<PcAudio>(message, m_jsonsettings);
                    update(pcAudio);
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
            Console.WriteLine("updateAndDispatchAudioState()");
            update(null);

            if (m_audioState != null)
            {
                Console.WriteLine("dispatching audio state");
                dispatchAudioState();
            }
            else
            {
                Console.WriteLine("m_audioState NULL no dispatch");
            }
        }

        public void dispatchAudioState()
        {
            if(m_server != null)
            {
                ThreadPool.QueueUserWorkItem(o =>
                {
                    var json = JsonConvert.SerializeObject(m_audioState, m_jsonsettings);
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
