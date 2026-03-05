using AssetRipper.Primitives;
using AssetsTools.NET;
using System.Collections.Generic;
using System.IO;

namespace USCSandbox.Processor
{
    public class BlobManager
    {
        private AssetsFileReader[] _readers;
        private UnityVersion _engVer;

        public List<BlobEntry> Entries;

        public BlobManager(byte[][] blobs, UnityVersion engVer)
        {
            // Initialize a reader for each decompressed segment
            _readers = new AssetsFileReader[blobs.Length];
            for (var i = 0; i < blobs.Length; i++)
            {
                _readers[i] = new AssetsFileReader(new MemoryStream(blobs[i]));
            }

            _engVer = engVer;

            // The header and entry list are always in Segment 0
            var tableReader = _readers[0];
            var count = tableReader.ReadInt32();
            Entries = new List<BlobEntry>(count);
            for (var i = 0; i < count; i++)
            {
                Entries.Add(new BlobEntry(tableReader, engVer));
            }
        }

        public byte[] GetRawEntry(int index)
        {
            var entry = Entries[index];
            
            // Older versions don't have Segment set (defaults to 0). 
            // Also adds a fallback safety check just in case.
            AssetsFileReader reader = entry.Segment < 0 || entry.Segment >= _readers.Length
                ? _readers[0]
                : _readers[entry.Segment];

            reader.BaseStream.Position = entry.Offset;
            return reader.ReadBytes(entry.Length);
        }

        public ShaderParams GetShaderParams(int index)
        {
            var blobEntry = GetRawEntry(index);
            var r = new AssetsFileReader(new MemoryStream(blobEntry));
            return new ShaderParams(r, _engVer, true);
        }

        public ShaderSubProgram GetShaderSubProgram(int index)
        {
            var blobEntry = GetRawEntry(index);
            var r = new AssetsFileReader(new MemoryStream(blobEntry));
            return new ShaderSubProgram(r, _engVer);
        }
    }
}
