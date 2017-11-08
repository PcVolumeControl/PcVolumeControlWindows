using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VolumeControl
{
    class PcAudio
    {
        public List<AudioDevice> devices = new List<AudioDevice>();
    }

    class AudioDevice
    {
        public string name;
        public List<AudioSession> sessions = new List<AudioSession>();

        public AudioDevice(string name)
        {
            this.name = name;
        }
    }

    class AudioSession
    {
        public string name;
        public float volume;

        public AudioSession(string name, float volume)
        {
            this.name = name;
            this.volume = volume;
        }
    }
}
