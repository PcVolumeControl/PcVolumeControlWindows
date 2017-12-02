using System.Collections.Generic;

namespace VolumeControl
{
    class PcAudio
    {
        public int version;
        public Dictionary<string,string> deviceIds = new Dictionary<string,string>();
        public AudioDevice defaultDevice;
    }

    class AudioDevice
    {
        public string deviceId;
        public string name;
        public float? masterVolume = null;
        public bool? masterMuted = null;
        public List<AudioSession> sessions = new List<AudioSession>();

        public AudioDevice(string name, string deviceId)
        {
            this.name = name;
            this.deviceId = deviceId;
        }
    }

    class AudioSession
    {
        public string name;
        public float volume;
        public bool muted;

        public AudioSession(string name, float volume, bool muted)
        {
            this.name = name;
            this.volume = volume;
            this.muted = muted;
        }
    }
}
