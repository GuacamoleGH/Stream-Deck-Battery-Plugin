using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BarRaider.SdTools;
using HidSharp;

namespace RazerBattery
{
    /// <summary>
    /// Sondea la batería de un dispositivo en segundo plano. Hay una instancia por dispositivo,
    /// compartida por todas las teclas que lo muestren.
    /// </summary>
    internal sealed class BatteryMonitor
    {
        public static readonly BatteryMonitor Razer = new BatteryMonitor("Razer", "Viper", PollRazer);
        public static readonly BatteryMonitor HyperX = new BatteryMonitor("HyperX", "Cloud III S", PollHyperX);

        private const int PollSeconds = 30;

        private readonly string logName;
        private readonly Func<BatteryReading, BatteryReading> poll;
        private readonly SemaphoreSlim wake = new SemaphoreSlim(0);
        private readonly object startLock = new object();
        private bool started;
        private volatile BatteryReading latest = BatteryReading.Starting;

        /// <summary>Nombre corto que se dibuja en la tecla.</summary>
        public string Label { get; }

        private BatteryMonitor(string logName, string label, Func<BatteryReading, BatteryReading> poll)
        {
            this.logName = logName;
            this.poll = poll;
            Label = label;
        }

        public BatteryReading Latest
        {
            get { return latest; }
        }

        public void Start()
        {
            lock (startLock)
            {
                if (started)
                {
                    return;
                }
                started = true;
            }
            Task.Run(Loop);
        }

        /// <summary>Despierta el sondeo para leer ya, sin esperar a los 30 s.</summary>
        public void RefreshNow()
        {
            if (wake.CurrentCount == 0)
            {
                wake.Release();
            }
        }

        private async Task Loop()
        {
            while (true)
            {
                try
                {
                    BatteryReading previous = latest;
                    BatteryReading next = poll(previous);

                    if (next.Status != previous.Status)
                    {
                        Logger.Instance.LogMessage(TracingLevel.INFO,
                            logName + ": " + previous.Status + " -> " + next.Status + " (" + next.Percent + "%)");
                    }
                    latest = next;
                }
                catch (Exception ex)
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, logName + ": error leyendo la batería: " + ex);
                }

                await wake.WaitAsync(TimeSpan.FromSeconds(PollSeconds)).ConfigureAwait(false);
            }
        }

        private static BatteryReading PollRazer(BatteryReading previous)
        {
            List<HidInfo> devices = RazerHid.FindDevices();
            if (devices.Count == 0)
            {
                return new BatteryReading(BatteryStatus.NoDevice, previous.Percent, previous.Charging, previous.DeviceName);
            }

            int percent;
            bool charging;
            string name;
            if (RazerHid.TryReadBattery(devices, out percent, out charging, out name))
            {
                return new BatteryReading(BatteryStatus.Ok, percent, charging, name);
            }
            return new BatteryReading(BatteryStatus.NoResponse, previous.Percent, previous.Charging, previous.DeviceName);
        }

        private static BatteryReading PollHyperX(BatteryReading previous)
        {
            List<HidDevice> devices = HyperXHid.FindDevices();
            if (devices.Count == 0)
            {
                return new BatteryReading(BatteryStatus.NoDevice, previous.Percent, previous.Charging, previous.DeviceName);
            }

            int value;
            string name;
            // Valores por encima de 100 = auriculares apagados (el dongle responde igualmente).
            if (HyperXHid.TryReadBattery(devices, out value, out name) && value <= 100)
            {
                // Aún no se sabe leer el estado de carga de estos auriculares.
                return new BatteryReading(BatteryStatus.Ok, value, false, name);
            }
            return new BatteryReading(BatteryStatus.NoResponse, previous.Percent, previous.Charging, previous.DeviceName);
        }
    }
}
