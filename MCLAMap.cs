using CodeX.Core.Engine;
using CodeX.Core.Numerics;
using CodeX.Core.Utilities;
using CodeX.Games.MCLA.Files;
using CodeX.Games.MCLA.RPF3;
using System.Collections.Generic;
using System.Numerics;

namespace CodeX.Games.MCLA
{
    public class MCLAMap : StreamingLevel<MCLAGame, MCLAMapFileCache, Rpf3FileManager>
    {
        public List<Rpf3Entry> CityFiles;
        public Dictionary<JenkHash, MCLAMapNode> MapNodeDict;
        public Dictionary<JenkHash, MCLAMapNode> StreamNodesPrev;
        public Dictionary<JenkHash, MCLAMapNode> StreamNodes;

        public static readonly Setting EnabledSetting = Settings.Register("MCLAMap.Enabled", SettingType.Bool, true, true);
        public static readonly Setting StartPositionSetting = Settings.Register("MCLAMap.StartPosition", SettingType.Vector3, new Vector3(150.0f, -350.0f, 30.0f));

        public Statistic NodeCountStat = Statistics.Register("MCLAMap.NodeCount", StatisticType.Counter);
        public Statistic EntityCountStat = Statistics.Register("MCLAMap.EntityCount", StatisticType.Counter);

        public MCLAMap(MCLAGame game) : base(game, "MCLA Map Level")
        {
            this.Game = game;
            this.DefaultSpawnPoint = StartPositionSetting.GetVector3();
            this.BoundingBox = new BoundingBox(new Vector3(-100000.0f), new Vector3(100000.0f));
            this.InitRenderData();
        }

        protected override bool StreamingInit()
        {
            Core.Engine.Console.Write("MCLAMap", "Initialising " + this.Game.Name + "...");
            this.FileManager = Game.GetFileManager() as Rpf3FileManager;

            if (this.FileManager == null)
            {
                throw new Exception("Failed to initialize MCLA.");
            }

            if (EnabledSetting.GetBool() == false)
            {
                Cache = new MCLAMapFileCache(FileManager);
                return true;
            }

            var dfm = this.FileManager.DataFileMgr;
            this.Cache = new MCLAMapFileCache(this.FileManager);
            this.MapNodeDict = [];
            this.StreamNodes = [];
            this.StreamNodesPrev = [];
            this.StreamPosition = this.DefaultSpawnPoint;

            dfm.LoadCityFiles(this.Cache);

            foreach (var kv in dfm.XshpFiles)
            {
                var node = new MCLAMapNode(kv.Value);
                this.MapNodeDict[node.NameHash] = node;
            }

            this.StreamBVH = new StreamingBVH();
            foreach (var kvp in this.MapNodeDict)
            {
                var mapnode = kvp.Value;
                if (mapnode.StreamingBox.Minimum != mapnode.StreamingBox.Maximum)
                {
                    this.StreamBVH.Add(mapnode);
                }
            }

            Core.Engine.Console.Write("MCLAMap", FileManager.Game.Name + " map initialised.");
            return true;
        }

        protected override bool StreamingUpdate()
        {
            if (this.StreamNodes == null) return false;
            if (EnabledSetting.GetBool() == false) return false;

            var nodes = this.StreamNodesPrev;
            var ents = this.StreamEntities.CurrentSet;
            var spos = this.StreamPosition;

            this.StreamNodesPrev = this.StreamNodes;
            nodes.Clear();

            foreach (var kvp in this.MapNodeDict)
            {
                var node = kvp.Value;
                if (!nodes.ContainsKey(node.NameHash))
                {
                    nodes[node.NameHash] = node;
                }

                if (node?.MapData?.Entities != null)
                {
                    foreach (var e in node.MapData.Entities)
                    {
                        RecurseAddStreamEntity(e, ref spos, ents);
                    }
                }
            }

            var needsAnotherUpdate = false;
            foreach (var ent in ents.ToList()) //Make sure all current entities assets are loaded
            {
                var hash = new JenkHash(ent.Level.Name);
                var pp = Cache.GetPiecePack(this.FileManager, hash, Rpf3ResourceType.BitMap);
                var changed = pp?.Piece != ent.Piece;
                
                ent.Piece = pp?.Piece;
                if (pp?.Piece != null && changed)
                {
                    if (pp.Piece.Lods != null)
                    {
                        var ld = 0.0f;
                        for (int i = 0; i < pp.Piece.Lods.Length; i++)
                        {
                            if (pp.Piece.Lods[i] != null)
                            {
                                ld = Math.Max(ld, pp.Piece.Lods[i].LodDist);
                            }
                        }
                        ent.LodDistMax = ld;
                    }
                }
            }

            this.NodeCountStat.SetCounter(nodes.Count);
            this.EntityCountStat.SetCounter(ents.Count);

            this.StreamNodes = nodes;
            if (needsAnotherUpdate) this.StreamUpdateRequest = true;

            return true;
        }

