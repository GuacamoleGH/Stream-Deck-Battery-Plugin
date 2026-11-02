using BarRaider.SdTools;

namespace RazerBattery
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            // Stream Deck launches this executable with the connection arguments.
            SDWrapper.Run(args);
        }
    }
}
