using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace GorillaDrive
{
    /// <summary>Finds the player's own Gorilla Tag folder (sheet games, row gt). Never copies from it.</summary>
    public static class GtLocator
    {
        public const string EnvVar = "GORILLADRIVE_GT_DIR";
        /// <summary>Melty passes the folder at launch: --gorilladrive-gt={game:custom-gorilla-tag}</summary>
        public const string ArgPrefix = "--gorilladrive-gt=";
        const string SteamAppId = "1533390";

        public static string Find(string configOverride, Action<string> log)
        {
            foreach (var candidate in Candidates(configOverride))
            {
                if (!string.IsNullOrEmpty(candidate) && Directory.Exists(candidate) && GtReader.FindDataDir(candidate) != null)
                    return candidate;
            }
            log("Gorilla Tag folder not found. Set it in BepInEx/config/gg.melty.gorilladrive.cfg (GorillaTagFolder).");
            return null;
        }

        static IEnumerable<string> Candidates(string configOverride)
        {
            yield return configOverride;
            foreach (var a in Environment.GetCommandLineArgs())
                if (a.StartsWith(ArgPrefix, StringComparison.OrdinalIgnoreCase)) yield return a.Substring(ArgPrefix.Length).Trim('"');
            yield return Environment.GetEnvironmentVariable(EnvVar);
            foreach (var lib in SteamLibraries())
            {
                var manifest = Path.Combine(lib, "steamapps", "appmanifest_" + SteamAppId + ".acf");
                if (!File.Exists(manifest)) continue;
                var m = Regex.Match(File.ReadAllText(manifest), "\"installdir\"\\s+\"([^\"]+)\"");
                yield return Path.Combine(Path.Combine(lib, "steamapps", "common"), m.Success ? m.Groups[1].Value : "Gorilla Tag");
            }
        }

        static IEnumerable<string> SteamLibraries()
        {
            var roots = new List<string>();
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                    if (key?.GetValue("SteamPath") is string p) roots.Add(p.Replace('/', '\\'));
            }
            catch { }
            roots.Add(@"C:\Program Files (x86)\Steam");
            // The Long Drive itself lives in a Steam library: walk up from the game folder.
            var here = BepInEx.Paths.GameRootPath;
            for (var d = new DirectoryInfo(here); d != null; d = d.Parent)
                if (d.Name.Equals("steamapps", StringComparison.OrdinalIgnoreCase) && d.Parent != null) roots.Add(d.Parent.FullName);

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var root in roots)
            {
                if (seen.Add(root)) yield return root;
                var vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
                if (!File.Exists(vdf)) continue;
                string text;
                try { text = File.ReadAllText(vdf); } catch { continue; }
                foreach (Match m in Regex.Matches(text, "\"path\"\\s+\"([^\"]+)\""))
                {
                    var lib = m.Groups[1].Value.Replace(@"\\", @"\");
                    if (seen.Add(lib)) yield return lib;
                }
            }
        }
    }
}
