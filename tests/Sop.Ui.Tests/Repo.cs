using System;
using System.Collections.Generic;
using System.IO;

namespace Sop.Ui.Tests
{
    /// <summary>Locates the repository so tests can read source files.</summary>
    internal static class Repo
    {
        public static string Root { get; } = Find();

        public static string Src => Path.Combine(Root, "src");

        private static string Find()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "Sop.sln")) || File.Exists(Path.Combine(dir.FullName, "SOPs.Packages.sln"))) return dir.FullName;
                dir = dir.Parent;
            }
            throw new InvalidOperationException("Sop.sln not found above " + AppContext.BaseDirectory);
        }

        public static IEnumerable<string> SourceFiles(params string[] extensions)
        {
            foreach (var file in Directory.EnumerateFiles(Src, "*.*", SearchOption.AllDirectories))
            {
                var rel = file.Substring(Src.Length + 1).Replace('\\', '/');
                if (rel.Contains("/bin/") || rel.Contains("/obj/")) continue;
                foreach (var ext in extensions)
                {
                    if (file.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) { yield return file; break; }
                }
            }
        }

        public static string Relative(string file) => file.Substring(Root.Length + 1).Replace('\\', '/');
    }
}
