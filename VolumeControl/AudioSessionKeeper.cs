using AudioSwitcher.AudioApi.Session;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VolumeControl
{
    class AudioSessionKeeper : IDisposable
    {
        private string m_id;

        private IDisposable m_volumeSubscription;
        private IDisposable m_muteSubscription;

        public AudioSessionKeeper(IAudioSession session, MainWindow mainWindow)
        {
            m_id = session.Id;

            m_volumeSubscription = session.VolumeChanged.Subscribe(new AudioSessionVolumeListener(mainWindow));
            m_muteSubscription = session.MuteChanged.Subscribe(new AudioSessionMuteListener(mainWindow));
        }

        public string id()
        {
            return m_id;
        }

        public void Dispose()
        {
            m_volumeSubscription.Dispose();
            m_muteSubscription.Dispose();
        }
    }
}
