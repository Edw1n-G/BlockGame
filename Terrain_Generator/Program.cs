namespace Basics
{
    /**
     * Entry Point des Programms
     */
    public class Programm
    {
        private static MainClass _game;

        private static void Main(string[] args)
        {
            _game = new MainClass();
            _game.Run();
        }
    }
}