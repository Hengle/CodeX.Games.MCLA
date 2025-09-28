using CodeX.Core.Engine;
using CodeX.Games.MCLA.RPF3;
using CodeX.Games.MCLA.RSC5;

namespace CodeX.Games.MCLA.Files
{
    internal class XapbFile(Rpf3FileEntry file) : PiecePack(file)
    {
        public Rsc5AmbientDrawablePed Drawable;
        public static List<Rsc5Texture> Textures;

        public override void Load(byte[] data)
        {
            var e = FileInfo as Rpf3ResourceFileEntry;
            var r = new Rsc5DataReader(e, data);

            Drawable = r.ReadBlock<Rsc5AmbientDrawablePed>();
            Pieces = [];

            var drawable = Drawable?.Drawable.Item;
            if (drawable != null)
            {
                Piece = drawable;
                Piece.Name = e.Name;
                Piece.FilePack = this;
                Pieces.Add(e.ShortNameHash, Piece);
            }
        }

        public override byte[] Save()
        {
            return null;
        }
    }
}