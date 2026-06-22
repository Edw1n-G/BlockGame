using Basics.Window;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;

namespace Basics.EngineStates
{
    /**
     * Entry Point des Programms
     */
    public class StateManager
    {
        private Game _game;
        
        private IStates _nextState;
        private IStates _currentState;
        
        private GL _gl = null!;
        private IInputContext _inputContext = null!;
        
        /**
        * Startpunkt des Programms
        * Fenster erstellen und Events registrieren
        */
        public void Run()
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

        private void onLoad()
        {
            _gl = WindowSetup.Window.CreateOpenGL();
            _inputContext = WindowSetup.Window.CreateInput();
            
            _currentState = new EngineStates.Menu();
            _currentState.Enter(_gl, _inputContext, this);
        }

        private void onRender(double delta)
        {
            _currentState.Render(delta);
        }

        private void onUpdate(double delta)
        {
            _currentState.Update(delta);
            
            if (_nextState != null)
            {
                _currentState.Exit();
                _currentState = _nextState;
                _currentState.Enter(_gl, _inputContext, this);
                _nextState = null;
            }
        }

        private void onFramebufferResize(Vector2D<int> newSize)
        {
            _currentState.FramebufferResize(newSize);
        }

        public void StateChange(IStates newState)
        {
            _nextState = newState;
        }
        
        public void CloseEngine()
        {
            _gl.Dispose();
        }
    }
}