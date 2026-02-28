using System.Numerics;
using Silk.NET.Windowing;

namespace Basics.Input;
using Basics.Setup; // Fenstereinstelungen
using Silk.NET.Input; //Für die Tastatureingabe
using System.Linq;
using System;

//Liste an Aktionen die durch Tasten getriggert werden können.
public enum Actions
{
    Close,
    Fullscreen,
    Borderless,
    ToogleDebugCamera,
    Up,
    Down,
    Left,
    Right,
    Forward,
    Backward
}

public class InputManager
{
    private static IKeyboard _keyboard;
    
    private static IMouse _mouse;
    
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
            _mouse.Cursor.CursorMode = CursorMode.Raw; //Mauszeiger unsichtbar und unbegrenzt
            _mouse.MouseMove += OnMouseMove;
            _mouse.Scroll += OnMouseWheel;
        }
        
        DefaultKeyBindings();
    }
    
    //Funktionen zum Verwalten der Tastenbelegung Mapping Key -> Aktion
    //==========================================================
    private static Dictionary<Actions, Key> _keyBindings = new Dictionary<Actions, Key>(); //leere Dictionary
    
    private static void DefaultKeyBindings() //Dictionary Füllen mit Standardbelegung
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
    }
    
    public static void SetkeyBindings(Actions action, Key key) //Dictonary Updaten
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
    //==========================================================
    
    //Mapping Aktion -> Methode (Action = void Methode, Actions = Enum)
    //==========================================================
    private static Dictionary<Actions, Action> _actionBindings = new Dictionary<Actions, Action>();
    
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
    //==========================================================
    
    //Funktionen zum Abfragen von Tasten
    //==========================================================
    public static bool IsKeyPressed(IKeyboard keyboard, Key key)
    {
        if (_keyboard == null) return false;
        return _keyboard.IsKeyPressed(key);
    }
    
    public static bool IsActionPressed(Actions action)
    {
        if (_keyboard == null) return false;
        if (_keyBindings.TryGetValue(action, out Key key))
        {
            return _keyboard.IsKeyPressed(key);
        }
        return false;
    }
    //==========================================================
    
    
    // Funktionen zum Verarbeiten von Tasten
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

    private static void KeyUp(IKeyboard keyboard, Key key, int arg3)
    {
        
    }
    
    //Funktionen zum Verarbeiten von Mausbewegungen
    private static void OnMouseMove(IMouse mouse, Vector2 position)
    {
        // Updating instantly. Nicht ideal sollte per Frame passieren.
        Movement.LookUpdate(position);
    }

    private static unsafe void OnMouseWheel(IMouse mouse, ScrollWheel scrollWheel)
    {
        
    }
}