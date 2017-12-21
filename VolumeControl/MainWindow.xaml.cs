using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using AudioSwitcher.AudioApi.CoreAudio;
using AudioSwitcher.AudioApi;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Windows.Navigation;
using AudioSwitcher.AudioApi.Session;

namespace VolumeControl
{
    public partial class MainWindow : Window, ClientListener
    {
        public static int VERSION = 6;
        private static Object m_lock = new Object();

        private CoreAudioController m_coreAudioController;
        private Server m_server;

        private PcAudio m_audioState;
        private UpdateListener m_updateListener;
        private Dictionary<string, AudioSessionKeeper> m_sessions = new Dictionary<string, AudioSessionKeeper>();

        private AudioSessionVolumeListener m_sessionVolumeListener;
        private AudioSessionMuteListener m_sessionMuteListener;

        private Subject<bool> m_updateSubject = new Subject<bool>();

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
                .Synchronize()
                .Throttle(TimeSpan.FromMilliseconds(10))
                .SubscribeOnDispatcher()
                .Subscribe(m_updateListener);

            string ipAddress = GetLocalIPAddress();
            server_ip.Content = ipAddress;
            Console.WriteLine("ipAddress: " + ipAddress);

            m_sessionVolumeListener = new AudioSessionVolumeListener(this);
            m_sessionMuteListener = new AudioSessionMuteListener(this);

            updateConnectionStatus();

            m_coreAudioController = new CoreAudioController();

            m_coreAudioController.DefaultPlaybackDevice.SessionController.SessionCreated.Subscribe( new AudioSessionAddedListener(this) );
            m_coreAudioController.DefaultPlaybackDevice.SessionController.SessionDisconnected.Subscribe(new AudioSessionRemovedListener(this));
            m_coreAudioController.AudioDeviceChanged.Subscribe( new DeviceChangeListener(this) );

            MasterVolumeListener masterVolumeListener = new MasterVolumeListener(this);

            m_coreAudioController.DefaultPlaybackDevice.VolumeChanged
                                //.Throttle(TimeSpan.FromMilliseconds(10))
                                .Subscribe(masterVolumeListener);

            m_coreAudioController.DefaultPlaybackDevice.MuteChanged
                                //.Throttle(TimeSpan.FromMilliseconds(10))
                                .Subscribe(masterVolumeListener);

            new Thread(() =>
            {
                updateState(null);

                m_server = new Server(this);
            }).Start();
        }

