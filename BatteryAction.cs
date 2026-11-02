using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using BarRaider.SdTools;
using BarRaider.SdTools.Wrappers;
using SkiaSharp;

namespace RazerBattery
{
    [PluginActionId("com.jose.razerbattery.battery")]
    public class RazerAction : BatteryActionBase
    {
        public RazerAction(ISDConnection connection, InitialPayload payload)
            : base(connection, payload, BatteryMonitor.Razer) { }
    }

    [PluginActionId("com.jose.razerbattery.hyperx")]
    public class HyperXAction : BatteryActionBase
    {
        public HyperXAction(ISDConnection connection, InitialPayload payload)
            : base(connection, payload, BatteryMonitor.HyperX) { }
    }

    /// <summary>Shared logic: draws the icon and percentage for a BatteryMonitor.</summary>
    public abstract class BatteryActionBase : KeypadBase
    {
        private static readonly SKColor Gray = SKColor.Parse("#9aa0a6");
        private static readonly SKColor LabelColor = SKColor.Parse("#d0d4d8");

        // Los iconos se cargan una vez y se reutilizan (no se liberan: viven lo que el plugin).
        private static readonly Dictionary<string, SKBitmap> IconCache = new Dictionary<string, SKBitmap>();
        private static readonly object IconLock = new object();

        private readonly BatteryMonitor monitor;
        private string lastRendered;

        internal BatteryActionBase(ISDConnection connection, InitialPayload payload, BatteryMonitor monitor)
            : base(connection, payload)
        {
            this.monitor = monitor;
            monitor.Start();
        }

        public override void KeyPressed(KeyPayload payload)
        {
            // Force a fresh reading; the key is updated on the next OnTick.
            monitor.RefreshNow();
        }

        public override void KeyReleased(KeyPayload payload) { }

        public override async void OnTick()
        {
            try
            {
                await RenderAsync();
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, "Error rendering key: " + ex);
            }
        }

        public override void ReceivedSettings(ReceivedSettingsPayload payload) { }

        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }

        public override void Dispose() { }

        private async Task RenderAsync()
        {
            BatteryReading reading = monitor.Latest;

            // Solo redibujar si algo ha cambiado.
            string signature = reading.Status + "|" + reading.Percent + "|" + reading.Charging;
            if (signature == lastRendered)
            {
                return;
            }
            lastRendered = signature;

            using (SKBitmap image = SkiaTools.GenerateGenericKeyImage(out SKCanvas canvas))
            {
                DrawKey(canvas, image.Width, reading, monitor.Label);
                canvas.Dispose();
                await Connection.SetImageAsync(image);
            }
        }

        /// <summary>Selects the icon based on the current status.</summary>
        private static string IconFor(BatteryReading r)
        {
            if (r.Status != BatteryStatus.Ok || r.Percent < 0)
            {
                return "pluginAction.png";   // gray: no fresh reading
            }
            if (r.Charging)
            {
                return "battcharge.png";     // blue with lightning bolt
            }
            if (r.Percent > 50)
            {
                return "battgreen.png";
            }
            if (r.Percent > 20)
            {
                return "battyellow.png";
            }
            return "battred.png";
        }

        private static SKBitmap LoadIcon(string fileName)
        {
            lock (IconLock)
            {
                SKBitmap bitmap;
                if (!IconCache.TryGetValue(fileName, out bitmap))
                {
                    // The executable is in the plugin folder, next to the Images folder.
                    string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", fileName);
                    bitmap = SkiaTools.LoadImage(path);
                    IconCache[fileName] = bitmap;
                }
                return bitmap;
            }
        }

        private static void DrawKey(SKCanvas canvas, int width, BatteryReading r, string label)
        {
            canvas.Clear(SKColors.Black);
            canvas.DrawBitmap(LoadIcon(IconFor(r)), 0, 0);

            bool live = r.Status == BatteryStatus.Ok;
            bool hasValue = r.Percent >= 0;

            // Percentage below the battery icon
            string text = hasValue ? r.Percent + "%" : "--";
            using (SKFont font = SkiaTools.CreateFont("Arial", 28, SKFontStyle.Bold))
            using (var paint = new SKPaint { Color = live ? SKColors.White : Gray, IsAntialias = true })
            {
                float x = canvas.GetTextCenter(text, width, font);
                canvas.DrawTextLine(text, font, paint, new SKPoint(x, 96));
            }

            // Status message when no fresh reading is available (above the battery icon)
            string note = label;
            switch (r.Status)
            {
                case BatteryStatus.Starting: note = "reading..."; break;
                case BatteryStatus.NoDevice: note = "no device"; break;
                case BatteryStatus.NoResponse: note = "sleeping"; break;
            }
            if (note != null)
            {
                // Label (successful reading) is light; status messages are gray.
                SKColor noteColor = r.Status == BatteryStatus.Ok ? LabelColor : Gray;
                using (SKFont small = SkiaTools.CreateFont("Arial", 19, SKFontStyle.Bold))
                using (var paint = new SKPaint { Color = noteColor, IsAntialias = true })
                {
                    float x = canvas.GetTextCenter(note, width, small);
                    canvas.DrawTextLine(note, small, paint, new SKPoint(x, 23));
                }
            }
        }
    }
}
