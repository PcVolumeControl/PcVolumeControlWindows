using AudioSwitcher.AudioApi.Session;
using System;

namespace VolumeControl
{
    class AudioSessionKeeper : IDisposable
    {
        private string m_id;

        private IDisposable m_volumeSubscription;
        private IDisposable m_muteSubscription;

        public AudioSessionKeeper(IAudioSession session, AudioSessionVolumeListener volumeListener, AudioSessionMuteListener muteListener)
        {
            m_id = session.Id;

            m_volumeSubscription = session.VolumeChanged.Subscribe(volumeListener);
            m_muteSubscription = session.MuteChanged.Subscribe(muteListener);
        }

        public string id()
        {
            return m_id;
        }

        public void Dispose()
        {
            m_volumeSubscription.Dispose();
            m_muteSubscription.Dispose();

            m_volumeSubscription = null;
            m_muteSubscription = null;
        }
    }
}
