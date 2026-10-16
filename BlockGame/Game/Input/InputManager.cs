using System.Collections.Generic;
using System.Numerics;
using Silk.NET.Windowing;
using Basics.Game.Player;

namespace Basics.Input;

using Silk.NET.Input; // Für die Tastatureingabe
using System.Linq;
using System;

/// <summary>
/// Definiert alle verfügbaren Aktionen, die durch Tasten oder Maus ausgelöst werden können.
/// </summary>
public enum Actions
{
    /// <summary>Beendet die Anwendung.</summary>
    Close,
    /// <summary>Schaltet den Vollbildmodus ein/aus.</summary>
    Fullscreen,
    /// <summary>Schaltet den randlosen Vollbildmodus ein/aus.</summary>
    Borderless,
    /// <summary>Schaltet die Debug-Kamera ein/aus.</summary>
    ToogleDebugCamera,
    /// <summary>Bewegt den Spieler nach oben.</summary>
    Up,
    /// <summary>Bewegt den Spieler nach unten.</summary>
    Down,
    /// <summary>Bewegt den Spieler nach links.</summary>
    Left,
    /// <summary>Bewegt den Spieler nach rechts.</summary>
    Right,
    /// <summary>Bewegt den Spieler vorwärts.</summary>
    Forward,
    /// <summary>Bewegt den Spieler rückwärts.</summary>
    Backward,
    /// <summary>Sperrt/entsperrt die Maus.</summary>
    ToggleMouseLock,
    /// <summary>Zerstört den Block in der Blickrichtung.</summary>
    DestroyBlock,
    /// <summary>Plaziert einen Block in der Blickrichtung.</summary>
    PlaceBlock
}

/// <summary>
/// Verwaltet alle Eingabeoperationen (Tastatur und Maus) und bindet sie an Aktionen.
/// </summary>
/// <remarks>
/// Der InputManager übersetzt physische Tasten- und Mausbewegungen in semantische Aktionen,
/// die von anderen Systemen (Player, UI, etc.) verwendet werden können.
/// </remarks>
public class InputManager
{
    private static IKeyboard _keyboard;
    private static IMouse _mouse;
    
    /// <summary>Gibt an, ob die Maus aktuell gesperrt ist (unbegrenzt im Raum).</summary>
    private static Boolean _isMouseLocked = false;
    /// <summary>Die aktuelle Sperrung der Maus.</summary>
    public static bool IsMouseLocked => _isMouseLocked;
    
    /// <summary>Das für die Spielerbewegung verwendete <see cref="PlayerMovement"/> oder null.</summary>
    private static PlayerMovement? _playerMovement;
    
    /// <summary>
    /// Legt das für die Spielerbewegung verwendete <see cref="PlayerMovement"/> fest.
    /// </summary>
    /// <param name="playerMovement">Das festzulegende <see cref="PlayerMovement"/>.</param>
    public static void SetPlayerMovement(PlayerMovement playerMovement)
    {
        _playerMovement = playerMovement;
    }

    /// <summary>
    /// Initialisiert den InputManager mit den bereitgestellten Eingabegeräten.
    /// Bindet Tastatur- und Maus-Ereignisse an die internen Handler.
    /// </summary>
    /// <param name="input">Der Eingabe-Kontext mit verfügbaren Tastatur- und Mausgeräten.</param>
    public static void Initialize(IInputContext input)
    {
        _keyboard = input.Keyboards.FirstOrDefault();
        if (_keyboard != null)
        {
            _keyboard.KeyDown += KeyDown;
            _keyboard.KeyUp += KeyUp;
        }
        
        _mouse = input.Mice.FirstOrDefault();
        if (_mouse != null)
        {
            _mouse.Cursor.CursorMode = CursorMode.Raw; // Mauszeiger unsichtbar und unbegrenzt
            _isMouseLocked = true;
            _mouse.MouseMove += OnMouseMove;
            _mouse.Scroll += OnMouseWheel;
            _mouse.Click += OnMouseClick;
        }
        
        DefaultKeyBindings();
        DefaultMouseBindings();
        
        SetActionBindings(Actions.ToggleMouseLock, ToggleMouseLock);
    }
    
