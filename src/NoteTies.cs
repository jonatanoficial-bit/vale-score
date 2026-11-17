using System;
using System.Collections.Generic;
using System.Linq;

namespace ValeScore
{
    internal static class NoteTies
    {
        internal static bool Fits(Note source, Note target)
        {
            return !source.IsRest && !target.IsRest && source.Voice == target.Voice &&
                source.Tick + source.Length == target.Tick && source.Step == target.Step && source.Alter == target.Alter;
        }

        internal static bool TryTarget(Note source, Dictionary<Guid, Note> notes, out Note target)
        {
            target = null;
            return source.TiedTo.HasValue && notes.TryGetValue(source.TiedTo.Value, out target) && Fits(source, target);
        }

        internal static void Prune(List<Note> lane)
        {
            if (!lane.Any(note => note.TiedTo.HasValue)) return;
            var notes = lane.ToDictionary(note => note.Id);
            foreach (var source in lane.Where(note => note.TiedTo.HasValue))
            {
                Note target;
                if (!TryTarget(source, notes, out target)) source.TiedTo = null;
            }
        }

        internal static Dictionary<Guid, double> AttackLengths(List<Note> lane)
        {
            var continuations = new HashSet<Guid>(lane.Where(n => n.TiedTo.HasValue).Select(n => n.TiedTo.Value));
            var lengths = new Dictionary<Guid, double>();
            var sustained = new Dictionary<Guid, int>();
            for (int i = lane.Count - 1; i >= 0; i--)
            {
                var note = lane[i];
                if (note.IsRest) continue;
                int length = note.Length + (note.TiedTo.HasValue ? sustained[note.TiedTo.Value] : 0);
                if (continuations.Contains(note.Id)) sustained.Add(note.Id, length);
                else lengths.Add(note.Id, note.TiedTo.HasValue ? length : length * Articulations.SoundLength(note.Articulation));
            }
            return lengths;
        }
    }
}
