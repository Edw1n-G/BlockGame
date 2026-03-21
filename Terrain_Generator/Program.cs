using Basics.EngineStates;
using Basics.Window;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;

namespace Basics
{
    /**
     * Entry Point des Programms
     */
    public class Programm
    {
        private static EngineStates.Game _game;
        
        private static IStates _nextState;
        private static IStates _currentState;
        
        private static GL _gl = null!;
        private static IInputContext _inputContext = null!;

        private static void Main(string[] args)
        {
            Run();
        }
        
        /**
        * Startpunkt des Programms
        * Fenster erstellen und Events registrieren
        */
        public static void Run()
        {
            //Fenster erstellen und die Event-Handler registrieren
            WindowSetup.CreateWindow();
        
            WindowSetup.Window.Load += onLoad;
            WindowSetup.Window.Render += onRender;
            WindowSetup.Window.Update += onUpdate;
            WindowSetup.Window.FramebufferResize += onFramebufferResize;
        
            //Fenster starten und Haupt-thread
            WindowSetup.Run();
        
            WindowSetup.Window.Dispose();
        }

        private static void onLoad()
        {
            _gl = WindowSetup.Window.CreateOpenGL();
            _inputContext = WindowSetup.Window.CreateInput();
            
            _currentState = new EngineStates.Game();
            _currentState.Enter(_gl, _inputContext);
        }

        private static void onRender(double delta)
        {
            _currentState.Render(delta);
        }

        private static void onUpdate(double delta)
        {
            _currentState.Update(delta);
        }

        private static void onFramebufferResize(Vector2D<int> newSize)
        {
            _currentState.FramebufferResize(newSize);
        }
    }
}