    // Funktionen zum Verwalten der Tastenbelegung Mapping Key -> Aktion
    // ==========================================================
    /// <summary>Dictionary für die Tastenbelegung (Aktion → Taste).</summary>
    private static Dictionary<Actions, Key> _keyBindings = new Dictionary<Actions, Key>();
    
    /// <summary>Fügt die Standard-Tastenbelegungen in das <see cref="_keyBindings"/> Dictionary ein.</summary>
    private static void DefaultKeyBindings()
    {
        _keyBindings.Add(Actions.Close, Key.Escape);
        _keyBindings.Add(Actions.Fullscreen, Key.F11);
        _keyBindings.Add(Actions.Borderless, Key.F12);
        _keyBindings.Add(Actions.ToogleDebugCamera, Key.F1);
        _keyBindings.Add(Actions.Up, Key.Space);
        _keyBindings.Add(Actions.Down, Key.ShiftLeft);
        _keyBindings.Add(Actions.Left, Key.A);
        _keyBindings.Add(Actions.Right, Key.D);
        _keyBindings.Add(Actions.Forward, Key.W);
        _keyBindings.Add(Actions.Backward, Key.S);
        _keyBindings.Add(Actions.ToggleMouseLock, Key.F);
    }
    
    /// <summary>Aktualisiert die Tastenbelegung für eine Aktion.</summary>
    /// <param name="action">Die Aktion, deren Belegung geändert werden soll.</param>
    /// <param name="key">Die neue Taste für diese Aktion.</param>
    public static void SetkeyBindings(Actions action, Key key)
    {
        if (_keyBindings.ContainsKey(action))
        {
            _keyBindings[action] = key;
        }
        else
        {
            _keyBindings.Add(action, key);
        }
    }
    
    /// <summary>Dictionary für die Maustastenbelegung (Aktion → Maustaste).</summary>
    private static Dictionary<Actions, MouseButton> _mouseBindings = new Dictionary<Actions, MouseButton>();
    
    /// <summary>Fügt die Standard-Mausbelegungen in das <see cref="_mouseBindings"/> Dictionary ein.</summary>
    private static void DefaultMouseBindings()
    {
        _mouseBindings.Add(Actions.DestroyBlock, MouseButton.Left);
        _mouseBindings.Add(Actions.PlaceBlock, MouseButton.Right);
    }
    // ==========================================================
    
    // Mapping Aktion → Methode (Action = void Methode, Actions = Enum)
    // ==========================================================
    /// <summary>Dictionary für die Aktionen-Zuordnung (Aktion → Methode).</summary>
    private static Dictionary<Actions, Action> _actionBindings = new Dictionary<Actions, Action>();
    
    /// <summary>Fügt oder aktualisiert die Methode für eine Aktion hinzu.</summary>
    /// <param name="action">Die Aktion, deren Methode gesetzt werden soll.</param>
    /// <param name="method">Die für diese Aktion aufzurufende <see cref="Action"/>.</param>
    public static void SetActionBindings(Actions action, Action method)
    {
        if (_actionBindings.ContainsKey(action))
        {
            _actionBindings[action] = method;
        }
        else
        {
            _actionBindings.Add(action, method);
        }
    }
    // ==========================================================
    
    // Funktionen zum Abfragen von Tasten
    // ==========================================================
    /// <summary>Prüft, ob eine Taste auf der Tastatur gedrückt ist.</summary>
    /// <param name="keyboard">Die zu überprüfende <see cref="IKeyboard"/>.</param>
    /// <param name="key">Die zu prüfende <see cref="Key"/>.</param>
    /// <returns><c>true</c>, wenn die Taste gedrückt ist, andernfalls <c>false</c>.</returns>
    public static bool IsKeyPressed(IKeyboard keyboard, Key key)
    {
        if (_keyboard == null) return false;
        return _keyboard.IsKeyPressed(key);
    }
    
