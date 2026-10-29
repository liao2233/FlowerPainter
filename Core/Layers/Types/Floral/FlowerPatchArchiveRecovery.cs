using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FlowerPainter.Core.Layers.Types.Floral
{
    // Retain only data that could not be restored. Normal saves release their buffers after writing.
    internal sealed class FlowerPatchArchiveRecovery
    {
        private readonly Dictionary<string,FlowerPatchArchive.Entry> _unrestored = new Dictionary<string,FlowerPatchArchive.Entry>(StringComparer.Ordinal);
        private byte[] _opaque;
        public bool HasRecoveryData => _opaque!=null || _unrestored.Count!=0;
        public bool HasOpaqueData => _opaque!=null;
        public void Clear() { _opaque=null;_unrestored.Clear(); }

        public void Restore(byte[] data, Action<FlowerPatchArchive.Entry> restore, Action resolve, Action<string> warning)
        {
            Clear();
            if(data.Length==0)return;
            List<FlowerPatchArchive.Entry> entries;
            try { entries=FlowerPatchArchive.Decode(data); }
            catch(Exception e) when(!(e is OutOfMemoryException)) {
                _opaque=data;warning("Archive cannot be decoded; original bytes retained for resaving: "+e.Message);return;
            }
            foreach(var entry in entries) {
                try { restore(entry); }
                catch(Exception e) when(!(e is OutOfMemoryException)) {
                    _unrestored[entry.Name]=entry;warning("Model retained for recovery: "+entry.Name+": "+e.Message);
                }
            }
            try { resolve(); }
            catch(Exception e) when(!(e is OutOfMemoryException)) {
                // The game may discard unresolved entities. Preserve their models even without live references.
                foreach(var entry in entries)_unrestored[entry.Name]=entry;
                warning("Prefab resolution failed; archive models retained: "+e.Message);
            }
        }

        public ArraySegment<byte> Capture(IEnumerable<string> names, Func<string,long,FlowerPatchArchive.Entry> export, int limit, Action<string> warning)
        {
            if(_opaque!=null) {
                warning("Unknown or damaged archive preserved unchanged. Newly added models cannot be embedded until it is readable.");
                return new ArraySegment<byte>(_opaque);
            }
            if(limit<8)throw new ArgumentOutOfRangeException(nameof(limit));
            using var stream=new MemoryStream();
            using var writer=new BinaryWriter(stream,Encoding.UTF8,true);
            writer.Write(1);writer.Write(0);
            int count=0;
            var seen=new HashSet<string>(StringComparer.Ordinal);
            void Append(string name, FlowerPatchArchive.Entry retained) {
                if(!seen.Add(name))return;
                try {
                    long remaining=limit-stream.Length;
                    var entry=retained??export(name,remaining);
                    long size=FlowerPatchArchive.EntrySize(entry.Name,entry.Metadata,entry.Geometry.LongLength,entry.LodGeometry.LongLength);
                    if(size>remaining) { warning("Archive size limit: model omitted: "+name);return; }
                    // Avoid MemoryStream's doubling beyond the archive budget. No final ToArray copy.
                    long needed=stream.Length+size;
                    if(needed>stream.Capacity)stream.Capacity=(int)Math.Min(limit,Math.Max(needed,Math.Max(4096L,stream.Capacity*2L)));
                    FlowerPatchArchive.WriteEntry(writer,entry);count++;
                } catch(Exception e) when(!(e is OutOfMemoryException)) {
                    warning("Model could not be embedded: "+name+": "+e.Message);
                }
            }
            // Retained data takes precedence so a resave cannot silently discard a failed restoration.
            foreach(var pair in _unrestored)Append(pair.Key,pair.Value);
            foreach(var name in names)Append(name,null);
            writer.Flush();stream.Position=4;writer.Write(count);writer.Flush();
            return new ArraySegment<byte>(stream.GetBuffer(),0,(int)stream.Length);
        }
    }
}
