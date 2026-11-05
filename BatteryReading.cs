namespace RazerBattery
{
    internal enum BatteryStatus
    {
        Starting,    // todavía no se ha hecho ninguna lectura
        NoDevice,    // no hay ningún Viper Ultimate / dongle conectado
        NoResponse,  // el dongle está, pero el ratón no contesta (dormido o apagado)
        Ok
    }

    /// <summary>Lectura inmutable: se sustituye entera en cada sondeo.</summary>
    internal sealed class BatteryReading
    {
        public static readonly BatteryReading Starting = new BatteryReading(BatteryStatus.Starting, -1, false, null);

        public BatteryStatus Status { get; }
        public int Percent { get; }        // -1 si nunca se ha leído
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
