using System.Numerics;
using Basics.Game.Input;
using Silk.NET.Input;

namespace BlockGame.Tests;

public class InputManagerTests
{
    private static (InputManager manager, FakeKeyboard keyboard, FakeMouse mouse) CreateInitializedManager()
    {
        var manager = new InputManager();
        var keyboard = new FakeKeyboard();
        var mouse = new FakeMouse();
        manager.InitializeDevices(keyboard, mouse);
        return (manager, keyboard, mouse);
    }

    // ---------- Grundlegendes Verhalten ohne Geräte ----------

    [Fact]
    public void IsActionPressed_NoDevicesInitialized_ReturnsFalse()
    {
        var manager = new InputManager();

        Assert.False(manager.IsActionPressed(Actions.Forward));
    }

    // ---------- Default-Keybindings ----------

    [Fact]
    public void IsActionPressed_DefaultForwardBinding_ReflectsKeyboardState()
    {
        var (manager, keyboard, _) = CreateInitializedManager();

        Assert.False(manager.IsActionPressed(Actions.Forward));

        keyboard.PressKey(Key.W);

        Assert.True(manager.IsActionPressed(Actions.Forward));
    }

    [Fact]
    public void IsActionPressed_DefaultMouseBinding_ReflectsMouseState()
    {
        var (manager, _, mouse) = CreateInitializedManager();

        Assert.False(manager.IsActionPressed(Actions.DestroyBlock));

        mouse.PressButton(MouseButton.Left);

        Assert.True(manager.IsActionPressed(Actions.DestroyBlock));
    }

    [Fact]
    public void IsActionPressed_KeyboardBindingTakesPrecedenceOverMouse()
    {
        // Dokumentiert aktuelles Verhalten: Sobald eine Aktion IRGENDEINE
        // Tastenbindung hat, wird die Maus gar nicht erst geprüft - selbst
        // wenn die Taste gerade nicht gedrückt ist. Das ist vermutlich kein
        // Problem bei den aktuellen Default-Bindings (Overlap existiert nicht),
        // aber wichtig zu weißen, falls man das später ändert.
        var (manager, keyboard, mouse) = CreateInitializedManager();
        manager.SetkeyBindings(Actions.DestroyBlock, Key.X); // künstlicher Overlap zum Testen

        mouse.PressButton(MouseButton.Left); // Maus-Binding wäre erfüllt
        // Taste X NICHT gedrückt

        Assert.False(manager.IsActionPressed(Actions.DestroyBlock));
    }

    // ---------- SetkeyBindings ----------

    [Fact]
    public void SetkeyBindings_OverridesExistingBinding_OldKeyNoLongerTriggers()
    {
        var (manager, keyboard, _) = CreateInitializedManager();
        manager.SetkeyBindings(Actions.Forward, Key.I);

        keyboard.PressKey(Key.W); // alte Standard-Taste

        Assert.False(manager.IsActionPressed(Actions.Forward));
    }

    [Fact]
    public void SetkeyBindings_OverridesExistingBinding_NewKeyTriggers()
    {
        var (manager, keyboard, _) = CreateInitializedManager();
        manager.SetkeyBindings(Actions.Forward, Key.I);

        keyboard.PressKey(Key.I);

        Assert.True(manager.IsActionPressed(Actions.Forward));
    }

    [Fact]
    public void SetkeyBindings_NewActionNotPreviouslyBound_AddsSuccessfully()
    {
        var manager = new InputManager();

        var exception = Record.Exception(() => manager.SetkeyBindings(Actions.PlaceBlock, Key.G));

        Assert.Null(exception);
    }

    // ---------- Action-Bindings über KeyDown ----------

    [Fact]
    public void KeyDown_BoundKey_InvokesRegisteredCallback()
    {
        var (manager, keyboard, _) = CreateInitializedManager();
        int callCount = 0;
        manager.SetActionBindings(Actions.Forward, () => callCount++);

        manager.KeyDown(keyboard, Key.W, 0);

        Assert.Equal(1, callCount);
    }

    [Fact]
    public void KeyDown_UnboundKey_DoesNotThrow_AndInvokesNothing()
    {
        var (manager, keyboard, _) = CreateInitializedManager();
        int callCount = 0;
        manager.SetActionBindings(Actions.Forward, () => callCount++);

        var exception = Record.Exception(() => manager.KeyDown(keyboard, Key.Z, 0));

        Assert.Null(exception);
        Assert.Equal(0, callCount);
    }

    [Fact]
    public void KeyDown_KeyBoundButNoActionCallbackRegistered_DoesNotThrow()
    {
        var (manager, keyboard, _) = CreateInitializedManager();
        // Forward (W) ist standardmäßig als Key gebunden, aber ohne Callback
        // (außer ToggleMouseLock registriert Initialize/InitializeDevices nichts anderes automatisch).

        var exception = Record.Exception(() => manager.KeyDown(keyboard, Key.W, 0));

        Assert.Null(exception);
    }

