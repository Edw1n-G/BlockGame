using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;

namespace Basics.EngineStates;

public interface IStates
{
    void Enter(GL gl, IInputContext inputContext); //State Laden
    void Update(double delta); //Für Logik
    void Render(double delta);
    void FramebufferResize(Vector2D<int> newSize);
    void Exit(); //State verlassen
}