        private static void RecurseAddStreamEntity(Entity e, ref Vector3 spos, HashSet<Entity> ents)
        {
            e.StreamingDistance = (e.Position - spos).Length();
            if (e.BoundingBox.Contains(ref spos) == ContainmentType.Contains)
            {
                e.StreamingDistance = 0.0f;
            }

            if ((e.StreamingDistance < e.LodDistMax) && ((e.LodChildren == null) || (e.StreamingDistance >= e.LodDistMin)))
            {
                ents.Add(e);
            }
        }
    }

    public class MCLAMapNode : StreamingBVHItem
    {
        public BoundingBox StreamingBox { get; set; }
        public BoundingBox BoundingBox { get; set; }

        public MCLAMapNode ParentNode;
        public MCLAMapData MapData;
        public JenkHash NameHash;
        public bool Enabled;

        public MCLAMapNode(XshpFile xshp)
        {
            MapData = new MCLAMapData(xshp);
            NameHash = xshp.Hash;
            BoundingBox = MapData.BoundingBox;
            StreamingBox = MapData.StreamingBox;
            Enabled = true;
        }

        public override string ToString()
        {
            return NameHash.ToString();
        }
    }

    public class MCLAMapData : Level
    {
        public MCLAMapData(XshpFile xshp)
        {
            FilePack = xshp;
            FilePack.EditorObject = this;
            Name = xshp.Name;

            var dist = new Vector3(2000.0f);
            BoundingBox = xshp.Piece.BoundingBox;
            StreamingBox = new BoundingBox(BoundingBox.Center - dist, BoundingBox.Center + dist);

            var e = new Entity()
            {
                Position = BoundingBox.Center,
                BoundingBox = BoundingBox,
                LodLevel = 0,
                LodDistMax = 2000.0f,
                Index = Entities.Count,
                Level = this
            };
            this.Add(e);
        }

        public override string ToString()
        {
            return Name;
        }
    }

    public class MCLAMapFileCache(Rpf3FileManager fman) : StreamingCache
    {
        public Rpf3FileManager FileManager = fman;
        private readonly Dictionary<Rpf3ResourceType, StreamingCacheDict<JenkHash>> Cache = new();

        public Dictionary<JenkHash, StreamingCacheEntry> GetCache(Rpf3ResourceType ext)
        {
            if (!Cache.TryGetValue(ext, out var cache))
            {
                cache = new StreamingCacheDict<JenkHash>(this);
                Cache[ext] = cache;
            }
            return cache;
        }

        public override void Invalidate(string gamepath)
        {
            if (string.IsNullOrEmpty(gamepath)) return;
            Rpf3FileManager.GetRpf3FileHashExt(gamepath, out var hash, out var ext);
            Cache.TryGetValue(ext, out var cache);
            cache?.Remove(hash);
        }

        public override void BeginFrame()
        {
            base.BeginFrame();
            foreach (var cache in Cache)
            {
                cache.Value.RemoveOldItems();
            }
        }

        public PiecePack GetPiecePack(Rpf3FileManager fm, JenkHash hash, Rpf3ResourceType ext)
        {
            var cache = GetCache(ext);
            if (!cache.TryGetValue(hash, out var cacheItem))
            {
                cacheItem = new StreamingCacheEntry();
                var entry = FileManager.DataFileMgr.TryGetStreamEntry(hash, ext);

                if (entry != null)
                {
                    Core.Engine.Console.Write("MCLAMap", entry.Name);
                    try
                    {
                        var piecePack = FileManager.LoadPiecePack(entry, null, true);
                        cacheItem.Object = piecePack;
                    }
                    catch { }
                }
            }
            else if (cacheItem.Object is XshpFile xshp && !xshp.DependenciesLoaded)
            {
                fm.LoadDependencies(xshp);
                xshp.DependenciesLoaded = true;
            }

            cacheItem.LastUseFrame = CurrentFrame;
            cache[hash] = cacheItem;
            return cacheItem.Object as PiecePack;
        }
    }
}