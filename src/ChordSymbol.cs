using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace ValeScore
{
    public sealed class ChordSymbol
    {
        private static readonly Regex Pattern = new Regex(@"^([A-G])([#b]?)(maj7|maj9|m7b5|m7|m9|m|dim7|dim|aug|sus2|sus4|add9|13|11|9|7|6)?(?:/([A-G])([#b]?))?$",
            RegexOptions.CultureInvariant);
        private static readonly string[] Suffixes = { "", "maj7", "maj9", "m7b5", "m7", "m9", "m", "dim7", "dim", "aug", "sus2", "sus4", "add9", "13", "11", "9", "7", "6" };
        private static readonly string[] Kinds = { "major", "major-seventh", "major-ninth", "half-diminished", "minor-seventh",
            "minor-ninth", "minor", "diminished-seventh", "diminished", "augmented", "suspended-second", "suspended-fourth",
            "major", "dominant-13th", "dominant-11th", "dominant-ninth", "dominant", "major-sixth" };
        private Match match;

        public static bool TryParse(string value, out ChordSymbol chord)
        {
            var normalized = (value ?? "").Trim().Replace('♯', '#').Replace('♭', 'b');
            var parsed = Pattern.Match(normalized);
            chord = parsed.Success ? new ChordSymbol { match = parsed } : null;
            return chord != null;
        }

        public string Text { get { return match.Value; } }

        public string Transpose(int amount, bool chromatic)
        {
            if (amount == 0) return Text;
            string root = MovePitch(match.Groups[1].Value, match.Groups[2].Value, amount, chromatic);
            string bass = match.Groups[4].Success
                ? "/" + MovePitch(match.Groups[4].Value, match.Groups[5].Value, amount, chromatic) : "";
            return root + match.Groups[3].Value + bass;
        }

        private static string MovePitch(string step, string accidental, int amount, bool chromatic)
        {
            int degree = Array.IndexOf(Score.Steps, step);
            if (!chromatic) return Score.Steps[(degree + amount % 7 + 7) % 7] + accidental;
            int original = Score.Semitones[degree] + (accidental == "#" ? 1 : accidental == "b" ? -1 : 0);
            int pitch = ((original + amount) % 12 + 12) % 12;
            var names = amount < 0
                ? new[] { "C", "Db", "D", "Eb", "E", "F", "Gb", "G", "Ab", "A", "Bb", "B" }
                : new[] { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
            return names[pitch];
        }

        public void Write(XmlWriter xml, int staff, bool piano)
        {
            xml.WriteStartElement("harmony");
            xml.WriteAttributeString("placement", "above");
            WritePitch(xml, "root", match.Groups[1].Value, match.Groups[2].Value);
            string suffix = match.Groups[3].Value;
            xml.WriteStartElement("kind");
            if (suffix != "") xml.WriteAttributeString("text", suffix);
            xml.WriteString(Kinds[Array.IndexOf(Suffixes, suffix)]);
            xml.WriteEndElement();
            if (match.Groups[4].Success) WritePitch(xml, "bass", match.Groups[4].Value, match.Groups[5].Value);
            if (suffix == "add9")
            {
                xml.WriteStartElement("degree");
                xml.WriteElementString("degree-value", "9");
                xml.WriteElementString("degree-alter", "0");
                xml.WriteElementString("degree-type", "add");
                xml.WriteEndElement();
            }
            if (piano) xml.WriteElementString("staff", (staff + 1).ToString());
            xml.WriteEndElement();
        }

        private static void WritePitch(XmlWriter xml, string element, string step, string alter)
        {
            xml.WriteStartElement(element);
            xml.WriteElementString(element + "-step", step);
            if (alter != "") xml.WriteElementString(element + "-alter", alter == "b" ? "-1" : "1");
            xml.WriteEndElement();
        }

        public static string Read(XElement element)
        {
            var kind = (string)element.Element("kind");
            int index = Array.IndexOf(Kinds, kind);
            if (index < 0) return null;
            string suffix = Suffixes[index];
            var degrees = element.Elements("degree").ToArray();
            if (degrees.Length != 0)
            {
                if (kind != "major" || degrees.Length != 1 || (string)degrees[0].Element("degree-value") != "9" ||
                    (string)degrees[0].Element("degree-alter") != "0" || (string)degrees[0].Element("degree-type") != "add") return null;
                suffix = "add9";
            }
            string root = ReadPitch(element.Element("root"), "root");
            if (root == null) return null;
            var bassElement = element.Element("bass");
            var bass = bassElement == null ? "" : ReadPitch(bassElement, "bass");
            ChordSymbol chord;
            if (bass == null || !TryParse(root + suffix + (bass == "" ? "" : "/" + bass), out chord)) return null;
            return chord.Text;
        }

        private static string ReadPitch(XElement element, string prefix)
        {
            if (element == null) return null;
            string step = (string)element.Element(prefix + "-step");
            string alter = (string)element.Element(prefix + "-alter") ?? "0";
            if (!Score.Steps.Contains(step)) return null;
            if (alter == "-1") return step + "b";
            if (alter == "1") return step + "#";
            return alter == "0" ? step : null;
        }
    }
}
