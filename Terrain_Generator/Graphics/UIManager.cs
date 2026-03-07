using Egui;
using Basics.Game;
using Basics.Window;

namespace Basics.Graphics
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

                    int dist = _chunkRequestor.RenderDistance;
                    
                    if (ui.Add(new Egui.Widgets.Slider<int>(ref dist, 1, 256).Text("Render Distance")).Changed)
                    {
                        _chunkRequestor.RenderDistance = dist;
                    }

                    if (ui.Button("Force Chunk Update").Clicked)
                    {
                        System.Console.WriteLine("Chunks neu laden...");
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