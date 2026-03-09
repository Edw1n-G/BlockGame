namespace Basics.Game.TerrainManaging
{
    /// <summary>
    /// Alle Klassen können jetzt die welt bearbeiten über ChunkProvider,
    /// ohne eine Referenz auf die eigentliche Instanz zu haben.
    /// </summary>
    public static class World
    {
        private static ChunkProvider _chunkProvider;

        public static void Initialize(ChunkProvider provider)
        {
            _chunkProvider = provider;
        }

        
        // --- Global API ---
        // Skripte einfach World.ModifyBlock
        public static void ModifyBlock(int x, int y, int z, byte blockId)
        {
            _chunkProvider.ModifyBlock(x, y, z, blockId);
        }

        public static byte GetBlock(int x, int y, int z)
        {
            return _chunkProvider.GetBlockAt(x, y, z);
        }
    }
}