    [Fact]
    public void KeyDown_TwoDifferentActionsBoundToSameKey_BothCallbacksFire()
    {
        var (manager, keyboard, _) = CreateInitializedManager();
        manager.SetkeyBindings(Actions.ToggleDebugCamera, Key.Space); // teilt sich Space mit "Up"
        int upCount = 0;
        int debugCount = 0;
        manager.SetActionBindings(Actions.Up, () => upCount++);
        manager.SetActionBindings(Actions.ToggleDebugCamera, () => debugCount++);

        manager.KeyDown(keyboard, Key.Space, 0);

        Assert.Equal(1, upCount);
        Assert.Equal(1, debugCount);
    }

    // ---------- Mehrere Events innerhalb eines Ticks ----------

    [Fact]
    public void KeyDown_MultipleDifferentKeysInSequence_EachActionFiresIndependently()
    {
        var (manager, keyboard, _) = CreateInitializedManager();
        int forwardCount = 0, leftCount = 0, rightCount = 0;
        manager.SetActionBindings(Actions.Forward, () => forwardCount++);
        manager.SetActionBindings(Actions.Left, () => leftCount++);
        manager.SetActionBindings(Actions.Right, () => rightCount++);

        // Simuliert mehrere Tasten-Events innerhalb eines einzigen Frames/Ticks
        manager.KeyDown(keyboard, Key.W, 0);
        manager.KeyDown(keyboard, Key.A, 0);
        manager.KeyDown(keyboard, Key.D, 0);

        Assert.Equal(1, forwardCount);
        Assert.Equal(1, leftCount);
        Assert.Equal(1, rightCount);
    }

    [Fact]
    public void KeyDown_SameKeyFiredMultipleTimesInOneTick_CallbackFiresEveryTime()
    {
        // Dokumentiert aktuelles Verhalten: Es gibt kein Debouncing. Jeder
        // KeyDown-Aufruf feuert den Callback erneut - relevant z.B. bei
        // OS-Key-Repeat (gehaltene Taste sendet wiederholt KeyDown ohne KeyUp
        // dazwischen). Siehe ToggleMouseLock-Test weiter unten für die
        // konkrete Konsequenz davon.
        var (manager, keyboard, _) = CreateInitializedManager();
        int callCount = 0;
        manager.SetActionBindings(Actions.Forward, () => callCount++);

        manager.KeyDown(keyboard, Key.W, 0);
        manager.KeyDown(keyboard, Key.W, 0);
        manager.KeyDown(keyboard, Key.W, 0);

        Assert.Equal(3, callCount);
    }

    [Fact]
    public void KeyDown_RapidPressAndRebindBetweenCalls_UsesBindingActiveAtCallTime()
    {
        var (manager, keyboard, _) = CreateInitializedManager();
        int oldActionCount = 0, newActionCount = 0;
        manager.SetActionBindings(Actions.Forward, () => oldActionCount++);
        manager.SetActionBindings(Actions.Backward, () => newActionCount++);

        manager.KeyDown(keyboard, Key.W, 0); // Forward (Standard-Bindung zu diesem Zeitpunkt)
        manager.SetkeyBindings(Actions.Backward, Key.W); // W jetzt zusätzlich an Backward gebunden
        manager.KeyDown(keyboard, Key.W, 0); // sollte jetzt BEIDE Actions triggern

        Assert.Equal(2, oldActionCount);
        Assert.Equal(1, newActionCount);
    }

    // ---------- Maus-Events ----------

    [Fact]
    public void OnMouseClick_BoundButton_InvokesRegisteredCallback()
    {
        var (manager, _, mouse) = CreateInitializedManager();
        int callCount = 0;
        manager.SetActionBindings(Actions.DestroyBlock, () => callCount++);

        manager.OnMouseClick(mouse, MouseButton.Left, Vector2.Zero);

        Assert.Equal(1, callCount);
    }

    [Fact]
    public void OnMouseClick_UnboundButton_DoesNotThrow()
    {
        var (manager, _, mouse) = CreateInitializedManager();

        var exception = Record.Exception(() => manager.OnMouseClick(mouse, MouseButton.Middle, Vector2.Zero));

        Assert.Null(exception);
    }

    [Fact]
    public void OnMouseMove_RaisesMouseMovedEvent_WithSamePosition()
    {
        var (manager, _, mouse) = CreateInitializedManager();
        Vector2? received = null;
        manager.MouseMoved += pos => received = pos;
        var expectedPosition = new Vector2(123f, 456f);

        manager.OnMouseMove(mouse, expectedPosition);

        Assert.Equal(expectedPosition, received);
    }

