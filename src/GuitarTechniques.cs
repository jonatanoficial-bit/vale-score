using System.Collections.Generic;

namespace ValeScore
{
    internal static class GuitarTechniques
    {
        internal static readonly string[] Names = {
            "bend", "slide-up", "slide-down", "hammer-on", "pull-off", "natural-harmonic", "palm-mute", "let-ring", "vibrato"
        };

        private static readonly Dictionary<string, string> Labels = new Dictionary<string, string> {
            { "bend", "Bend" },
            { "slide-up", "Slide ascendente" },
            { "slide-down", "Slide descendente" },
            { "hammer-on", "Hammer-on" },
            { "pull-off", "Pull-off" },
            { "natural-harmonic", "Harmônico natural" },
            { "palm-mute", "Palm mute" },
            { "let-ring", "Let ring" },
            { "vibrato", "Vibrato" }
        };

        internal static bool IsValid(string technique)
        {
            return technique == null || System.Array.IndexOf(Names, technique) >= 0;
        }

        internal static string Label(string technique)
        {
            string label;
            return technique != null && Labels.TryGetValue(technique, out label) ? label : "";
        }

        internal static string Abbreviation(string technique)
        {
            switch (technique)
            {
                case "bend": return "B";
                case "hammer-on": return "H";
                case "pull-off": return "P";
                case "natural-harmonic": return "N.H.";
                case "palm-mute": return "P.M.";
                case "let-ring": return "let ring";
                case "vibrato": return "vib.";
                default: return "";
            }
        }
    }
}