    /// <summary>Prüft, ob eine Aktion (durch Taste oder Maus) ausgeführt wird.</summary>
    /// <param name="action">Die zu prüfende <see cref="Actions"/>.</param>
    /// <returns><c>true</c>, wenn die Aktion ausgeführt wird, andernfalls <c>false</c>.</returns>
    public static bool IsActionPressed(Actions action)
    {
        if (_keyboard == null) return false;
        if (_keyBindings.TryGetValue(action, out Key key))
        {
            return _keyboard.IsKeyPressed(key);
        }
        
        if (_mouse != null && _mouseBindings.TryGetValue(action, out MouseButton button))
        {
            if (_mouse.IsButtonPressed(button)) return true;
        }
        
        return false;
    }
    // ==========================================================
    
    
    // Funktionen zum Verarbeiten von Tasten
    /// <summary>Handler für Tastatur-KeyDown-Ereignisse.</summary>
    /// <param name="keyboard">Die auslösende <see cref="IKeyboard"/>.</param>
    /// <param name="key">Die gedrückte <see cref="Key"/>.</param>
    /// <param name="arg3">Weitere Daten (nicht verwendet).</param>
    private static void KeyDown(IKeyboard keyboard, Key key, int arg3)
    {
        // Wir suchen rückwärts: Welche Aktion gehört zu dieser Taste?
        foreach (var binding in _keyBindings)
        {
            if (binding.Value == key) 
            {
                // Wenn wir für diese Aktion Methoden registriert haben → Ausführen!
                if (_actionBindings.TryGetValue(binding.Key, out Action callback))
                {
                    callback?.Invoke();
                }
            }
        }
    }

    /// <summary>Handler für Tastatur-KeyUp-Ereignisse (aktuell nicht implementiert).</summary>
    /// <param name="keyboard">Die auslösende <see cref="IKeyboard"/>.</param>
    /// <param name="key">Die losgelassene <see cref="Key"/>.</param>
    /// <param name="arg3">Weitere Daten (nicht verwendet).</param>
    private static void KeyUp(IKeyboard keyboard, Key key, int arg3)
    {
        
    }
    
    // Funktionen zum Verarbeiten von Mausbewegungen
    /// <summary>Handler für Mausbewegungs-Ereignisse.</summary>
    /// <param name="mouse">Die auslösende <see cref="IMouse"/>.</param>
    /// <param name="position">Die aktuelle Mausposition.</param>
    private static void OnMouseMove(IMouse mouse, Vector2 position)
    {
        _playerMovement?.LookUpdate(position);
    }

    /// <summary>Handler für Maus-Scroll-Ereignisse (aktuell nicht implementiert).</summary>
    /// <param name="mouse">Die auslösende <see cref="IMouse"/>.</param>
    /// <param name="scrollWheel">Die ScrollWheel-Informationen.</param>
    private static unsafe void OnMouseWheel(IMouse mouse, ScrollWheel scrollWheel)
    {
        
    }
    
    /// <summary>Handler für Maus-Klick-Ereignisse.</summary>
    /// <param name="mouse">Die auslösende <see cref="IMouse"/>.</param>
    /// <param name="button">Die gedrückte <see cref="MouseButton"/>.</param>
    /// <param name="pos">Die Mausposition beim Klick.</param>
    private static void OnMouseClick(IMouse mouse, MouseButton button, Vector2 pos)
    {
        // Check if the clicked button is bound to an action
        foreach (var binding in _mouseBindings)
        {
            if (binding.Value == button)
            {
                // If yes, invoke the corresponding logic
                if (_actionBindings.TryGetValue(binding.Key, out Action callback))
                {
                    callback?.Invoke();
                }
            }
        }
    }
    
    /// <summary>Schaltet die Mauspfeil-Sperrung um (sichtbar/unbegrenzt).</summary>
    private static void ToggleMouseLock()
    {
        if (_mouse == null) return;
        
        if (_isMouseLocked)
        {
            _mouse.Cursor.CursorMode = CursorMode.Normal; // Mauszeiger sichtbar und begrenzt
            _isMouseLocked = false;
        }
        else
        {
            _mouse.Cursor.CursorMode = CursorMode.Raw; // Mauszeiger unsichtbar und unbegrenzt
            _isMouseLocked = true;
        }
    }
}