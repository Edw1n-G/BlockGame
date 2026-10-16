using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;

namespace Basics.EngineStates;

/// <summary>
/// Interface für alle Spielzustände (Menu, Game, etc.).
/// Definiert den Lebenszyklus eines Spielzustands.
/// </summary>
public interface IStates
{
    /// <summary>
    /// Wird aufgerufen, wenn der Zustand aktiviert wird.
    /// Initialisiert OpenGL, Input und StateManager.
    /// </summary>
    /// <param name="gl">Der OpenGL-Kontext.</param>
    /// <param name="inputContext">Der Eingabe-Kontext.</param>
    /// <param name="manager">Der StateManager für Zustandswechsel.</param>
    void Enter(GL gl, IInputContext inputContext, StateManager manager);
    
    /// <summary>
    /// Wird in jedem Update-Frame aufgerufen für Logik (Kollisionserkennung, Bewegung, etc.).
    /// </summary>
    /// <param name="delta">Die Zeit in Sekunden seit dem letzten Frame.</param>
    void Update(double delta);
    
    /// <summary>
    /// Wird in jedem Render-Frame aufgerufen für Zeichenoperationen.
    /// </summary>
    /// <param name="delta">Die Zeit in Sekunden seit dem letzten Frame.</param>
    void Render(double delta);
    
    /// <summary>
    /// Wird bei einer Größenänderung des Framebuffers aufgerufen.
    /// </summary>
    /// <param name="newSize">Die neue Größe des Framebuffers.</param>
    void FramebufferResize(Vector2D<int> newSize);
    
    /// <summary>
    /// Wird aufgerufen, wenn der Zustand verlassen wird.
    /// Bereitet das Aufräumen von Ressourcen vor.
    /// </summary>
    void Exit();
}