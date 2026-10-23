// FlowerPainter — registration of the game-tool workstream (systems, settings, key bindings,
// localisation). Called once from Mod.OnLoad by the lead: ToolRegistration.Register(updateSystem, this);
// and ToolRegistration.Unregister() from Mod.OnDispose.

using System;
using System.Collections.Generic;
using Colossal.IO.AssetDatabase;
using Game;
using Game.Input;
using Game.SceneFlow;
using FlowerPainter.Game.Localization;
using FlowerPainter.Game.Overlay;
using FlowerPainter.Game.Settings;
using FlowerPainter.Game.UI;

namespace FlowerPainter.Game.Tool
{
    public static class ToolRegistration
    {
        public static FlowerPainterSettings Settings { get; private set; }
        public static LayerDefaultsStore Defaults { get; private set; }
        private static FlowerPainterToolSystem s_Tool;

        public static void Register(UpdateSystem updateSystem, Mod mod)
        {
            ModPaths.Initialize(mod);

            Settings = new FlowerPainterSettings(mod);
            try
            {
                AssetDatabase.global.LoadSettings(nameof(FlowerPainter), Settings, new FlowerPainterSettings(mod));
            }
            catch (Exception e) { Mod.Log.Warn("FlowerPainter: settings not loaded, using defaults: " + e.Message); }
            Settings.PushToServices();
            Settings.RegisterInOptionsUI();
            // Must run before any GetAction() (actions come from the ProxyBinding properties).
            Settings.RegisterKeyBindings();
            Defaults = new LayerDefaultsStore();

            LocalizationLoader.Register(SettingsLocale);

            updateSystem.UpdateAt<FlowerPainterToolSystem>(SystemUpdatePhase.ToolUpdate);
            updateSystem.UpdateAt<FlowerPainterUISystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<FlowerPainterTooltipSystem>(SystemUpdatePhase.UITooltip);
            updateSystem.UpdateAt<FlowerPainterOverlaySystem>(SystemUpdatePhase.Rendering);
            updateSystem.UpdateAt<FilledPreviewSystem>(SystemUpdatePhase.Rendering);
            s_Tool = updateSystem.World.GetOrCreateSystemManaged<FlowerPainterToolSystem>();

            FlowerPainterSettings.RemoveEverythingRequested += OnRemoveEverything;
            FlowerPainterSettings.ResetDefaultsRequested += OnResetDefaults;
            Mod.Log.Info("FlowerPainter tool registered");
        }

        public static void Unregister()
        {
            FlowerPainterSettings.RemoveEverythingRequested -= OnRemoveEverything;
            FlowerPainterSettings.ResetDefaultsRequested -= OnResetDefaults;
            LocalizationLoader.Unregister();
            try { Settings?.UnregisterInOptionsUI(); } catch (Exception) { }
            Settings = null;
            s_Tool = null;
        }

        private static void OnRemoveEverything() => s_Tool?.RemoveEverything();
        private static void OnResetDefaults() => s_Tool?.ResetAllDefaults();

