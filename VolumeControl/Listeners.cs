using AudioSwitcher.AudioApi;
using AudioSwitcher.AudioApi.Session;
using System;

namespace VolumeControl
{
    public class AudioSessionRemovedListener : IObserver<string>
    {
        private MainWindow m_mainWindow;

        public AudioSessionRemovedListener(MainWindow mainWindow)
        {
            m_mainWindow = mainWindow;
        }

        public void OnCompleted()
        {

        }

        public void OnError(Exception error)
        {

        }

        public void OnNext(string sessionId)
        {
            m_mainWindow.requestUpdate();
        }
    }

    public class AudioSessionAddedListener : IObserver<IAudioSession>
    {
        private MainWindow m_mainWindow;

        public AudioSessionAddedListener(MainWindow mainWindow)
        {
            m_mainWindow = mainWindow;
        }

        public void OnCompleted()
        {

        }

        public void OnError(Exception error)
        {

        }

        public void OnNext(IAudioSession session)
        {
            m_mainWindow.requestUpdate();
        }
    }

    public class DeviceChangeListener : IObserver<DeviceChangedArgs>
    {
        private MainWindow m_mainWindow;

        public DeviceChangeListener(MainWindow mainWindow)
        {
            m_mainWindow = mainWindow;
        }

        public void OnCompleted()
        {

        }

        public void OnError(Exception error)
        {

        }

        public void OnNext(DeviceChangedArgs value)
        {
            switch (value.ChangedType)
            {
                case DeviceChangedType.DefaultChanged:
                    m_mainWindow.requestUpdate();
                    break;
                case DeviceChangedType.DeviceAdded:
                    m_mainWindow.requestUpdate();
                    break;
                case DeviceChangedType.DeviceRemoved:
                    m_mainWindow.requestUpdate();
                    break;
                case DeviceChangedType.PropertyChanged:
                    break;
                case DeviceChangedType.StateChanged:
                    break;
                case DeviceChangedType.MuteChanged:
                    m_mainWindow.requestUpdate();
                    break;
                case DeviceChangedType.VolumeChanged:
                    m_mainWindow.requestUpdate();
                    break;
                case DeviceChangedType.PeakValueChanged:
                    m_mainWindow.requestUpdate();
                    break;
            }
        }
    }

    public class AudioSessionVolumeListener : IObserver<SessionVolumeChangedArgs>
    {
        private MainWindow m_mainWindow;

        public AudioSessionVolumeListener(MainWindow mainWindow)
        {
            m_mainWindow = mainWindow;
        }

        public void OnCompleted()
        {

        }

        public void OnError(Exception error)
        {

        }

        public void OnNext(SessionVolumeChangedArgs args)
        {
            m_mainWindow.requestUpdate();
        }
    }

    public class AudioSessionMuteListener : IObserver<SessionMuteChangedArgs>
    {
        private MainWindow m_mainWindow;

        public AudioSessionMuteListener(MainWindow mainWindow)
        {
            m_mainWindow = mainWindow;
        }

        public void OnCompleted()
        {

        }

        public void OnError(Exception error)
        {

        }

        public void OnNext(SessionMuteChangedArgs args)
        {
            m_mainWindow.requestUpdate();
        }
    }

    public class MasterVolumeListener : IObserver<DeviceVolumeChangedArgs>, IObserver<DeviceMuteChangedArgs>
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
}
