using System;
using Colossal.Logging;
using Game;
using Game.Modding;

namespace FlowerPainter
{
    public sealed class Mod : IMod
    {
        public static readonly ILog Log = LogManager.GetLogger(nameof(FlowerPainter)).SetShowsErrorsInUI(false);

        public void OnLoad(UpdateSystem updateSystem)
        {
            Log.Info($"FlowerPainter {typeof(Mod).Assembly.GetName().Version} loading");
            // Each workstream registers its own systems; a failure in one area is logged and the
            // rest still loads, so a broken feature never takes the whole mod (or the game) down.
            Try("build", () => Game.Build.BuildRegistration.Register(updateSystem));
            Try("tool", () => Game.Tool.ToolRegistration.Register(updateSystem, this));
            // In-game test suite: inert unless the command line AND an armed ModsData/FlowerPainter/suite/run.json ask for it.
            Try("suite", () => Game.Suite.SuiteGate.Register(updateSystem));
        }

        public void OnDispose()
        {
            try { Game.Tool.ToolRegistration.Unregister(); } catch (Exception e) { Log.Error(e, "FlowerPainter: tool unregister failed"); }
            Game.Services.Reset();
        }

        private static void Try(string area, Action register)
        {
            try { register(); }
            catch (Exception e) { Log.Error(e, $"FlowerPainter: registering '{area}' failed"); }
        }
    }
}
