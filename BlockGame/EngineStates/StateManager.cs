using Basics.Window;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;

namespace Basics.EngineStates
{
    /// <summary>
    /// Entry Point des Programms. Verwaltet den Wechsel zwischen verschiedenen Spielzuständen.
    /// </summary>
    public class StateManager
    {
        /// <summary>
        /// Die aktuelle Game-Instanz.
        /// </summary>
        private Game _game;
        
        /// <summary>
        /// Der nächste zu ladende Spielzustand.
        /// </summary>
        private IStates _nextState;
        
        /// <summary>
        /// Der aktuell aktive Spielzustand.
        /// </summary>
        private IStates _currentState;
        
        /// <summary>
        /// Der OpenGL-Kontext für Render-Operationen.
        /// </summary>
        private GL _gl = null!;
        
        /// <summary>
        /// Der Eingabe-Kontext für Input-Verarbeitung.
        /// </summary>
        private IInputContext _inputContext = null!;
        
        /// <summary>
        /// Startpunkt des Programms.
        /// Erstellt das Fenster und registriert alle Event-Handler.
        /// </summary>
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

        /// <summary>
        /// Wird aufgerufen, wenn das Fenster geladen ist. Initialisiert OpenGL und den ersten Spielzustand.
        /// </summary>
        private void onLoad()
        {
            _gl = WindowSetup.Window.CreateOpenGL();
            _inputContext = WindowSetup.Window.CreateInput();
            
            _currentState = new EngineStates.Menu();
            _currentState.Enter(_gl, _inputContext, this);
        }

        /// <summary>
        /// Wird während des Render-Loops aufgerufen. Delegiert an den aktuellen Spielzustand.
        /// </summary>
        /// <param name="delta">Die Zeit in Sekunden seit dem letzten Frame.</param>
        private void onRender(double delta)
        {
            _currentState.Render(delta);
        }

        /// <summary>
        /// Wird während des Update-Loops aufgerufen. Führt Logik aus und prüft auf Zustandswechsel.
        /// </summary>
        /// <param name="delta">Die Zeit in Sekunden seit dem letzten Frame.</param>
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

        /// <summary>
        /// Wird bei einer Größenänderung des Framebuffers aufgerufen. Delegiert an den aktuellen Spielzustand.
        /// </summary>
        /// <param name="newSize">Die neue Größe des Framebuffers.</param>
        private void onFramebufferResize(Vector2D<int> newSize)
        {
            _currentState.FramebufferResize(newSize);
        }

        /// <summary>
        /// Plant einen Zustandswechsel für das nächste Update.
        /// Der Wechsel erfolgt am Ende des aktuellen Frames, um Inkonsistenzen zu vermeiden.
        /// </summary>
        /// <param name="newState">Der neue Spielzustand.</param>
        public void StateChange(IStates newState)
        {
            _nextState = newState;
        }
        
        /// <summary>
        /// Schließt die Engine und gibt alle Ressourcen frei.
        /// </summary>
        public void CloseEngine()
        {
            _gl.Dispose();
        }
    }
}