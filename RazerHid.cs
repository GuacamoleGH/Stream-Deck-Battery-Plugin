using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using HidSharp;
using Microsoft.Win32.SafeHandles;

namespace RazerBattery
{
    internal sealed class HidInfo
    {
        public string Path { get; }
        public string Name { get; }

        public HidInfo(string path, string name)
        {
            Path = path;
            Name = name;
        }
    }

    /// <summary>
    /// Reads a Razer Viper Ultimate battery over HID using the Razer protocol
    /// (90-byte packets sent as feature reports), without requiring Synapse.
    /// </summary>
    internal static class RazerHid
    {
        private const int VendorId = 0x1532;
        private const int ReportLength = 90;
        private const byte StatusBusy = 0x01;
        private const byte StatusSuccess = 0x02;

        // Razer commands: class 0x07 = power
        private const byte ClassPower = 0x07;
        private const byte CmdBatteryLevel = 0x80;
        private const byte CmdChargingStatus = 0x84;

        private static readonly Dictionary<int, string> Products = new Dictionary<int, string>
        {
            { 0x007A, "Viper Ultimate (cable)" },
            { 0x007B, "Viper Ultimate (wireless)" }
        };

        // Typical transaction IDs for Razer wireless mice; tried in order.
        private static readonly byte[] TransactionIds = { 0x3F, 0x1F, 0xFF };

        // The last successful values, tried first next time.
        private static string lastPath;
        private static int lastTid = -1;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFile(string fileName, uint desiredAccess, uint shareMode,
            IntPtr securityAttributes, uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

        [DllImport("hid.dll", SetLastError = true)]
        private static extern bool HidD_SetFeature(SafeFileHandle device, byte[] buffer, uint length);

        [DllImport("hid.dll", SetLastError = true)]
        private static extern bool HidD_GetFeature(SafeFileHandle device, byte[] buffer, uint length);

        public static List<HidInfo> FindDevices()
        {
            var result = new List<HidInfo>();
            foreach (HidDevice device in DeviceList.Local.GetHidDevices(VendorId))
            {
                string name;
                if (Products.TryGetValue(device.ProductID, out name))
                {
                    result.Add(new HidInfo(device.DevicePath, name));
                }
            }
            return result;
        }

        public static bool TryReadBattery(List<HidInfo> devices, out int percent, out bool charging, out string deviceName)
        {
            percent = 0;
            charging = false;
            deviceName = null;

            foreach (HidInfo info in Ordered(devices))
            {
                using (SafeFileHandle handle = Open(info.Path))
                {
                    if (handle == null)
                    {
                        continue;
                    }

                    foreach (byte tid in OrderedTransactionIds(info.Path))
                    {
                        byte[] level = Query(handle, tid, ClassPower, CmdBatteryLevel);
                        if (level == null)
                        {
                            continue;
                        }

                        byte[] charge = Query(handle, tid, ClassPower, CmdChargingStatus);

                        // arguments[1] is located at byte 9 of the response
                        percent = (int)Math.Round(level[9] / 255.0 * 100.0);
                        charging = charge != null && charge[9] == 1;
                        deviceName = info.Name;

                        lastPath = info.Path;
                        lastTid = tid;
                        return true;
                    }
                }
            }

            lastPath = null;
            lastTid = -1;
            return false;
        }

        private static IEnumerable<HidInfo> Ordered(List<HidInfo> devices)
        {
            if (lastPath != null)
            {
                foreach (HidInfo d in devices)
                {
                    if (d.Path == lastPath)
                    {
                        yield return d;
                    }
                }
            }
            foreach (HidInfo d in devices)
            {
                if (d.Path != lastPath)
                {
                    yield return d;
                }
            }
        }

        private static IEnumerable<byte> OrderedTransactionIds(string path)
        {
            if (path == lastPath && lastTid >= 0)
            {
                yield return (byte)lastTid;
            }
            foreach (byte tid in TransactionIds)
            {
                if (!(path == lastPath && tid == lastTid))
                {
                    yield return tid;
                }
            }
        }

        /// <summary>
        /// Abre la interfaz HID con acceso 0 (igual que hidapi): basta para los feature reports
        /// and prevents Windows from rejecting it because it is a mouse.
        /// </summary>
        private static SafeFileHandle Open(string path)
        {
            const uint FileShareReadWrite = 3;
            const uint OpenExisting = 3;

            SafeFileHandle handle = CreateFile(path, 0, FileShareReadWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
            if (handle.IsInvalid)
            {
                handle.Dispose();
                return null;
            }
            return handle;
        }

        /// <summary>Paquete de 91 bytes: report ID (0) + 90 bytes del protocolo Razer.</summary>
        private static byte[] BuildRequest(byte transactionId, byte commandClass, byte commandId, byte dataSize)
        {
            byte[] buf = new byte[ReportLength + 1];
            buf[2] = transactionId;
            buf[6] = dataSize;
            buf[7] = commandClass;
            buf[8] = commandId;

            // CRC = XOR de los bytes 2..87 del paquete (3..88 con el report ID delante)
            byte crc = 0;
            for (int i = 3; i <= 88; i++)
            {
                crc ^= buf[i];
            }
            buf[89] = crc;
            return buf;
        }

        /// <summary>Returns the 90-byte response (without report ID), or null on failure.</summary>
        private static byte[] Query(SafeFileHandle handle, byte tid, byte commandClass, byte commandId)
        {
            byte[] request = BuildRequest(tid, commandClass, commandId, 0x02);

            for (int attempt = 0; attempt < 3; attempt++)
            {
                if (!HidD_SetFeature(handle, request, (uint)request.Length))
                {
                    return null; // esta interfaz no acepta el feature report
                }

                Thread.Sleep(60);

                byte[] buf = new byte[ReportLength + 1];
                if (!HidD_GetFeature(handle, buf, (uint)buf.Length))
                {
                    return null;
                }

                byte[] response = new byte[ReportLength];
                Array.Copy(buf, 1, response, 0, ReportLength);

                if (response[0] == StatusBusy)
                {
                    Thread.Sleep(50);
                    continue;
                }

                bool ok = response[0] == StatusSuccess
                          && response[6] == commandClass
                          && response[7] == commandId;
                return ok ? response : null;
            }
            return null;
        }
    }
}
