namespace Basics.EngineStates;

public interface IStates
{
    void Enter(); //State Laden
    void Update(float delta); //Für Logik
    void Render();
    void Exit(); //State verlassen
}