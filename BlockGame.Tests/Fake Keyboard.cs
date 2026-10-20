using System.Numerics;
using Silk.NET.Core;
using Silk.NET.Input;

namespace BlockGame.Tests;

public class FakeKeyboard : IKeyboard
{
    private readonly HashSet<Key> _pressedKeys = new();

    public void PressKey(Key key) => _pressedKeys.Add(key);
    public void ReleaseKey(Key key) => _pressedKeys.Remove(key);
    public bool IsKeyPressed(Key key) => _pressedKeys.Contains(key);

    public event Action<IKeyboard, Key, int>? KeyDown;
    public event Action<IKeyboard, Key, int>? KeyUp;
    public event Action<IKeyboard, char>? KeyChar;

    public IReadOnlyList<Key> SupportedKeys { get; } = Enum.GetValues<Key>();
    public string ClipboardText { get; set; } = string.Empty;
    public string Name => "FakeKeyboard";
    public int Index => 0;
    public bool IsConnected => true;
    
    public void BeginInput() { }
    public void EndInput() { }
    public bool IsScancodePressed(int scancode) => false;
}

public class FakeCursor : ICursor
{
    public Vector2 Position { get; set; }
    public CursorMode CursorMode { get; set; }
    public StandardCursor StandardCursor { get; set; }
    public CursorType Type { get; set; } = CursorType.Standard;
    
    public int HotspotX { get; set; }
    public int HotspotY { get; set; }
    public RawImage Image { get; set; }
    public bool IsConfined { get; set; }

    public bool IsSupported(CursorMode mode) => true;
    public bool IsSupported(StandardCursor cursor) => true;
}

public class FakeMouse : IMouse
{
    private readonly HashSet<MouseButton> _pressedButtons = new();

    public FakeCursor FakeCursor { get; } = new();
    public ICursor Cursor => FakeCursor;

    public void PressButton(MouseButton button) => _pressedButtons.Add(button);
    public void ReleaseButton(MouseButton button) => _pressedButtons.Remove(button);
    public bool IsButtonPressed(MouseButton button) => _pressedButtons.Contains(button);

    public Vector2 Position { get; set; }

    public event Action<IMouse, MouseButton>? MouseDown;
    public event Action<IMouse, MouseButton>? MouseUp;
    public event Action<IMouse, MouseButton, Vector2>? Click;
    public event Action<IMouse, MouseButton, Vector2>? DoubleClick;
    public event Action<IMouse, Vector2>? MouseMove;
    public event Action<IMouse, ScrollWheel>? Scroll;

    public IReadOnlyList<ScrollWheel> ScrollWheels { get; } = new List<ScrollWheel> { new(0, 0) };
    public IReadOnlyList<MouseButton> SupportedButtons { get; } = Enum.GetValues<MouseButton>();
    
    public int DoubleClickTime { get; set; } = 500;
    public int DoubleClickRange { get; set; } = 4;
    public string Name => "FakeMouse";
    public int Index => 0;
    public bool IsConnected => true;
    
    // Hilfsmethode für Tests, um OnMouseClick direkt auszulösen
    public void RaiseClick(MouseButton button, Vector2 pos) => Click?.Invoke(this, button, pos);
}