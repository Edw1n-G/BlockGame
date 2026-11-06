using BlockGame.Benchmarking;

namespace BlockGame.Workloads;

public static class ScenarioContracts
{
    public static bool NeedsNative(string family) => family is "mesh" or "terrain" or "noise";
    public static ScenarioContract Create(string family, string scenario, object? dependencies = null, object? extraParameters = null)
    {
        object Coordinates(SelectionTarget t) => new {x=t.Center.X,y=t.Center.Y,z=t.Center.Z,horizontal=t.Horizontal,vertical=t.Vertical,lod=0};
        object inputs; string unit; string timing="normal";
        switch (family)
        {
            case "selection-full":
                var (center,h,v)=WorkloadCatalog.Selection(scenario);
                inputs=new {x=center.X,y=center.Y,z=center.Z,horizontal=h,vertical=v,lod=0,storage="fresh complete HashSet"};
                unit="one-fresh-complete-set"; break;
            case "selection-movement":
                inputs=new {initial=Coordinates(SelectionMovementWorkload.Start),path=SelectionMovementWorkload.Path(scenario).Select(Coordinates).ToArray(),
                    setup="one complete warm path",includes="selection, set difference, active-set updates",excludes="provider, locks, tasks"};
                unit="one-closed-movement-path"; if(scenario=="same") timing="early-return"; break;
            case "mesh":
                string? frozen=null;
                if(scenario=="frozen-terrain")
                {
                    using var stream=typeof(MeshWorkload).Assembly.GetManifestResourceStream("BlockGame.Workloads.FrozenTerrain.json")!;
                    frozen=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream));
                }
                inputs=new {scenario,lod=0,center=new {x=0,y=0,z=0},neighbors=26,frozen,
                    pool="representative warmed provider lists, return inside operation",dependencies};
                unit="one-lod0-build-and-return-buffers"; break;
            case "terrain":
                var t=WorkloadCatalog.Terrain(scenario);
                inputs=new {seed=t.Seed,x=t.Coord.X,y=t.Coord.Y,z=t.Coord.Z,lod=0,mapSize=100,nodeTree=WorkloadCatalog.NodeTree,dependencies};
                unit="one-generated-chunk"; if(t.Kind=="outside") timing="diagnostic"; break;
            case "noise":
                var n=WorkloadCatalog.Noise(scenario);
                inputs=new {seed=n.Seed,n.X,n.Y,n.Z,sizeX=16,sizeY=17,sizeZ=16,step=1,nodeTree=WorkloadCatalog.NodeTree,dependencies};
                unit="one-noise-grid-with-array"; break;
            default: throw new ArgumentException("Unknown scenario family: "+family);
        }
        if(NeedsNative(family) && dependencies is null) throw new InvalidOperationException("Native/content identity required for "+family);
        return ScenarioContract.Create(family+"/"+scenario,1,unit,new {definition=inputs,parameters=extraParameters},timing);
    }
}
