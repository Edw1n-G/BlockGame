using System.Collections.Immutable;
using Egui;
using Egui.Silk.NET;
using Silk.NET.Input;
using Silk.NET.Windowing;

namespace Basics.Game.Input;

/// <summary>
/// Dünner Wrapper um <see cref="SilkGlIntegration"/>, der Mausbewegungen
/// unterdrückt, solange der <see cref="InputManager"/> die Maus gesperrt hat
/// </summary>
public sealed class GameEguiIntegration : IDisposable
{
    private readonly SilkGlIntegration _integration;
    private readonly InputManager _inputManager;

    public GameEguiIntegration(
        Context context,
        IWindow window,
        IInputContext input,
        InputManager inputManager)
    {
        _integration = new SilkGlIntegration(context, window, input);
        _inputManager = inputManager;
    }

    public void Run(Action<Ui> uiAction)
    {
        if (_inputManager.IsMouseLocked)
        {
            RemovePointerMovedEvents();
        }

        _integration.Run(uiAction);
    }

    /// <summary>
    /// Entfernt alle <see cref="Event.PointerMoved"/>-Events aus dem
    /// gesammelten <see cref="RawInput"/>, bevor Egui sie verarbeitet.
    /// </summary>
    private void RemovePointerMovedEvents()
    {
        var events = _integration._rawInput.Events;
        if (events.IsDefaultOrEmpty) return;

        var builder = ImmutableArray.CreateBuilder<Event>(events.Length);
        foreach (var e in events)
        {
            if (e.Inner is Event.PointerMoved) continue;
            builder.Add(e);
        }

        _integration._rawInput.Events = builder.ToImmutable();
    }

    public void Dispose() => _integration.Dispose();
}