using System;
using System.IO;
using System.Linq;
using System.Text;

namespace ValeScore
{
    public sealed class RecentProjects
    {
        private readonly string file;
        private const int Limit = 12;

        public RecentProjects(string folder) { file = Path.Combine(folder, "recentes.txt"); }

        public string[] Read()
        {
            var info = new FileInfo(file);
            if (!info.Exists || info.Length > 131072) return new string[0];
            return File.ReadLines(file, Encoding.UTF8).Where(IsProject)
                .Distinct(StringComparer.OrdinalIgnoreCase).Take(Limit).ToArray();
        }

        public void Record(string path)
        {
            path = Path.GetFullPath(path);
            if (!IsProject(path)) return;
            Write(new[] { path }.Concat(Read().Where(value => !SamePath(value, path))).Take(Limit).ToArray());
        }

        public void Remove(string path)
        {
            Write(Read().Where(value => !SamePath(value, path)).ToArray());
        }

        private static bool SamePath(string first, string second)
        {
            return string.Equals(first, second, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsProject(string path)
        {
            return !string.IsNullOrWhiteSpace(path) && path.IndexOfAny(Path.GetInvalidPathChars()) < 0 &&
                Path.IsPathRooted(path) && string.Equals(Path.GetExtension(path), ".valescore", StringComparison.OrdinalIgnoreCase);
        }

        private void Write(string[] entries)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            string temporary = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllLines(temporary, entries, new UTF8Encoding(false));
                if (File.Exists(file)) File.Replace(temporary, file, null);
                else File.Move(temporary, file);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
