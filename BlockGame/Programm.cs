using Basics.EngineStates;

namespace Basics;

/// <summary>
/// Einstiegspunkt der Anwendung. Initialisiert den <see cref="StateManager"/> und startet die Engine.
/// </summary>
public class Programm
{
    /// <summary>
    /// Startet die Anwendung durch Initialisierung des <see cref="StateManager"/> und Aufruf von <see cref="StateManager.Run"/>.
    /// </summary>
    /// <param name="args">Befehlszeilenargumente (nicht verwendet).</param>
    private static void Main(string[] args)
    {
        StateManager stateManager = new StateManager();
        stateManager.Run();
    }
}