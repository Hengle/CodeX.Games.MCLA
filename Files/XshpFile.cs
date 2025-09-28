using CodeX.Core.Engine;
using CodeX.Core.Utilities;
using CodeX.Forms.Utilities;
using CodeX.Games.MCLA.RPF3;
using CodeX.Games.MCLA.RSC5;

namespace CodeX.Games.MCLA.Files
{
    public class XshpFile(Rpf3FileEntry file) : PiecePack(file)
    {
        public Rsc5Bitmap Bitmap = null;
        public Rsc5City City = null;
        public Rsc5TextureDictionary CityTextures = null;

        public JenkHash Hash = JenkHash.GenHash(file?.NameLower ?? "");
        public string Name = file?.NameLower;
        public bool DependenciesLoaded = false;

        public override void Load(byte[] data)
        {
            if (FileInfo is not Rpf3ResourceFileEntry e)
            {
                return;
            }

            var r = new Rsc5DataReader(e, data);
            var ident = (Rsc5XshpType)r.ReadUInt32();
            r.Position = Rpf3Crypto.VIRTUAL_BASE;

            switch (ident)
            {
                case Rsc5XshpType.BITMAP_VINYL:
                case Rsc5XshpType.BITMAP_TIRE:
                    Bitmap = r.ReadBlock<Rsc5Bitmap>();
                    break;
                case Rsc5XshpType.CITY:
                    City = r.ReadBlock<Rsc5City>();
                    break;
                case Rsc5XshpType.CITY_TEXTURE:
                    CityTextures = r.ReadBlock<Rsc5TextureDictionary>();
                    break;
                default:
                    break;
            }

            Pieces = [];
            if (Bitmap != null)
            {
                var tex1 = Bitmap?.Texture1.Item;
                var tex2 = Bitmap?.Texture2.Item;
                var txp = new TexturePack(e) { Textures = [] };

                if (tex1 != null)
                {
                    txp.Textures[tex1.Name] = tex1;
                    tex1.Pack = txp;
                }
                if (tex2 != null)
                {
                    txp.Textures[tex2.Name] = tex2;
                    tex2.Pack = txp;
                }

                var texCount = txp.Textures?.Count ?? 0;
                if (texCount > 0)
                {
                    Piece = new Piece { TexturePack = txp };
                    Pieces.Add(e.ShortNameLower, Piece);
                }
                MessageBoxEx.Show(null, $"Detected XSHP bitmap file ({texCount} texture{(texCount > 1 ? "s" : "")} & 0 model)");
            }
            else if (City != null)
            {
                var drawable = City.Drawable.Item;
                if (drawable != null)
                {
                    Piece = drawable;
                    Pieces.Add(e.ShortNameLower, drawable);
                }
            }
            else if (CityTextures != null)
            {
                var hashes = CityTextures.HashTable.Items;
                var textures = CityTextures.Textures.Items;
                var txp = new TexturePack(e) { Textures = [] };

                if (textures != null && hashes != null)
                {
                    for (int i = 0; i < textures.Length; i++)
                    {
                        var tex = textures[i];
                        var hash = hashes[i];

                        tex.Name ??= hash.ToString();
                        txp.Textures[hash.ToString()] = tex;
                        tex.Pack = txp;
                    }
                }

                var texCount = txp.Textures?.Count ?? 0;
                if (texCount > 0)
                {
                    Piece = new Piece { TexturePack = txp };
                    Pieces.Add(e.NameLower, Piece);
                }
            }
        }

        public override byte[] Save()
        {
            return null;
        }
    }

    public enum Rsc5XshpType : uint
    {
        BITMAP_VINYL = 0x40CC5600,
        BITMAP_TIRE = 0x9CC65600,
        CITY = 0x10B75C00,
        CITY_TEXTURE = 0xEC505900,
        FILESET = 0x509A5C00,
        UNKNOWN = 0x3CAF5C00
    }
}