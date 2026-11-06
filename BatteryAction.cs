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
        private static readonly SKColor MoonColor = SKColor.Parse("#f4d06f");

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
                DrawKey(canvas, image.Width, reading, monitor);
                canvas.Dispose();
                await Connection.SetImageAsync(image);
            }
        }

        /// <summary>
        /// Hay un porcentaje que mostrar con normalidad: lectura buena, o ratón dormido
        /// (el dongle está pero no contesta) con un valor conocido de antes.
        /// En auriculares apagados no se considera usable para que se muestre en gris (igual que desconectado).
        /// </summary>
        private static bool IsUsable(BatteryReading r, BatteryMonitor monitor)
        {
            if (r.Percent < 0)
            {
                return false;
            }
            if (r.Status == BatteryStatus.Ok)
            {
                return true;
            }
            if (r.Status == BatteryStatus.NoResponse && monitor.Kind == DeviceKind.Mouse)
            {
                return true;
            }
            return false;
        }

        /// <summary>Qué icono toca según el estado.</summary>
        private static string IconFor(BatteryReading r, BatteryMonitor monitor)
        {
            if (!IsUsable(r, monitor))
            {
                return "pluginAction.png";   // gris: apagado, desconectado, o aún sin valor
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

        private static void DrawMoon(SKCanvas canvas, float cx, float cy, float r)
        {
            using (var outer = new SKPath())
            using (var inner = new SKPath())
            {
                outer.AddCircle(cx, cy, r);
                // Círculo interior desplazado hacia la derecha para tallar un creciente lunar clásico y vertical (🌙)
                inner.AddCircle(cx + r * 0.48f, cy - r * 0.05f, r * 0.88f);
                using (SKPath moon = outer.Op(inner, SKPathOp.Difference))
                {
                    if (moon != null)
                    {
                        using (var paint = new SKPaint { Color = MoonColor, IsAntialias = true, Style = SKPaintStyle.Fill })
                        {
                            canvas.DrawPath(moon, paint);
                        }
                    }
                }
            }
        }

        private static void DrawKey(SKCanvas canvas, int width, BatteryReading r, BatteryMonitor monitor)
        {
            canvas.Clear(SKColors.Black);
            canvas.DrawBitmap(LoadIcon(IconFor(r, monitor)), 0, 0);

            bool live = IsUsable(r, monitor);
            bool hasValue = r.Percent >= 0;

            // Porcentaje debajo de la batería del icono
            string text = hasValue ? r.Percent + "%" : "--";
            using (SKFont font = SkiaTools.CreateFont("Arial", 28, SKFontStyle.Bold))
            using (var paint = new SKPaint { Color = live ? SKColors.White : Gray, IsAntialias = true })
            {
                float x = canvas.GetTextCenter(text, width, font);
                canvas.DrawTextLine(text, font, paint, new SKPoint(x, 96));
            }

            // Mensaje o etiqueta arriba de la batería
            string note = monitor.Label;
            bool showMoon = false;
            bool isAlert = false;

            switch (r.Status)
            {
                case BatteryStatus.Starting:
                    note = "Leyendo...";
                    isAlert = true;
                    break;
                case BatteryStatus.NoDevice:
                    note = "Desconectado";
                    isAlert = true;
                    break;
                case BatteryStatus.NoResponse:
                    if (monitor.Kind == DeviceKind.Mouse)
                    {
                        // Ratón dormido: mantiene la etiqueta (ej. "Viper") y muestra la lunita
                        note = monitor.Label;
                        showMoon = true;
                    }
                    else
                    {
                        // Cascos apagados: el dongle está conectado pero el auricular está apagado
                        note = "Apagados";
                        isAlert = true;
                    }
                    break;
                case BatteryStatus.Ok:
                    note = monitor.Label;
                    break;
            }

            if (note != null)
            {
                SKColor noteColor = isAlert ? Gray : LabelColor;
                float fontSize = note.Length > 10 ? 16f : 19f;

                using (SKFont small = SkiaTools.CreateFont("Arial", fontSize, SKFontStyle.Bold))
                using (var paint = new SKPaint { Color = noteColor, IsAntialias = true })
                {
                    float noteWidth = small.MeasureText(note);
                    float moonRadius = 7.5f;
                    float moonGap = 7f;
                    // Centrar conjuntamente el texto y la lunita como un único bloque armonioso
                    float totalHeaderWidth = showMoon ? (noteWidth + moonGap + moonRadius * 2f) : noteWidth;
                    float startX = (width - totalHeaderWidth) / 2f;

                    canvas.DrawTextLine(note, small, paint, new SKPoint(startX, 23));

                    if (showMoon)
                    {
                        float moonCenterX = startX + noteWidth + moonGap + moonRadius;
                        // DrawTextLine suma Abs(Ascent) al Y pasado (23 + 17.2 = 40.2 de línea base).
                        // Alineamos la luna exactamente al centro visual de las mayúsculas (baseline - CapHeight / 2).
                        float baselineY = 23f + Math.Abs(small.Metrics.Ascent);
                        float moonCenterY = baselineY - (small.Metrics.CapHeight / 2f);
                        DrawMoon(canvas, moonCenterX, moonCenterY, moonRadius);
                    }
                }
            }
        }
    }
}
