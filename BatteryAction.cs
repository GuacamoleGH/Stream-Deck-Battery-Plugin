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

    /// <summary>Lógica común: dibuja el icono y el porcentaje de un BatteryMonitor.</summary>
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
            // Forzar una lectura nueva; el icono se actualiza en el siguiente OnTick.
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
                Logger.Instance.LogMessage(TracingLevel.ERROR, "Error dibujando la tecla: " + ex);
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

        /// <summary>
        /// Hay un porcentaje que mostrar con normalidad: lectura buena, o dispositivo dormido
        /// (el dongle está pero no contesta) con un valor conocido de antes.
        /// </summary>
        private static bool IsUsable(BatteryReading r)
        {
            return r.Percent >= 0
                && (r.Status == BatteryStatus.Ok || r.Status == BatteryStatus.NoResponse);
        }

        /// <summary>Qué icono toca según el estado.</summary>
        private static string IconFor(BatteryReading r)
        {
            if (!IsUsable(r))
            {
                return "pluginAction.png";   // gris: aún sin ningún valor, o dispositivo no encontrado
            }
            if (r.Status == BatteryStatus.Ok && r.Charging)
            {
                return "battcharge.png";     // azul con rayo
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
                    // El exe está en la carpeta del plugin, junto a la carpeta Images.
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

            bool live = IsUsable(r);
            bool hasValue = r.Percent >= 0;

            // Porcentaje debajo de la batería del icono
            string text = hasValue ? r.Percent + "%" : "--";
            using (SKFont font = SkiaTools.CreateFont("Arial", 28, SKFontStyle.Bold))
            using (var paint = new SKPaint { Color = live ? SKColors.White : Gray, IsAntialias = true })
            {
                float x = canvas.GetTextCenter(text, width, font);
                canvas.DrawTextLine(text, font, paint, new SKPoint(x, 96));
            }

            // Mensaje de estado cuando no hay lectura fresca (arriba de la batería)
            string note = label;
            switch (r.Status)
            {
                case BatteryStatus.Starting: note = "leyendo..."; break;
                case BatteryStatus.NoDevice: note = "sin dongle"; break;
            }
            if (note != null)
            {
                // Etiqueta (lectura buena) en claro; avisos de estado en gris.
                SKColor noteColor = live ? LabelColor : Gray;
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
