using System;
using System.IO;
using System.Linq;

namespace ValeScore
{
    public sealed class Recovery
    {
        private readonly string folder;
        private readonly string current;

        public Recovery(string folder)
        {
            this.folder = folder;
            current = Path.Combine(folder, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" +
                Guid.NewGuid().ToString("N") + ".valescore");
        }

        public string Folder { get { return folder; } }

        public bool HasCopies()
        {
            return Directory.Exists(folder) && Directory.EnumerateFiles(folder, "*.valescore").Any();
        }

        public void Save(Score score)
        {
            Directory.CreateDirectory(folder);
            score.Save(current);
        }

        public void Clear()
        {
            if (File.Exists(current)) File.Delete(current);
        }
    }
}
