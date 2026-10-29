using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FlowerPainter.Core.Layers.Types.Floral
{
    // The game save is compressed already. Store each distinct native geometry once.
    internal static class FlowerPatchArchive
    {
        internal sealed class Entry
        {
            public string Name;
            public string Metadata;
            public byte[] Geometry;
            public byte[] LodGeometry;
        }

        private const int Version = 1;

        internal static long EntrySize(string name, string metadata, long geometry, long lod)
        {
            long TextSize(string value) {
                int n=Encoding.UTF8.GetByteCount(value), prefix=1;
                for(int v=n;v>=128;v>>=7)prefix++;
                return (long)n+prefix;
            }
            return TextSize(name)+TextSize(metadata)+8+geometry+lod;
        }

        internal static void WriteEntry(BinaryWriter writer, Entry entry)
        {
            writer.Write(entry.Name);writer.Write(entry.Metadata);
            WriteBytes(writer,entry.Geometry);WriteBytes(writer,entry.LodGeometry);
        }

        public static byte[] Encode(IEnumerable<Entry> entries)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
            var list = new List<Entry>(entries);
            writer.Write(Version);
            writer.Write(list.Count);
            foreach (var entry in list)
            {
                WriteEntry(writer,entry);
            }
            writer.Flush();
            return stream.ToArray();
        }

        public static List<Entry> Decode(byte[] data)
        {
            using var stream = new MemoryStream(data, false);
            using var reader = new BinaryReader(stream, Encoding.UTF8);
            if (reader.ReadInt32() != Version) throw new InvalidDataException("Unsupported flower patch archive version");
            int count = reader.ReadInt32();
            if (count < 0 || count > data.Length / 12) throw new InvalidDataException("Invalid flower patch count");
            var entries = new List<Entry>(count);
            var names = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < count; i++)
            {
                var entry = new Entry { Name = reader.ReadString(), Metadata = reader.ReadString(), Geometry = ReadBytes(reader), LodGeometry = ReadBytes(reader) };
                if ((!entry.Name.StartsWith(FlowerPatchMesh.Prefix, StringComparison.Ordinal) && !entry.Name.StartsWith(FlowerPatchMesh.FlagPrefix, StringComparison.Ordinal)) ||
                    entry.Name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || !names.Add(entry.Name))
                    throw new InvalidDataException("Invalid flower patch name");
                entries.Add(entry);
            }
            if (stream.Position != stream.Length) throw new InvalidDataException("Unexpected flower patch archive data");
            return entries;
        }

        private static void WriteBytes(BinaryWriter writer, byte[] value)
        {
            writer.Write(value.Length);
            writer.Write(value);
        }

        private static byte[] ReadBytes(BinaryReader reader)
        {
            int length = reader.ReadInt32();
            if (length < 0 || length > reader.BaseStream.Length - reader.BaseStream.Position)
                throw new InvalidDataException("Invalid flower patch geometry length");
            return reader.ReadBytes(length);
        }
    }
}
