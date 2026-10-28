using System;
using System.IO;
using FlowerPainter.Core.Layers.Types.Floral;
using Xunit;

namespace FlowerPainter.Tests
{
    public class FloralArchiveTests
    {
        [Fact]
        public void ArchivePreservesGeometryAndMetadata()
        {
            var items = new[] { new FlowerPatchArchive.Entry {
                Name = "FlowerPainter Patch ABC", Metadata = "{\"MaterialVersion\":5}",
                Geometry = new byte[] { 0, 1, 255 }, LodGeometry = new byte[] { 5, 4 }
            }};
            var restored = FlowerPatchArchive.Decode(FlowerPatchArchive.Encode(items));
            Assert.Single(restored);
            Assert.Equal(items[0].Name, restored[0].Name);
            Assert.Equal(items[0].Metadata, restored[0].Metadata);
            Assert.Equal(items[0].Geometry, restored[0].Geometry);
            Assert.Equal(items[0].LodGeometry, restored[0].LodGeometry);
        }

        [Fact]
        public void ArchiveRejectsTruncatedGeometry()
        {
            var bytes = FlowerPatchArchive.Encode(new[] { new FlowerPatchArchive.Entry {
                Name = "FlowerPainter Flag ABC", Metadata = "{}", Geometry = new byte[] { 1 }, LodGeometry = new byte[] { 2 }
            }});
            Array.Resize(ref bytes, bytes.Length - 1);
            Assert.Throws<InvalidDataException>(() => FlowerPatchArchive.Decode(bytes));
        }
    }
}
