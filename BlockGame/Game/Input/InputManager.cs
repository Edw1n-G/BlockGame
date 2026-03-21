using System.Collections.Generic;
using System.Numerics;
using Silk.NET.Windowing;
using Basics.Game.Player;

namespace Basics.Input;

// Fenstereinstelungen
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
    Backward,
    ToggleMouseLock,
    DestroyBlock,
    PlaceBlock
}

public class InputManager
{
    private static IKeyboard _keyboard;
    private static IMouse _mouse;
    
    private static Boolean _isMouseLocked = false;
    public static bool IsMouseLocked => _isMouseLocked; //getter um ui interaktion zu locken
    
    private static PlayerMovement? _playerMovement;
    
    //TODO: InputManager komplett vom Spieler trennen damit es nur als Translation von Input zu Aktionen dient.
    public static void SetPlayerMovement(PlayerMovement playerMovement)
    {
        _playerMovement = playerMovement;
    }

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
            _isMouseLocked = true;
            _mouse.MouseMove += OnMouseMove;
            _mouse.Scroll += OnMouseWheel;
            _mouse.Click += OnMouseClick;
        }
        
        DefaultKeyBindings();
        DefaultMouseBindings();
        
        SetActionBindings(Actions.ToggleMouseLock, ToggleMouseLock);
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
        _keyBindings.Add(Actions.ToggleMouseLock, Key.F);
        
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
    
    private static Dictionary<Actions, MouseButton> _mouseBindings = new Dictionary<Actions, MouseButton>();
    
    private static void DefaultMouseBindings()
    {
        _mouseBindings.Add(Actions.DestroyBlock, MouseButton.Left);
        _mouseBindings.Add(Actions.PlaceBlock, MouseButton.Right);
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
        
        if (_mouse != null && _mouseBindings.TryGetValue(action, out MouseButton button))
        {
            if (_mouse.IsButtonPressed(button)) return true;
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
        //TODO: Herausfinden ob ich das hier haben will
        _playerMovement?.LookUpdate(position);
    }

    private static unsafe void OnMouseWheel(IMouse mouse, ScrollWheel scrollWheel)
    {
        
    }
    
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
    
    private static void ToggleMouseLock()
    {
        if (_mouse == null) return;
        
        if (_isMouseLocked)
        {
            _mouse.Cursor.CursorMode = CursorMode.Normal; //Mauszeiger sichtbar und begrenzt
            _isMouseLocked = false;
        }
        else
        {
            _mouse.Cursor.CursorMode = CursorMode.Raw; //Mauszeiger unsichtbar und unbegrenzt
            _isMouseLocked = true;
        }
    }
}