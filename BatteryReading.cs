namespace RazerBattery
{
    internal enum BatteryStatus
    {
        Starting,    // no reading has been completed yet
        NoDevice,    // no Viper Ultimate / receiver is connected
        NoResponse,  // the receiver is connected, but the mouse is not responding (asleep or powered off)
        Ok
    }

    /// <summary>Immutable reading: replaced as a whole on each poll.</summary>
    internal sealed class BatteryReading
    {
        public static readonly BatteryReading Starting = new BatteryReading(BatteryStatus.Starting, -1, false, null);

        public BatteryStatus Status { get; }
        public int Percent { get; }        // -1 if no reading has ever been obtained
        public bool Charging { get; }
        public string DeviceName { get; }

        public BatteryReading(BatteryStatus status, int percent, bool charging, string deviceName)
        {
            Status = status;
            Percent = percent;
            Charging = charging;
            DeviceName = deviceName;
        }
    }
}
