// Review helpers: automatic asset resolution (price = build), permanent-effect acknowledgement,
// automatic markers, and moving the camera to an issue (locate).

using System;
using System.Collections.Generic;
using Game.Rendering;
using FlowerPainter.Core;
using FlowerPainter.Core.Tool;
using FlowerPainter.Game.Preview;
using UnityEngine;

namespace FlowerPainter.Game.Tool
{
    public sealed partial class FlowerPainterToolSystem
    {
        // ───────────── assets ─────────────

        private int _resolvedCatalogVersion = int.MinValue;
        private int _assetsResolvedStamp;
        private bool _patchAssetsPending;
        private bool _patchAssetsFailed;

        /// <summary>Bumped whenever the plan's assets were (re)resolved — summary change detection.</summary>
        public int AssetsResolvedStamp => _assetsResolvedStamp;

        /// <summary>
        /// Writes concrete prefab names into the plan's automatic assets (Game/Preview/AutoAssets) so
        /// the summary prices what Build places. Re-runs when a new plan arrives or the catalog changes.
        /// </summary>
        private void ResolveAssets()
        {
            if (_plan == null) { _patchAssetsPending = false; _patchAssetsFailed = false; return; }
            var catalog = Services.Assets;
            if (catalog == null || !catalog.Ready) return;
            int version = catalog.Version;
            if (ReferenceEquals(_resolvedPlan, _plan) && _resolvedCatalogVersion == version) return;
            _patchAssetsFailed = false;
            try {
                _patchAssetsPending = !World.GetOrCreateSystemManaged<Assets.FlowerPatchPrefabSystem>().Prepare(_plan);
                if (_patchAssetsPending) return;
                AutoAssets.Resolve(_plan, catalog);
            }
            catch (Exception e) {
                _patchAssetsPending = false;
                _patchAssetsFailed = true;
                Mod.Log.Warn("FlowerPainter: resolving automatic assets failed: " + e.Message);
                _plan.Issues.Add(new PlanIssue { Severity = IssueSeverity.Error, Key = "FlowerPainter.Floral.Issue.PatchFailed" });
            }
            _resolvedPlan = _plan;
            _resolvedCatalogVersion = version;
            _assetsResolvedStamp++;
            _affordRevision = -1;
            Touch();
        }

        private ComposedPlan _resolvedPlan;

        // ───────────── permanent effects ─────────────

        private string _permanentAck;
        private int _permanentRevision = -2;
        private string _permanentFingerprint = "";

        private string PermanentFingerprint()
        {
            if (_permanentRevision != _planRevision)
            {
                _permanentRevision = _planRevision;
                _permanentFingerprint = UiModels.PermanentFingerprint(_plan);
            }
            return _permanentFingerprint;
        }

        /// <summary>The plan has permanent effects not covered by the player's acknowledgement (Build and Enter refused).</summary>
        public bool PermanentPending()
        {
            var fp = PermanentFingerprint();
            return fp.Length > 0 && _permanentAck != fp;
        }

        public bool PermanentAcknowledged => !PermanentPending();

        /// <summary>UI: the player ticked (or unticked) "I understand this is permanent". Covers exactly the current effects.</summary>
        public void AcknowledgePermanent(bool ack)
        {
            _permanentAck = ack ? PermanentFingerprint() : null;
            Touch();
        }

        // ───────────── markers ─────────────
        // Entrances are never placed automatically (FEEDBACK-4b; playtest-1 "no auto entrances"): a step
        // that needs one stays unready and the primary button leads the player to place it.

        private void MarkMarkersEdited(string path)
        {
            if (!string.IsNullOrEmpty(path)) _markersEdited.Add(path);
        }

        // ───────────── locate ─────────────

        /// <summary>UI: move the camera to a world position (issue, marker…), keeping zoom and angle.</summary>
        public void Locate(float x, float y, float z)
        {
            if (float.IsNaN(x) || float.IsNaN(z) || float.IsInfinity(x) || float.IsInfinity(z)) return;
            try
            {
                var cams = World.GetExistingSystemManaged<CameraUpdateSystem>();
                if (cams == null) return;
                var target = new Vector3(x, float.IsNaN(y) ? 0f : y, z);
                var game = cams.gamePlayController;
                if (game != null)
                {
                    // Orbit/cinematic cameras follow their own targets: hand control back to the normal camera first.
                    if (!ReferenceEquals(cams.activeCameraController, game))
                    {
                        game.TryMatchPosition(cams.activeCameraController);
                        cams.activeCameraController = game;
                    }
                    game.pivot = target;
                    return;
                }
                var active = cams.activeCameraController;
                if (active != null) active.pivot = target;
            }
            catch (Exception e) { Mod.Log.Warn("FlowerPainter: locate failed: " + e.Message); }
        }
    }
}
