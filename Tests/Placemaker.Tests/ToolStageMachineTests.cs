using FlowerPainter.Core.Tool;
using Xunit;

namespace FlowerPainter.Tests
{
    public class ToolStageMachineTests
    {
        [Theory]
        [InlineData(ToolStage.Design)]
        [InlineData(ToolStage.Review)]
        public void ManualPlanningRequiresClickAndBlocksWithoutEntrance(ToolStage stage)
        {
            var f=ReadyFacts(); f.ManualPlanning=true; f.PlanReady=false;
            var p=StageMachine.Primary(stage,f);
            Assert.Equal(PrimaryKind.StartPlanning,p.Kind); Assert.True(p.Enabled);
            f.Planning=true; Assert.False(StageMachine.Primary(stage,f).Enabled);
            f.Planning=false; f.UnreadyStepPath="root"; f.UnreadyMarkerKind="entrance";
            Assert.Equal(PrimaryKind.PlaceMarkers,StageMachine.Primary(stage,f).Kind);
            Assert.False(StageMachine.Primary(stage,f).Enabled);
            f.UnreadyStepPath=null; f.PlanReady=true;
            Assert.Equal(stage==ToolStage.Design ? PrimaryKind.ToReview : PrimaryKind.Build,StageMachine.Primary(stage,f).Kind);
        }

        private static StageFacts ReadyFacts() => new StageFacts
        {
            AreaValid = true, HasSelection = true, HasRecipe = true, PlanReady = true, CanAfford = true, BuildServiceAvailable = true,
        };

        [Fact]
        public void FloralDesignCanOpenBeforeTheAreaIsPlaced()
        {
            var f=new StageFacts { HasRecipe=true,CanDesignWithoutArea=true };
            Assert.Contains(ToolStage.Design,StageMachine.Reachable(f));
            Assert.Equal(ToolStage.Design,StageMachine.Normalize(ToolStage.Design,f));
            Assert.False(StageMachine.Primary(ToolStage.Design,f).Enabled);
        }

        [Fact]
        public void NothingSelectedOnlyAreaAndManageReachable()
        {
            var r = StageMachine.Reachable(new StageFacts());
            Assert.Equal(new[] { ToolStage.Area, ToolStage.Manage }, r.ToArray());
        }

        [Fact]
        public void ValidAreaWithRecipeReachesReview()
        {
            var r = StageMachine.Reachable(ReadyFacts());
            Assert.Contains(ToolStage.Design, r);
            Assert.Contains(ToolStage.Review, r);
            Assert.DoesNotContain(ToolStage.Building, r);
        }

        [Fact]
        public void WhileBuildingOnlyBuildingReachable()
        {
            var f = ReadyFacts();
            f.BuildRunning = true;
            Assert.Equal(new[] { ToolStage.Building }, StageMachine.Reachable(f).ToArray());
            Assert.Equal(ToolStage.Building, StageMachine.Normalize(ToolStage.Area, f));
        }

        [Fact]
        public void NormalizeFallsBackWhenAreaBreaks()
        {
            var f = ReadyFacts();
            f.AreaValid = false;
            Assert.Equal(ToolStage.Area, StageMachine.Normalize(ToolStage.Review, f));
            f.AreaValid = true; f.HasRecipe = false;
            Assert.Equal(ToolStage.Design, StageMachine.Normalize(ToolStage.Review, f));
            Assert.Equal(ToolStage.Manage, StageMachine.Normalize(ToolStage.Manage, f));
        }

        [Fact]
        public void AreaStagePrimaryExplainsWhatIsMissing()
        {
            var p = StageMachine.Primary(ToolStage.Area, new StageFacts());
            Assert.False(p.Enabled);
            Assert.Equal("FlowerPainter.Reason.NoArea", p.Reason.Key);

            p = StageMachine.Primary(ToolStage.Area, new StageFacts { AreaInProgress = true, HasSelection = true });
            Assert.Equal("FlowerPainter.Reason.CloseArea", p.Reason.Key);

            p = StageMachine.Primary(ToolStage.Area, new StageFacts { HasSelection = true, AreaProblem = new LocText("FlowerPainter.Problem.Crossing") });
            Assert.False(p.Enabled);
            Assert.Equal("FlowerPainter.Problem.Crossing", p.Reason.Key);

            p = StageMachine.Primary(ToolStage.Area, ReadyFacts());
            Assert.True(p.Enabled);
            Assert.Equal(PrimaryKind.ToDesign, p.Kind);
        }

        [Fact]
        public void DesignPrimaryAsksForMarkersBeforeReview()
        {
            var f = ReadyFacts();
            f.UnreadyStepPath = "0";
            f.UnreadyMarkerKind = "entrance";
            f.UnreadyNextStep = new LocText("FlowerPainter.Step.PlaceEntrance");
            var p = StageMachine.Primary(ToolStage.Design, f);
            // No automatic entrances (FEEDBACK-4b): disabled "Review" with "Add at least one entrance",
            // still carrying the step + kind so pressing it starts placement there.
            Assert.Equal(PrimaryKind.PlaceMarkers, p.Kind);
            Assert.False(p.Enabled);
            Assert.Equal(StageMachine.AddEntranceKey, p.Reason.Key);
            Assert.Equal("entrance", p.MarkerKind);
            Assert.Equal("0", p.StepPath);
            Assert.Equal("FlowerPainter.Primary.Review", p.Label.Key);

            f.UnreadyMarkerKind = null;
            f.UnreadyReason = new LocText("FlowerPainter.Reason.TooNarrow");
            p = StageMachine.Primary(ToolStage.Design, f);
            Assert.Equal(PrimaryKind.ToReview, p.Kind);
            Assert.False(p.Enabled);
            Assert.Equal("FlowerPainter.Reason.TooNarrow", p.Reason.Key);
        }

