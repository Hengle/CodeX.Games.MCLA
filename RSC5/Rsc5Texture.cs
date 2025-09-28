using CodeX.Core.Engine;
using CodeX.Core.Utilities;
using CodeX.Games.MCLA.RPF3;
using SharpDX.Direct3D11;

namespace CodeX.Games.MCLA.RSC5
{
    public class Rsc5TextureDictionary : Rsc5FileBase
    {
        public override ulong BlockLength => 32;
        public Rsc5Ptr<Rsc5BlockMap> BlockMapPointer { get; set; }
        public uint ParentDictionary { get; set; } //Always 0 in file
        public uint UsageCount { get; set; } //Always 1 in file
        public Rsc5Arr<JenkHash> HashTable { get; set; }
        public Rsc5PtrArr<Rsc5Texture> Textures { get; set; }

        public Dictionary<JenkHash, Rsc5Texture> Dict = [];

        public override void Read(Rsc5DataReader reader)
        {
            base.Read(reader);
            BlockMapPointer = reader.ReadPtr<Rsc5BlockMap>();
            ParentDictionary = reader.ReadUInt32();
            UsageCount = reader.ReadUInt32();
            HashTable = reader.ReadArr<JenkHash>();
            Textures = reader.ReadPtrArr<Rsc5Texture>();

            if (Textures.Items != null)
            {
                var imax = Math.Min(HashTable.Items.Length, Textures.Items.Length);
                for (int i = 0; i < Textures.Items.Length; i++)
                {
                    var t = Textures.Items[i];
                    var h = (i < imax) ? HashTable.Items[i] : 0;
                    Dict[h] = t;
                }
            }
        }

        public override void Write(Rsc5DataWriter writer)
        {
            base.Write(writer);
            writer.WritePtr(BlockMapPointer);
            writer.WriteUInt32(ParentDictionary);
            writer.WriteUInt32(UsageCount);
            writer.WriteArr(HashTable);
            writer.WritePtrArr(Textures);
        }
    }

    public class Rsc5Bitmap : Rsc5FileBase
    {
        public override ulong BlockLength => 16;
        public Rsc5Ptr<Rsc5BlockMap> BlockMapPointer { get; set; }
        public Rsc5Ptr<Rsc5Texture> Texture1 { get; set; }
        public Rsc5Ptr<Rsc5Texture> Texture2 { get; set; }

        public override void Read(Rsc5DataReader reader)
        {
            base.Read(reader);
            BlockMapPointer = reader.ReadPtr<Rsc5BlockMap>();
            Texture1 = reader.ReadPtr<Rsc5Texture>();
            Texture2 = reader.ReadPtr<Rsc5Texture>();
        }

        public override void Write(Rsc5DataWriter writer)
        {
            base.Write(writer);
            writer.WritePtr(BlockMapPointer);
            writer.WritePtr(Texture1);
            writer.WritePtr(Texture2);
        }
    }

    public class Rsc5Texture : Rsc5TextureBase
    {
        public override ulong BlockLength => 80;
        public Rsc5TextureType TextureType { get; set; } //0 = normal, 1 = cube, 3 = volume
        public float ColorExpR { get; set; } = 1.0f; //m_ColorExprR
        public float ColorExpG { get; set; } = 1.0f; //m_ColorExprG
        public float ColorExpB { get; set; } = 1.0f; //m_ColorExprB
        public float ColorOfsR { get; set; } = 0.0f; //m_ColorOfsR
        public float ColorOfsG { get; set; } = 0.0f; //m_ColorOfsG
        public float ColorOfsB { get; set; } = 0.0f; //m_ColorOfsB
        public int Size { get; set; }

        public override void Read(Rsc5DataReader reader)
        {
            base.Read(reader);
            if (D3DBaseTexture.Item == null)
            {
                return;
            }

            Stride = reader.ReadUInt16();
            TextureType = (Rsc5TextureType)reader.ReadByte();
            MipLevels = reader.ReadByte();
            ColorExpR = reader.ReadSingle(); //1.0f
            ColorExpG = reader.ReadSingle(); //1.0f
            ColorExpB = reader.ReadSingle(); //1.0f
            ColorOfsR = reader.ReadSingle(); //0.0f
            ColorOfsG = reader.ReadSingle(); //0.0f
            ColorOfsB = reader.ReadSingle(); //0.0f

            reader.Position = D3DBaseTexture.Item.FilePosition + 0x20;
            var d3dValue = reader.ReadInt32();
            var virtualW = Rpf3Crypto.GetVirtualSize(Width);
            var virtualH = Rpf3Crypto.GetVirtualSize(Height);
            reader.Position = (ulong)Rpf3Crypto.GetBaseAdressFromDirect3D(d3dValue, reader);

            Format = ConvertToEngineFormat((Rsc5TextureFormat)(d3dValue & byte.MaxValue));
            Size = CalcDataSize(virtualW, virtualH);
            Data = reader.ReadBytes(Size);
            Sampler = TextureSampler.Create(TextureSamplerFilter.Anisotropic, TextureAddressMode.Wrap);
            Data = Rpf3Crypto.UnswizzleXbox360Data(Data, Width, Height, Format);
            Size = Data.Length; //In case of virtual dimensions, get the actual texture size
        }

