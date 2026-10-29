using System;
using System.Linq;
using System.Reflection;
using System.Text;
using Colossal.PSI.Environment;
using UnityEngine;

namespace FlowerPainter.Game.Tool
{
    public sealed partial class FlowerPainterToolSystem
    {
        private float _diagnosticsCopiedUntil;
        public void CopyDiagnostics()
        {
            var archive=World.GetExistingSystemManaged<Assets.FlowerPatchArchiveSystem>();
            var version=typeof(Mod).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? typeof(Mod).Assembly.GetName().Version.ToString();
            var companions=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetName().Name)
                .Where(n=>n.IndexOf("Placemaker",StringComparison.OrdinalIgnoreCase)>=0).OrderBy(n=>n).ToArray();
            var text=new StringBuilder();
            text.AppendLine("FlowerPainter diagnostics").AppendLine("Mod: "+version)
                .AppendLine("Game: "+Application.version).AppendLine("Save format: "+global::Game.Version.current)
                .AppendLine("Loaded Placemaker assemblies: "+(companions.Length==0?"none detected":string.Join(", ",companions)))
                .AppendLine("Archive warning: "+(archive?.WarningKey??"unavailable"))
                .AppendLine("Last operation warnings: "+(archive?.LastOperationWarnings??0))
                .AppendLine("Last capture bytes: "+(archive?.LastCaptureBytes??0))
                .AppendLine("Last capture ms: "+(archive?.LastCaptureMilliseconds??0).ToString("F1",System.Globalization.CultureInfo.InvariantCulture))
                .AppendLine("Log: "+System.IO.Path.Combine(EnvPath.kUserDataPath,"Logs","FlowerPainter.log"))
                .AppendLine("Shared/imported save: [please fill in]")
                .AppendLine("Steps to reproduce: [please fill in]");
            GUIUtility.systemCopyBuffer=text.ToString();
            _diagnosticsCopiedUntil=UnityEngine.Time.realtimeSinceStartup+4f;Touch();
        }
    }
}
