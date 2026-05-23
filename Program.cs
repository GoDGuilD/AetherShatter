using System;

namespace AetherShatter
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            using var game = new AetherShatterGame();
            game.Run();
        }
    }
}
