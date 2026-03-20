using Basics.EngineStates;

namespace Basics
{
    /**
     * Entry Point des Programms
     */
    public class Programm
    {
        private static EngineStates.Game _game;

        private static void Main(string[] args)
        {
            _game = new EngineStates.Game();
            _game.Run();
        }
    }
}