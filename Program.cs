using BarRaider.SdTools;

namespace RazerBattery
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            // Stream Deck lanza este exe con los argumentos de conexión.
            SDWrapper.Run(args);
        }
    }
}