        public override void Write(Rsc5DataWriter writer)
        {
            base.Write(writer);
            throw new NotImplementedException();
        }

        public enum Rsc5TextureType
        {
            Normal = 0,
            Cube = 1,
            Volume = 3
        };

        private int CalcDataSize(int width, int height)
        {
            var num = Format switch
            {
                TextureFormat.BC1 => width * height / 2,
                TextureFormat.BC2 or TextureFormat.BC3 or TextureFormat.A8R8G8B8 or TextureFormat.L8 => width * height,
                _ => throw new NotImplementedException()
            };
            return num;
        }

        public static TextureFormat ConvertToEngineFormat(Rsc5TextureFormat f)
        {
            return f switch
            {
                Rsc5TextureFormat.D3DFMT_DXT3 => TextureFormat.BC2,
                Rsc5TextureFormat.D3DFMT_DXT5 => TextureFormat.BC3,
                Rsc5TextureFormat.D3DFMT_A8R8G8B8 => TextureFormat.A8R8G8B8,
                Rsc5TextureFormat.D3DFMT_L8 => TextureFormat.L8,
                _ => TextureFormat.BC1,
            };
        }

        public static Rsc5TextureFormat ConvertToRsc5Format(TextureFormat f)
        {
            return f switch
            {
                TextureFormat.BC2 => Rsc5TextureFormat.D3DFMT_DXT3,
                TextureFormat.BC3 => Rsc5TextureFormat.D3DFMT_DXT5,
                TextureFormat.A8R8G8B8 => Rsc5TextureFormat.D3DFMT_A8R8G8B8,
                TextureFormat.L8 => Rsc5TextureFormat.D3DFMT_L8,
                _ => Rsc5TextureFormat.D3DFMT_DXT1,
            };
        }

        public override string ToString()
        {
            return "Texture: " + Width.ToString() + "x" + Height.ToString() + ": " + Name;
        }
    }

    public class Rsc5TextureBase : Texture, Rsc5Block
    {
        public virtual ulong BlockLength => 80;
        public ulong FilePosition { get; set; }
        public bool IsPhysical => false;
        public uint VFT { get; set; }
        public uint Unknown_4h { get; set; } = 1;
        public uint Unknown_8h { get; set; }
        public uint Unknown_Ch { get; set; }
        public uint Unknown_10h { get; set; }
        public uint Unknown_14h { get; set; }
        public Rsc5Str TextureName { get; set; }
        public Rsc5Ptr<Rsc5BlockMap> D3DBaseTexture { get; set; }

        public virtual void Read(Rsc5DataReader reader)
        {
            VFT = reader.ReadUInt32();
            Unknown_4h = reader.ReadUInt32();
            Unknown_8h = reader.ReadUInt32();
            Unknown_Ch = reader.ReadUInt32();
            Unknown_10h = reader.ReadUInt32();
            Unknown_14h = reader.ReadUInt32();
            TextureName = reader.ReadStr();
            D3DBaseTexture = reader.ReadPtr<Rsc5BlockMap>();

            Name = TextureName.Value?.Replace(".dds", "").Replace("pack:/", "") ?? null;
            if (D3DBaseTexture.Item == null)
            {
                return;
            }

            Width = reader.ReadUInt16();
            Height = reader.ReadUInt16();
        }

        public virtual void Write(Rsc5DataWriter writer)
        {
            throw new NotImplementedException();
        }

        public override string ToString()
        {
            return "TextureBase: " + Name;
        }
    }

    public class Rsc5TextureData : Rsc5Block
    {
        public ulong BlockLength { get; set; }
        public ulong FilePosition { get; set; }
        public bool IsPhysical => true;
        public byte[] Data { get; set; }

        public void Read(Rsc5DataReader reader)
        {
            Data = reader.ReadBytes((int)BlockLength);
        }

        public void Write(Rsc5DataWriter writer)
        {
            writer.WriteBytes(Data);
        }
    }

    public enum Rsc5TextureFormat : uint
    {
        D3DFMT_L8 = 2,
        D3DFMT_DXT1 = 82,
        D3DFMT_DXT3 = 83,
        D3DFMT_DXT5 = 84,
        D3DFMT_A8R8G8B8 = 134
    }
}