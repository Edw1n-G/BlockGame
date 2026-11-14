using Basics.States;

namespace Basics;

/// <summary>
/// Einstiegspunkt der Anwendung. Initialisiert den <see cref="StateManager"/> und startet die Engine.
/// </summary>
public class Programm
{
    /// <summary>
    /// Startet die Anwendung durch Initialisierung des <see cref="StateManager"/> und Aufruf von <see cref="StateManager.Run"/>.
    /// </summary>
    /// <param name="args">Optional benchmark command and settings.</param>
    private static void Main(string[] args)
    {
        try
        {
            if (Basics.Diagnostics.FrameBenchmarkCommand.Handle(args)) return;
            StateManager stateManager = new StateManager();
            stateManager.Run();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            Environment.ExitCode = 1;
        }
    }
}