    [Fact]
    public void OnMouseMove_MultipleRapidMovements_EventFiresForEachWithCorrectValue()
    {
        var (manager, _, mouse) = CreateInitializedManager();
        var received = new List<Vector2>();
        manager.MouseMoved += pos => received.Add(pos);

        manager.OnMouseMove(mouse, new Vector2(1, 1));
        manager.OnMouseMove(mouse, new Vector2(2, 2));
        manager.OnMouseMove(mouse, new Vector2(3, 3));

        Assert.Equal(3, received.Count);
        Assert.Equal(new Vector2(1, 1), received[0]);
        Assert.Equal(new Vector2(2, 2), received[1]);
        Assert.Equal(new Vector2(3, 3), received[2]);
    }

    [Fact]
    public void OnMouseMove_NoSubscribers_DoesNotThrow()
    {
        var (manager, _, mouse) = CreateInitializedManager();

        var exception = Record.Exception(() => manager.OnMouseMove(mouse, Vector2.Zero));

        Assert.Null(exception);
    }

    // ---------- ToggleMouseLock ----------

    [Fact]
    public void Initialize_MouseLockStartsTrue()
    {
        var (manager, _, _) = CreateInitializedManager();

        Assert.True(manager.IsMouseLocked);
    }

    [Fact]
    public void ToggleMouseLock_ViaDefaultKeyBinding_FlipsLockState()
    {
        var (manager, keyboard, _) = CreateInitializedManager();
        Assert.True(manager.IsMouseLocked);

        manager.KeyDown(keyboard, Key.F, 0); // Default-Bindung für ToggleMouseLock

        Assert.False(manager.IsMouseLocked);
    }

    [Fact]
    public void ToggleMouseLock_PressedTwiceInOneTick_ReturnsToOriginalState()
    {
        // Wichtiger Fund: Ohne KeyUp-Handling zwischen den Presses würde ein
        // vom Betriebssystem wiederholt gesendetes KeyDown (Key-Repeat bei
        // gehaltener Taste) die Maussperre bei JEDEM Repeat-Event umschalten,
        // nicht nur beim initialen physischen Tastendruck. Dieser Test zeigt
        // das aktuelle Verhalten explizit auf - der Mangel an "wurde die Taste
        // gerade NEU gedrückt oder nur weiter gehalten"-Erkennung ist ein
        // potenzieller Bug für spätere Untersuchung, kein Testfehler.
        var (manager, keyboard, _) = CreateInitializedManager();

        manager.KeyDown(keyboard, Key.F, 0);
        manager.KeyDown(keyboard, Key.F, 0);

        Assert.True(manager.IsMouseLocked); // zurück zum Ursprungszustand
    }

    [Fact]
    public void KeyUp_CurrentlyNoOp_DoesNotThrowAndDoesNotAffectState()
    {
        var (manager, keyboard, _) = CreateInitializedManager();
        bool lockStateBefore = manager.IsMouseLocked;

        var exception = Record.Exception(() => manager.KeyUp(keyboard, Key.F, 0));

        Assert.Null(exception);
        Assert.Equal(lockStateBefore, manager.IsMouseLocked);
    }

    // ---------- Mehrfache unabhängige Instanzen ----------
    // Testet direkt den Fix des eingangs erwähnten static-Bugs.

    [Fact]
    public void TwoIndependentInstances_DoNotShareBindingsOrState()
    {
        var manager1 = new InputManager();
        var keyboard1 = new FakeKeyboard();
        var mouse1 = new FakeMouse();
        manager1.InitializeDevices(keyboard1, mouse1);

        var manager2 = new InputManager();
        var keyboard2 = new FakeKeyboard();
        var mouse2 = new FakeMouse();
        manager2.InitializeDevices(keyboard2, mouse2);

        manager1.SetkeyBindings(Actions.Forward, Key.I); // nur auf Instanz 1 ändern

        keyboard2.PressKey(Key.W); // Standard-Bindung auf Instanz 2 sollte unverändert sein

        Assert.True(manager2.IsActionPressed(Actions.Forward));
    }

    [Fact]
    public void TwoIndependentInstances_CanBothBeInitializedWithoutException()
    {
        // Wäre vor dem static-Fix mit ArgumentException fehlgeschlagen,
        // da DefaultKeyBindings() versucht hätte, bereits vorhandene
        // Dictionary-Keys erneut hinzuzufügen.
        var exception = Record.Exception(() =>
        {
            var m1 = new InputManager();
            m1.InitializeDevices(new FakeKeyboard(), new FakeMouse());

            var m2 = new InputManager();
            m2.InitializeDevices(new FakeKeyboard(), new FakeMouse());
        });

        Assert.Null(exception);
    }
}