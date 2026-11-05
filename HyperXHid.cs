using System;
using System.Collections.Generic;
using HidSharp;

namespace RazerBattery
{
    /// <summary>
    /// Lee la batería de los HyperX Cloud III S Wireless (dongle USB) por la colección vendor
    /// usage_page 0x01C0 de la interfaz 3: report 0x0C, comando "02 03 01 00 06", respuesta con
    /// el porcentaje en el byte 6. Protocolo comprobado en el equipo del usuario con hidapi.
    /// </summary>
    internal static class HyperXHid
    {
        private const int VendorId = 0x03F0;
        private static readonly int[] ProductIds = { 0x02CC, 0x06BE }; // dos revisiones del mismo modelo

        private const byte ReportId = 0x0C;
        private const byte OpcodeBattery = 0x06;
        private const string DeviceName = "HyperX Cloud III S Wireless";

        // Usage Page 0x01C0 en el descriptor HID: 06 C0 01
        private static readonly byte[] VendorUsagePage = { 0x06, 0xC0, 0x01 };

        private static string lastPath;

        public static List<HidDevice> FindDevices()
        {
            var result = new List<HidDevice>();
            foreach (HidDevice device in DeviceList.Local.GetHidDevices(VendorId))
            {
                if (Array.IndexOf(ProductIds, device.ProductID) >= 0)
                {
                    result.Add(device);
                }
            }
            return result;
        }

        /// <summary>
        /// Devuelve true si el dongle contestó. value es el byte crudo: 0..100 = porcentaje,
        /// más de 100 = auriculares apagados.
        /// </summary>
        public static bool TryReadBattery(List<HidDevice> devices, out int value, out string name)
        {
            value = 0;
            name = null;

            foreach (HidDevice device in Candidates(devices))
            {
                int outLen;
                int inLen;
                try
                {
                    outLen = device.GetMaxOutputReportLength();
                    inLen = device.GetMaxInputReportLength();
                }
                catch (Exception)
                {
                    continue;
                }
                if (outLen < 7 || inLen < 7)
                {
                    continue;
                }

                HidStream stream;
                try
                {
                    if (!device.TryOpen(out stream))
                    {
                        continue;
                    }
                }
                catch (Exception)
                {
                    continue;
                }

                using (stream)
                {
                    try
                    {
                        stream.ReadTimeout = 300;
                        stream.WriteTimeout = 1000;

                        byte[] request = new byte[outLen];
                        request[0] = ReportId;
                        request[1] = 0x02;
                        request[2] = 0x03;
                        request[3] = 0x01;
                        request[4] = 0x00;
                        request[5] = OpcodeBattery;
                        stream.Write(request);

                        // Pueden llegar otros reports sin pedirlos (cambios de estado): se ignoran.
                        DateTime deadline = DateTime.UtcNow.AddSeconds(2);
                        byte[] buffer = new byte[inLen];
                        while (DateTime.UtcNow < deadline)
                        {
                            int read;
                            try
                            {
                                read = stream.Read(buffer);
                            }
                            catch (TimeoutException)
                            {
                                continue;
                            }

                            if (read >= 7 && buffer[0] == ReportId && buffer[1] == 0x02
                                && buffer[2] == 0x03 && buffer[5] == OpcodeBattery)
                            {
                                value = buffer[6];
                                name = DeviceName;
                                lastPath = device.DevicePath;
                                return true;
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // Esta colección no sirve: se prueba la siguiente.
                    }
                }
            }

            lastPath = null;
            return false;
        }

        /// <summary>Primero las colecciones vendor 0x01C0 (la última que funcionó, la primera).</summary>
        private static IEnumerable<HidDevice> Candidates(List<HidDevice> devices)
        {
            var preferred = new List<HidDevice>();
            var others = new List<HidDevice>();

            foreach (HidDevice device in devices)
            {
                bool isVendor = false;
                try
                {
                    isVendor = Contains(device.GetRawReportDescriptor(), VendorUsagePage);
                }
                catch (Exception)
                {
                }

                if (isVendor)
                {
                    if (device.DevicePath == lastPath)
                    {
                        preferred.Insert(0, device);
                    }
                    else
                    {
                        preferred.Add(device);
                    }
                }
                else
                {
                    others.Add(device);
                }
            }

            // Si ninguna se identificó como vendor, se prueban todas.
            if (preferred.Count == 0)
            {
                return others;
            }
            return preferred;
        }

        private static bool Contains(byte[] data, byte[] pattern)
        {
            if (data == null)
            {
                return false;
            }
            for (int i = 0; i <= data.Length - pattern.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (data[i + j] != pattern[j])
                    {
                        match = false;
                        break;
                    }
                }
                if (match)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
