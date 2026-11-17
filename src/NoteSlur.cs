using System;
using System.Collections.Generic;
using System.Linq;

namespace ValeScore
{
    internal sealed class NoteSlur
    {
        internal readonly Note Start, End;
        internal int Number;

        internal NoteSlur(Note start, Note end) { Start = start; End = end; }

        internal static bool Fits(Note start, Note end)
        {
            return !start.IsRest && !end.IsRest && start.Voice == end.Voice && start.Tick < end.Tick;
        }

        internal static List<NoteSlur> In(List<Note> staff)
        {
            var slurs = new List<NoteSlur>();
            if (!staff.Any(n => n.SlursTo != null)) return slurs;
            var notes = staff.ToDictionary(n => n.Id);
            foreach (var start in staff.Where(n => n.SlursTo != null))
            foreach (var id in start.SlursTo)
            {
                Note end;
                if (notes.TryGetValue(id, out end) && Fits(start, end)) slurs.Add(new NoteSlur(start, end));
            }
            return slurs;
        }

        internal static void Prune(List<Note> staff)
        {
            if (!staff.Any(n => n.SlursTo != null)) return;
            var notes = staff.ToDictionary(n => n.Id);
            foreach (var start in staff.Where(n => n.SlursTo != null))
            {
                start.SlursTo.RemoveAll(id =>
                {
                    Note end;
                    return !notes.TryGetValue(id, out end) || !Fits(start, end);
                });
                if (start.SlursTo.Count == 0) start.SlursTo = null;
            }
        }

        internal static List<NoteSlur> Numbered(Part part, int barTicks)
        {
            var slurs = part.Staves.SelectMany(In).ToList();
            if (slurs.Count == 0) return slurs;
            // MusicXML numbers distinguish overlapping spans in serialized note order.
            var order = part.Staves.SelectMany((staff, index) => staff.Select(n => new { Note = n, Staff = index }))
                .OrderBy(n => n.Note.Tick / barTicks).ThenBy(n => n.Staff).ThenBy(n => n.Note.Voice)
                .ThenBy(n => n.Note.Tick).Select((n, i) => new { n.Note.Id, Index = i })
                .ToDictionary(n => n.Id, n => n.Index);
            var occupiedUntil = Enumerable.Repeat(-1, 16).ToArray();
            foreach (var slur in slurs.OrderBy(s => order[s.Start.Id]).ThenByDescending(s => order[s.End.Id]))
            {
                int slot = Array.FindIndex(occupiedUntil, end => end <= order[slur.Start.Id]);
                if (slot < 0) continue;
                occupiedUntil[slot] = order[slur.End.Id];
                slur.Number = slot + 1;
            }
            return slurs;
        }
    }
}
