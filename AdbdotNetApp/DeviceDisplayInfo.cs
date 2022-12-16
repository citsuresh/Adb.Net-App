using FluentAdb.Interfaces;

namespace AdbdotNetApp
{
    public class DeviceDisplayInfo
    {
        public IDeviceInfo DeviceInfoObject { get; set; }
        public string DisplayText { get; set; }
    }
}