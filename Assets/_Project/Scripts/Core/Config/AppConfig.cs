using System;

namespace Guideon.Core
{
    [Serializable]
    public class AppConfig
    {
        public ServerConfig server = new();
        public DeviceConfig device = new();
        public KioskConfig kiosk = new();
    }

    [Serializable]
    public class ServerConfig
    {
        public string baseUrl = "";
        public string wsUrl = "";
        public int timeout = 10;
    }

    [Serializable]
    public class DeviceConfig
    {
        public string token = "";
    }

    [Serializable]
    public class KioskConfig
    {
        public int idleTimeoutSeconds = 120;
        public int sttSilenceTimeoutMs = 3000;
        public bool ttsEnabled = true;
        public string language = "ko";
    }
}