        private void DownloadLatest_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }

        public void requestUpdate()
        {
            m_updateSubject.OnNext(true);
        }

        private void updateConnectionStatus()
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
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

        private class UpdateListener : IObserver<bool>
        {
            private MainWindow m_mainWindow;

            public UpdateListener(MainWindow mainWindow)
            {
                m_mainWindow = mainWindow;
            }

            public void OnCompleted()
            {
                
            }

            public void OnError(Exception error)
            {
                
            }

            public void OnNext(bool value)
            {
                m_mainWindow.updateAndDispatchAudioState();
            }
        }

        private void cleanUpSessionKeepers()
        {
            var defaultDevice = m_coreAudioController.GetDefaultDevice(DeviceType.Playback, AudioSwitcher.AudioApi.Role.Multimedia);

            IDictionary<string, IAudioSession> currentSessions = new Dictionary<string, IAudioSession>();
            foreach( var session in defaultDevice.SessionController.All() )
            {
                currentSessions[session.Id] = session;
            }

            List<AudioSessionKeeper> deadSessions = new List<AudioSessionKeeper>();
            foreach ( var session in m_sessions.Values)
            {
                if(!currentSessions.ContainsKey(session.id()))
                {
                    deadSessions.Add(session);
                }
            }

            foreach( var session in deadSessions )
            {
                session.Dispose();
                m_sessions.Remove(session.id());
            }
        }

        private bool updateState(PcAudio audioUpdate)
        {
            Console.WriteLine("update");

            lock (m_lock)
            {
                try
                {
                    PcAudio audioState = new PcAudio();
                    audioState.version = VERSION;

                    cleanUpSessionKeepers();

                    var defaultDevice = m_coreAudioController.GetDefaultDevice(DeviceType.Playback, AudioSwitcher.AudioApi.Role.Multimedia);
                    if (defaultDevice != null)
                    {
                        string defaultDeviceId = defaultDevice.Id.ToString();

                        // Add all avalible audio devices to our list of device IDs
                        IEnumerable<CoreAudioDevice> devices = m_coreAudioController.GetPlaybackDevices();
                        foreach (var device in devices)
                        {
                            if (device.State == AudioSwitcher.AudioApi.DeviceState.Active)
                            {
                                audioState.deviceIds.Add(device.Id.ToString(), device.FullName);
                            }
                        }

                        // Master device updates
                        if (audioUpdate != null && audioUpdate.defaultDevice != null)
                        {
                            if (!audioUpdate.defaultDevice.deviceId.Equals(defaultDeviceId))
                            {
                                Guid deviceId = Guid.Parse(audioUpdate.defaultDevice.deviceId);

                                CoreAudioDevice newDefaultAudioDevice = m_coreAudioController.GetDevice(deviceId);
                                if (newDefaultAudioDevice != null)
                                {
                                    Console.WriteLine("Updated default audio device: " + audioUpdate.defaultDevice.deviceId);

                                    m_coreAudioController.SetDefaultDevice(newDefaultAudioDevice);
                                    m_coreAudioController.SetDefaultCommunicationsDevice(newDefaultAudioDevice);

                                    return false;
                                }
                                else
                                {
                                    Console.WriteLine("Failed to update default audio device. Could not find device for ID: " + audioUpdate.defaultDevice.deviceId);
                                }
                            }
                            else
                            {
                                if (audioUpdate.defaultDevice.masterMuted != null || audioUpdate.defaultDevice.masterVolume != null)
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

                                        m_coreAudioController.DefaultPlaybackDevice.Volume = volume;
                                    }

                                    return false;
                                }
                            }
                        }

                        // Create our default audio device and populate it's volume and mute status
                        AudioDevice audioDevice = new AudioDevice(defaultDevice.FullName, defaultDeviceId);
                        audioState.defaultDevice = audioDevice;

                        CoreAudioDevice defaultPlaybackDevice = m_coreAudioController.DefaultPlaybackDevice;
                        audioDevice.masterVolume = (float)defaultPlaybackDevice.Volume;
                        audioDevice.masterMuted = defaultPlaybackDevice.IsMuted;

                        // Go through all audio sessions
                        foreach (var session in defaultDevice.SessionController.All())
                        {
                            if (!session.IsSystemSession)
                            {
                                // If we haven't seen this before, create our book keeper
                                string sessionId = session.Id.ToString();
                                if (!m_sessions.ContainsKey(sessionId))
                                {
                                    //Console.WriteLine("Found new audio session");

                                    AudioSessionKeeper sessionKeeper = new AudioSessionKeeper(session, m_sessionVolumeListener, m_sessionMuteListener);
                                    m_sessions.Add(session.Id, sessionKeeper);
                                }

                                try
                                {
                                    // Audio session update
                                    if (audioUpdate != null && audioUpdate.defaultDevice != null && audioUpdate.defaultDevice.deviceId != null)
                                    {
                                        if (audioUpdate.defaultDevice.deviceId.Equals(defaultDeviceId))
                                        {
                                            if (audioUpdate.defaultDevice.sessions != null && audioUpdate.defaultDevice.sessions.Count > 0)
                                            {
                                                foreach (AudioSession sessionUpdate in audioUpdate.defaultDevice.sessions)
                                                {
                                                    Console.WriteLine("sessionUpdate?: " + (audioUpdate != null));

                                                    if (sessionUpdate.id.Equals(session.Id))
                                                    {
                                                        Console.WriteLine("Adjusting volume: " + sessionUpdate.name + " - " + sessionUpdate.volume);
                                                        Console.WriteLine("Adjusting mute: " + sessionUpdate.muted + " - " + sessionUpdate.muted);

                                                        session.Volume = sessionUpdate.volume;
                                                        session.IsMuted = sessionUpdate.muted;

                                                        break;
                                                    }
                                                }
                                            }
                                        }
                                    }

                                    string sessionName = session.DisplayName;
                                    if (sessionName == null || sessionName.Trim() == "")
                                    {
                                        using (var process = Process.GetProcessById(session.ProcessId))
                                        {
                                            sessionName = process.ProcessName;
                                        }
                                    }

                                    AudioSession audioSession = new AudioSession(sessionName, session.Id, (float)session.Volume, session.IsMuted);
                                    audioDevice.sessions.Add(audioSession);
                                }
                                catch (Exception e)
                                {
                                    Console.WriteLine(e.Message);
                                    Console.WriteLine(e.StackTrace);
                                    Console.WriteLine("Proccess in audio session no longer alive");

                                    AudioSessionKeeper sessionKeeper = m_sessions[session.Id];
                                    m_sessions.Remove(session.Id);
                                    sessionKeeper.Dispose();
                                }
                            }
                        }

                        m_audioState = audioState;
                    }
                    else
                    {
                        Console.WriteLine("Update complete!");
                    }
                }
                finally
                {
                    Console.WriteLine("Update complete!");
                }
            }

            return true;
        }

        public void onClientMessage(string message, TcpClient tcpClient)
        {
            if(message != null)
            {
                ThreadPool.QueueUserWorkItem(o =>
                {
                    try
                    {
                        Console.WriteLine("client message: " + message);
                        var pcAudio = JsonConvert.DeserializeObject<PcAudio>(message, m_jsonsettings);

                        if(VERSION == pcAudio.version)
                        {
                            updateState(pcAudio);
                        }
                        else
                        {
                            Console.WriteLine("Bad version from client. Dropping client.");
                            tcpClient.Close();
                        }
                    }
                    catch(JsonException e)
                    {
                        Console.WriteLine("Bad message from client. Dropping client.");
                        tcpClient.Close();
                    }
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
            requestUpdate();
        }

        public void updateAndDispatchAudioState()
        {
            updateState(null);

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
