using Basics.Window;
using Egui;

namespace Basics.Game.UI
{
    public class UIManager
    {
        private ChunkRequestor _chunkRequestor;

        public UIManager(ChunkRequestor requestor)
        {
            _chunkRequestor = requestor;
        }

        // Diese Methode wird aus dem Integration.Run() aufgerufen
        public void Draw(Context ctx)
        {
            
            new Egui.Containers.Area("crosshair")
                .Anchor(Align2.CenterCenter, new EVec2(0, 0))
                .Show(ctx, ui => { ui.Label("+"); });

            
            new Egui.Containers.Window("Engine Settings")
                .Resizable((true, true)) // Du kannst hier vorher noch Optionen setzen!
                .Show(ctx, ui =>
                {
                    ui.Heading("EdwinCraft Debug");

                    int dist = GameSettings.RenderDistance;
                    
                    if (ui.Add(new Egui.Widgets.Slider<int>(ref dist, 1, 256).Text("Render Distance")).Changed)
                    {
                        GameSettings.SetRenderDistance(dist);
                    }

                    if (ui.Button("Unload All Chunks").Clicked)
                    {
                        _chunkRequestor.UnloadAllChunks();
                    }
                });
            
            new Egui.Containers.Window("PlayerSettings")
                .Resizable((true, true))
                .Show(ctx, ui =>
                {
                    ui.Heading("Player Settings");

                    float speed = GameSettings.PlayerMoveSpeed;
                    
                    if (ui.Add(new Egui.Widgets.Slider<float>(ref speed, 1f, 100f).Text("Player Speed")).Changed)
                    {
                        GameSettings.SetPlayerMoveSpeed(speed);
                    }
                    
                    float sensitivity = GameSettings.MouseSensitivity;
                    if (ui.Add(new Egui.Widgets.Slider<float>(ref sensitivity, 0.01f, 1f).Text("Mouse Sensitivity")).Changed)
                    {
                        GameSettings.SetMouseSensitivity(sensitivity);
                    }
                });
            
            new Egui.Containers.Window("PlayerInformation")
                .Resizable((true, true))
                .Show(ctx, ui =>
                {
                    ui.Heading("Player Information");
                    ui.Label(
                        " X: " + MainClass.PlayerCamera.Position.X +
                                " Y: " + MainClass.PlayerCamera.Position.Y +
                                " Z: " + MainClass.PlayerCamera.Position.Z);
                    ui.Label("Current Chunk:" + MainClass.PlayerCamera.currentChunkCoord);
                    ui.Label("FPS: " + (WindowSetup.Window.FramesPerSecond).ToString("F2"));
                });
        }
    }
}