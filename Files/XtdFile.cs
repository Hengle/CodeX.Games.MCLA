using CodeX.Core.Engine;
using CodeX.Games.MCLA.RPF3;
using CodeX.Games.MCLA.RSC5;

namespace CodeX.Games.MCLA.Files
{
    class XtdFile(Rpf3FileEntry file) : TexturePack(file)
    {
        public Rsc5TextureDictionary TextureDictionary = null;

        public override void Load(byte[] data)
        {
            var e = FileInfo as Rpf3ResourceFileEntry;
            var r = new Rsc5DataReader(e, data, Core.Utilities.DataEndianess.BigEndian);

            TextureDictionary = r.ReadBlock<Rsc5TextureDictionary>();
            Textures = [];

            if (TextureDictionary?.Textures.Items != null)
            {
                foreach (var tex in TextureDictionary.Textures.Items)
                {
                    Textures[tex.Name] = tex;
                }
            }
        }

        public override byte[] Save()
        {
            return null;
        }

        public override void BuildFromTextureList(List<Texture> textures)
        {
            base.BuildFromTextureList(textures);
        }
    }
}