using CodeX.Core.Engine;
using CodeX.Games.MCLA.RPF3;
using CodeX.Games.MCLA.RSC5;

namespace CodeX.Games.MCLA.Files
{
    public class DrawableFile(Rpf3FileEntry file) : PiecePack(file)
    {
        public Rsc5DrawableBase Drawable { get; set; } = null;

        public override void Load(byte[] data)
        {
            if (FileInfo is not Rpf3ResourceFileEntry e) return;

            var r = new Rsc5DataReader(e, data);
            Drawable = r.ReadBlock<Rsc5DrawableBase>();
            Pieces = [];

            if (Drawable != null)
            {
                Piece = Drawable;
                Pieces.Add(e.ShortNameLower, Drawable);
            }
        }

        public override byte[] Save()
        {
            return null;
        }
    }
}