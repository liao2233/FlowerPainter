using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using FlowerPainter.Core.Layers.Types.Floral;
using Xunit;

namespace FlowerPainter.Tests
{
    public class FloralArchiveTests
    {
        private static FlowerPatchArchive.Entry Entry(string id="A",int size=3)=>new FlowerPatchArchive.Entry {
            Name="FlowerPainter Patch "+id,Metadata="{}",Geometry=new byte[size],LodGeometry=new byte[]{1,2}
        };
        private static byte[] Bytes(ArraySegment<byte> segment)=>segment.ToArray();
        [Fact] public void MissingFileDoesNotDiscardOtherModels() {
            var recovery=new FlowerPatchArchiveRecovery();var warnings=new List<string>();
            var data=recovery.Capture(new[]{"missing","ok"},(name,budget)=>name=="missing"?throw new FileNotFoundException():Entry(name),1024,warnings.Add);
            Assert.Equal("FlowerPainter Patch ok",Assert.Single(FlowerPatchArchive.Decode(Bytes(data))).Name);
            Assert.Single(warnings);
        }
        [Fact] public void RestoreContinuesAfterWriteFailureAndRetainsFailedModelForResave() {
            var recovery=new FlowerPatchArchiveRecovery();var a=Entry();var b=Entry("B");int restored=0;
            recovery.Restore(FlowerPatchArchive.Encode(new[]{a,b}),e=>{if(e.Name==a.Name)throw new UnauthorizedAccessException();restored++;},()=>{},_=>{});
            Assert.Equal(1,restored);Assert.True(recovery.HasRecoveryData);
            var saved=recovery.Capture(Array.Empty<string>(),(_,__)=>throw new Exception("Not expected"),1024,_=>{});
            Assert.Equal(a.Geometry,Assert.Single(FlowerPatchArchive.Decode(Bytes(saved))).Geometry);
        }
        [Theory] [InlineData(true)] [InlineData(false)]
        public void UnreadableArchiveIsPreservedByteForByte(bool future) {
            var original=FlowerPatchArchive.Encode(new[]{Entry()});
            if(future)original[0]=2;else Array.Resize(ref original,original.Length-1);
            var recovery=new FlowerPatchArchiveRecovery();bool restored=false;
            recovery.Restore(original,_=>restored=true,()=>{},_=>{});
            Assert.False(restored);Assert.True(recovery.HasOpaqueData);
            var saved=recovery.Capture(new[]{"new"},(_,__)=>throw new Exception("Cannot merge unknown format"),1024,_=>{});
            Assert.Equal(original,Bytes(saved));
        }
        [Fact] public void ChangedGameFieldsKeepAllModelsEvenIfEntitiesAreLost() {
            var recovery=new FlowerPatchArchiveRecovery();var entries=new[]{Entry(),Entry("B")};
            recovery.Restore(FlowerPatchArchive.Encode(entries),_=>{},()=>throw new MissingMemberException(),_=>{});
            var saved=recovery.Capture(Array.Empty<string>(),(_,__)=>throw new Exception(),1024,_=>{});
            Assert.Equal(2,FlowerPatchArchive.Decode(Bytes(saved)).Count);
        }
        [Fact] public void BudgetKeepsFittingModelsAndContinuesPastOversizedOne() {
            var recovery=new FlowerPatchArchiveRecovery();var entries=new[]{Entry("A"),Entry("huge",500),Entry("B")};
            int budget=(int)(8+entries.Where(e=>e.Name!=entries[1].Name).Sum(e=>FlowerPatchArchive.EntrySize(e.Name,e.Metadata,e.Geometry.Length,e.LodGeometry.Length)));
            var warnings=new List<string>();
            var saved=recovery.Capture(entries.Select(e=>e.Name),(name,remaining)=>entries.Single(e=>e.Name==name),budget,warnings.Add);
            Assert.Equal(budget,saved.Count);Assert.Equal(2,FlowerPatchArchive.Decode(Bytes(saved)).Count);Assert.Single(warnings);
        }
        [Fact] public void ExportReceivesRemainingBudgetBeforeReadingGeometry() {
            var recovery=new FlowerPatchArchiveRecovery();long secondBudget=0;var a=Entry();
            recovery.Capture(new[]{"first","second"},(name,budget)=>{if(name=="second")secondBudget=budget;return a;},1024,_=>{});
            Assert.Equal(1024-8-FlowerPatchArchive.EntrySize(a.Name,a.Metadata,a.Geometry.Length,a.LodGeometry.Length),secondBudget);
        }
        [Fact] public void RecoveryPrecedesLiveExportAndIsNotDuplicated() {
            var a=Entry();var recovery=new FlowerPatchArchiveRecovery();
            recovery.Restore(FlowerPatchArchive.Encode(new[]{a}),_=>throw new IOException(),()=>{},_=>{});
            var saved=recovery.Capture(new[]{a.Name},(_,__)=>throw new Exception("Retained model must take precedence"),1024,_=>throw new Exception());
            Assert.Single(FlowerPatchArchive.Decode(Bytes(saved)));
        }
        [Fact] public void SuccessfulRestorationDoesNotKeepUnusedModelBuffers() {
            var recovery=new FlowerPatchArchiveRecovery();
            recovery.Restore(FlowerPatchArchive.Encode(new[]{Entry()}),_=>{},()=>{},_=>{});
            Assert.False(recovery.HasRecoveryData);
            Assert.Empty(FlowerPatchArchive.Decode(Bytes(recovery.Capture(Array.Empty<string>(),(_,__)=>throw new Exception(),1024,_=>{}))));
        }
        [Fact] public void NewCityClearsPreviousRecoveryData() {
            var recovery=new FlowerPatchArchiveRecovery();
            recovery.Restore(new byte[]{9},_=>{},()=>{},_=>{});recovery.Clear();
            Assert.False(recovery.HasRecoveryData);
        }
        [Fact] public void BoundedWriterPreservesExistingArchiveFormat() {
            var a=Entry("非ASCII");var recovery=new FlowerPatchArchiveRecovery();
            var saved=recovery.Capture(new[]{a.Name},(_,__)=>a,1024,_=>{});
            Assert.Equal(FlowerPatchArchive.Encode(new[]{a}),Bytes(saved));
        }
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
