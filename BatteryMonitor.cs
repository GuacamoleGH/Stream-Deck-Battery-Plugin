using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BarRaider.SdTools;
using HidSharp;

namespace RazerBattery
{
    /// <summary>
    /// Polls a device battery in the background. There is one instance per device,
    /// shared by all keys displaying it.
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

        /// <summary>Short name displayed on the key.</summary>
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

        /// <summary>Wakes the polling loop for an immediate read instead of waiting 30 seconds.</summary>
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
                    Logger.Instance.LogMessage(TracingLevel.ERROR, logName + ": error reading battery: " + ex);
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
            // Values above 100 mean the headphones are powered off (the receiver still responds).
            if (HyperXHid.TryReadBattery(devices, out value, out name) && value <= 100)
            {
                // Charging status is not yet available for these headphones.
                return new BatteryReading(BatteryStatus.Ok, value, false, name);
            }
            return new BatteryReading(BatteryStatus.NoResponse, previous.Percent, previous.Charging, previous.DeviceName);
        }
    }
}
