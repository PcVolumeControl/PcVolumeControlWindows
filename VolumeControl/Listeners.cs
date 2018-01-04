using AudioSwitcher.AudioApi;
using AudioSwitcher.AudioApi.Session;
using System;

namespace VolumeControl
{
    public class AudioSessionRemovedListener : IObserver<string>
    {
        private App m_app;

        public AudioSessionRemovedListener(App app)
        {
            m_app = app;
        }

        public void OnCompleted()
        {

        }

        public void OnError(Exception error)
        {

        }

        public void OnNext(string sessionId)
        {
            m_app.requestUpdate();
        }
    }

    public class AudioSessionAddedListener : IObserver<IAudioSession>
    {
        private App m_app;

        public AudioSessionAddedListener(App app)
        {
            m_app = app;
        }

        public void OnCompleted()
        {

        }

        public void OnError(Exception error)
        {

        }

        public void OnNext(IAudioSession session)
        {
            m_app.requestUpdate();
        }
    }

    public class DeviceChangeListener : IObserver<DeviceChangedArgs>
    {
        private App m_app;

        public DeviceChangeListener(App app)
        {
            m_app = app;
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
                    m_app.requestUpdate();
                    break;
                case DeviceChangedType.DeviceAdded:
                    m_app.requestUpdate();
                    break;
                case DeviceChangedType.DeviceRemoved:
                    m_app.requestUpdate();
                    break;
                case DeviceChangedType.PropertyChanged:
                    break;
                case DeviceChangedType.StateChanged:
                    break;
                case DeviceChangedType.MuteChanged:
                    m_app.requestUpdate();
                    break;
                case DeviceChangedType.VolumeChanged:
                    m_app.requestUpdate();
                    break;
                case DeviceChangedType.PeakValueChanged:
                    m_app.requestUpdate();
                    break;
            }
        }
    }

    public class AudioSessionVolumeListener : IObserver<SessionVolumeChangedArgs>
    {
        private App m_app;

        public AudioSessionVolumeListener(App app)
        {
            m_app = app;
        }

        public void OnCompleted()
        {

        }

        public void OnError(Exception error)
        {

        }

        public void OnNext(SessionVolumeChangedArgs args)
        {
            m_app.requestUpdate();
        }
    }

    public class AudioSessionMuteListener : IObserver<SessionMuteChangedArgs>
    {
        private App m_app;

        public AudioSessionMuteListener(App app)
        {
            m_app = app;
        }

        public void OnCompleted()
        {

        }

        public void OnError(Exception error)
        {

        }

        public void OnNext(SessionMuteChangedArgs args)
        {
            m_app.requestUpdate();
        }
    }

    public class MasterVolumeListener : IObserver<DeviceVolumeChangedArgs>, IObserver<DeviceMuteChangedArgs>
    {
        private App m_app;

        public MasterVolumeListener(App app)
        {
            m_app = app;
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
            m_app.requestUpdate();
        }

        public void OnNext(DeviceVolumeChangedArgs value)
        {
            Console.WriteLine("Master volume changed");
            m_app.requestUpdate();
        }
    }
}
