using Basics.Game.Utilities;

namespace BlockGame.Tests;

public class CoreBoundaryTests
{
    [Fact]
    public void CoreDoesNotReferenceGraphicsLibraries()
    {
        string[] graphicsPrefixes =
        [
            "Silk.NET.OpenGL", "Silk.NET.Windowing", "Silk.NET.Input", "Silk.NET.GLFW",
            "Egui", "System.Drawing", "StbImageSharp"
        ];

        Assert.DoesNotContain(typeof(ChunkCoord).Assembly.GetReferencedAssemblies(),
            reference => graphicsPrefixes.Any(prefix => reference.Name!.StartsWith(prefix, StringComparison.Ordinal)));
    }
}