        [Fact]
        public void ReviewPrimaryBuildDisabledReasonsInOrder()
        {
            var f = ReadyFacts();
            Assert.True(StageMachine.Primary(ToolStage.Review, f).Enabled);
            Assert.Equal(PrimaryKind.Build, StageMachine.Primary(ToolStage.Review, f).Kind);

            f.CanAfford = false;
            Assert.Equal("FlowerPainter.Reason.NoMoney", StageMachine.Primary(ToolStage.Review, f).Reason.Key);
            f.PlanHasErrors = true;
            Assert.Equal("FlowerPainter.Reason.PlanErrors", StageMachine.Primary(ToolStage.Review, f).Reason.Key);
            f.Planning = true;
            Assert.Equal("FlowerPainter.Reason.Planning", StageMachine.Primary(ToolStage.Review, f).Reason.Key);
            f.BuildServiceAvailable = false;
            Assert.Equal("FlowerPainter.Reason.BuildUnavailable", StageMachine.Primary(ToolStage.Review, f).Reason.Key);
        }

        [Fact]
        public void AreaProblemBlocksBuild()
        {
            var f = ReadyFacts();
            f.AreaProblem = new LocText("FlowerPainter.Problem.TooSmallFor", 400);
            var p = StageMachine.Primary(ToolStage.Review, f);
            Assert.False(p.Enabled);
            Assert.Equal("FlowerPainter.Problem.TooSmallFor", p.Reason.Key);
            Assert.False(StageMachine.Primary(ToolStage.Area, f).Enabled);
        }

        [Fact]
        public void EditingBuildsAsFinishEdit()
        {
            var f = ReadyFacts();
            f.Editing = true;
            var p = StageMachine.Primary(ToolStage.Review, f);
            Assert.Equal(PrimaryKind.FinishEdit, p.Kind);
            Assert.Equal("FlowerPainter.Primary.ApplyEdit", p.Label.Key);
        }

        [Fact]
        public void BuildingPrimaryCancels()
        {
            var f = ReadyFacts();
            f.BuildRunning = true;
            var p = StageMachine.Primary(ToolStage.Building, f);
            Assert.Equal(PrimaryKind.CancelBuild, p.Kind);
            Assert.True(p.Enabled);
        }

        [Fact]
        public void MarkerModePrimaryFinishesMarkers()
        {
            var f = ReadyFacts();
            f.MarkerModeActive = true;
            var p = StageMachine.Primary(ToolStage.Design, f);
            Assert.Equal(PrimaryKind.PlaceMarkers, p.Kind);
            Assert.Equal("FlowerPainter.Primary.DoneMarkers", p.Label.Key);
        }

        [Fact]
        public void EscapeStepsBackOneLevelAtATime()
        {
            var f = ReadyFacts();
            f.MarkerModeActive = true;
            Assert.Equal(EscapeAction.ExitMarkers, StageMachine.Escape(ToolStage.Review, f));
            f.MarkerModeActive = false;
            f.GestureActive = true;
            Assert.Equal(EscapeAction.CancelGesture, StageMachine.Escape(ToolStage.Review, f));
            f.GestureActive = false;
            Assert.Equal(EscapeAction.ToDesign, StageMachine.Escape(ToolStage.Review, f));
            Assert.Equal(EscapeAction.ToArea, StageMachine.Escape(ToolStage.Design, f));
            Assert.Equal(EscapeAction.ClearSelection, StageMachine.Escape(ToolStage.Area, f));
            Assert.Equal(EscapeAction.CloseTool, StageMachine.Escape(ToolStage.Area, new StageFacts()));
            Assert.Equal(EscapeAction.ToArea, StageMachine.Escape(ToolStage.Manage, new StageFacts()));
            f.BuildRunning = true;
            Assert.Equal(EscapeAction.None, StageMachine.Escape(ToolStage.Building, f));
        }

        [Fact]
        public void StageIdsRoundTrip()
        {
            foreach (ToolStage s in System.Enum.GetValues(typeof(ToolStage)))
            {
                Assert.True(StageMachine.TryParse(StageMachine.Id(s), out var back));
                Assert.Equal(s, back);
            }
            Assert.False(StageMachine.TryParse("nope", out _));
        }

        [Fact]
        public void PrimaryKindIdsMatchContract()
        {
            Assert.Equal("none", PrimaryAction.KindId(PrimaryKind.None));
            Assert.Equal("toDesign", PrimaryAction.KindId(PrimaryKind.ToDesign));
            Assert.Equal("toReview", PrimaryAction.KindId(PrimaryKind.ToReview));
            Assert.Equal("build", PrimaryAction.KindId(PrimaryKind.Build));
            Assert.Equal("placeMarkers", PrimaryAction.KindId(PrimaryKind.PlaceMarkers));
            Assert.Equal("cancelBuild", PrimaryAction.KindId(PrimaryKind.CancelBuild));
            Assert.Equal("finishEdit", PrimaryAction.KindId(PrimaryKind.FinishEdit));
        }
    }
}