        /// <summary>
        /// CS2 derives the Options page's localisation ids from type and property names; map them to
        /// our readable keys ("FlowerPainter.Settings.<Property>" / ".Desc") from Lang/en-US.tool.json.
        /// </summary>
        private static Dictionary<string, string> SettingsLocale(Dictionary<string, string> strings)
        {
            var s = Settings;
            var r = new Dictionary<string, string>();
            if (s == null) return r;
            string T(string key, string fallback) => strings.TryGetValue(key, out var v) ? v : fallback;

            r[s.GetSettingsLocaleID()] = T("FlowerPainter.Name", "FlowerPainter");
            r[s.GetOptionTabLocaleID(FlowerPainterSettings.SectionMain)] = T("FlowerPainter.Settings.Tab.Main", "FlowerPainter");
            foreach (var g in new[] { FlowerPainterSettings.GroupGeneral, FlowerPainterSettings.GroupBrush, FlowerPainterSettings.GroupBuild, FlowerPainterSettings.GroupKeys, FlowerPainterSettings.GroupMaintenance })
                r[s.GetOptionGroupLocaleID(g)] = T("FlowerPainter.Settings.Group." + g, g);
            foreach (var p in new[]
            {
                nameof(FlowerPainterSettings.ShowCursorTooltips), nameof(FlowerPainterSettings.PreviewDetail), nameof(FlowerPainterSettings.SnapToRoadsWhenDrawing), nameof(FlowerPainterSettings.KeepOffExistingRoads),
                nameof(FlowerPainterSettings.LayoutSize), nameof(FlowerPainterSettings.EdgeMargin), nameof(FlowerPainterSettings.PaintAvoidBuildings),
                nameof(FlowerPainterSettings.BrushPaintOverAnything), nameof(FlowerPainterSettings.BrushStopRoads), nameof(FlowerPainterSettings.BrushStopPaths),
                nameof(FlowerPainterSettings.BrushStopBikes), nameof(FlowerPainterSettings.BrushStopTracks), nameof(FlowerPainterSettings.AreaGap), nameof(FlowerPainterSettings.AreaReach), nameof(FlowerPainterSettings.BrushStopBuildings), nameof(FlowerPainterSettings.BrushStopBuilds),
                nameof(FlowerPainterSettings.BrushStopLots), nameof(FlowerPainterSettings.BrushStopZoned), nameof(FlowerPainterSettings.BrushStopAllZoned), nameof(FlowerPainterSettings.BlockPaintAvoidEmptyZones), nameof(FlowerPainterSettings.BrushStopWater),
                nameof(FlowerPainterSettings.BrushStayInBlock), nameof(FlowerPainterSettings.BrushDrawClip),
                nameof(FlowerPainterSettings.BucketWallRoads), nameof(FlowerPainterSettings.BucketWallPaths), nameof(FlowerPainterSettings.BucketWallBikes), nameof(FlowerPainterSettings.BucketWallTracks),
                nameof(FlowerPainterSettings.BucketWallWater), nameof(FlowerPainterSettings.BucketWallLots), nameof(FlowerPainterSettings.BucketWallZoned),
                nameof(FlowerPainterSettings.BrushSnapDistance), nameof(FlowerPainterSettings.BrushSmoothing), nameof(FlowerPainterSettings.ResetBrushRules),
                nameof(FlowerPainterSettings.ToggleToolBinding), nameof(FlowerPainterSettings.PrimaryBinding), nameof(FlowerPainterSettings.UndoBinding),
                nameof(FlowerPainterSettings.RedoBinding), nameof(FlowerPainterSettings.RedoAltBinding), nameof(FlowerPainterSettings.ResetKeys),
                nameof(FlowerPainterSettings.RemoveEverything), nameof(FlowerPainterSettings.ResetDefaults),
                nameof(FlowerPainterSettings.AutoExcludeCollisions), nameof(FlowerPainterSettings.IgnoreOwnCollisions), nameof(FlowerPainterSettings.SurfaceClones), nameof(FlowerPainterSettings.TexturedPreviewSurfaces), nameof(FlowerPainterSettings.ParkingEconomy),
                nameof(FlowerPainterSettings.DefaultParkingFee), nameof(FlowerPainterSettings.ExperimentalParkingCompanion), nameof(FlowerPainterSettings.CompanionUpkeepFactor),
                nameof(FlowerPainterSettings.ClearExistingTreesAndProps), nameof(FlowerPainterSettings.RecoverRejectedPieces), nameof(FlowerPainterSettings.OverlayPavedSurfaces),
                nameof(FlowerPainterSettings.ChargeNetConstruction), nameof(FlowerPainterSettings.SelectAsOneThing), nameof(FlowerPainterSettings.ConfirmBulldoze),
                nameof(FlowerPainterSettings.ApplyParkStats), nameof(FlowerPainterSettings.RealParkStats),
            })
            {
                r[s.GetOptionLabelLocaleID(p)] = T("FlowerPainter.Settings." + p, p);
                r[s.GetOptionDescLocaleID(p)] = T("FlowerPainter.Settings." + p + ".Desc", "");
            }
            r[s.GetOptionWarningLocaleID(nameof(FlowerPainterSettings.RemoveEverything))] = T("FlowerPainter.Settings.RemoveEverything.Warning", "Remove everything FlowerPainter built?");
            r[s.GetOptionWarningLocaleID(nameof(FlowerPainterSettings.ResetDefaults))] = T("FlowerPainter.Settings.ResetDefaults.Warning", "Reset all FlowerPainter options and saved defaults?");
            foreach (PreviewDetail d in Enum.GetValues(typeof(PreviewDetail)))
                r[s.GetEnumValueLocaleID(d)] = T("FlowerPainter.Settings.PreviewDetail." + d, d.ToString());
            r[s.GetBindingMapLocaleID()] = T("FlowerPainter.Name", "FlowerPainter");
            foreach (var a in new[] { FlowerPainterSettings.ActionToggleTool, FlowerPainterSettings.ActionPrimary, FlowerPainterSettings.ActionUndo, FlowerPainterSettings.ActionRedo, FlowerPainterSettings.ActionRedoAlt })
                r[s.GetBindingKeyLocaleID(a)] = T("FlowerPainter.Settings.Action." + a, a);
            return r;
        }

        /// <summary>The global hotkey (polled by the UI system, which runs while the tool is closed).</summary>
        internal static ProxyAction ToggleAction
        {
            get
            {
                try { return Settings?.GetAction(FlowerPainterSettings.ActionToggleTool); }
                catch (Exception) { return null; }
            }
        }

        internal static bool InGame
        {
            get
            {
                try { return GameManager.instance != null && (GameManager.instance.gameMode & GameMode.Game) != 0; }
                catch (Exception) { return false; }
            }
        }
    }
}
