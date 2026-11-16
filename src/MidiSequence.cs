using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ValeScore
{
    public sealed class MidiEvent
    {
        public long Tick;
        public double Seconds;
        public int Status, Data1, Data2;
        public int Track;
        internal int Tempo;
    }

    public sealed class MidiSequence
    {
        public MidiEvent[] Events { get; private set; }
        public double Duration { get; private set; }
        public int Tracks { get; private set; }
        public int Division { get; private set; }
        public int InitialTempo { get; private set; }
        public bool HasTempoChanges { get; private set; }
        public bool HasOtherMeter { get; private set; }

        private MidiSequence() { }

        public static MidiSequence Open(string path)
        {
            using (var stream = File.OpenRead(path)) return Read(stream);
        }

        public static MidiSequence Read(Stream stream)
        {
            if (stream.Length < 14 || stream.Length > 8 * 1024 * 1024)
                throw new InvalidDataException("O arquivo MIDI deve ter entre 14 bytes e 8 MB.");
            var events = new List<MidiEvent>();
            long lastTick = 0;
            bool otherMeter = false;
            int division, tracks;
            using (var reader = new BinaryReader(stream, Encoding.ASCII, true))
            {
                if (Tag(reader) != "MThd") throw new InvalidDataException("Cabeçalho MIDI inválido.");
                uint length = Number(reader, 4);
                if (length < 6 || length > stream.Length - stream.Position) throw new InvalidDataException("Cabeçalho MIDI incompleto.");
                int format = (int)Number(reader, 2);
                tracks = (int)Number(reader, 2);
                division = (int)Number(reader, 2);
                if (format > 1 || tracks < 1 || tracks > 128 || (format == 0 && tracks != 1) || division < 1 || division > 32767)
                    throw new InvalidDataException("Use MIDI tipo 0 ou 1 com resolução PPQN e até 128 pistas.");
                stream.Position += length - 6;
                int count = 0;
                while (count < tracks && stream.Position < stream.Length)
                {
                    string chunk = Tag(reader);
                    uint size = Number(reader, 4);
                    if (size > stream.Length - stream.Position) throw new InvalidDataException("Pista MIDI incompleta.");
                    if (chunk != "MTrk") { stream.Position += size; continue; }
                    lastTick = Math.Max(lastTick, ReadTrack(reader.ReadBytes((int)size), events, count, ref otherMeter));
                    count++;
                }
                if (count != tracks) throw new InvalidDataException("O arquivo tem pistas ausentes.");
            }

            int tempo = 500000;
            int initialTempo = tempo;
            bool tempoChanges = false;
            long previous = 0;
            double time = 0;
            var timed = new List<MidiEvent>();
            foreach (var item in events.OrderBy(e => e.Tick))
            {
                time += (item.Tick - previous) * (double)tempo / (division * 1000000.0);
                previous = item.Tick;
                if (item.Tempo != 0)
                {
                    if (item.Tick == 0) initialTempo = item.Tempo;
                    else if (tempo != item.Tempo) tempoChanges = true;
                    tempo = item.Tempo;
                }
                else
                {
                    item.Seconds = time;
                    timed.Add(item);
                }
            }
            time += (lastTick - previous) * (double)tempo / (division * 1000000.0);
            if (time > 3600) throw new InvalidDataException("O limite de reprodução MIDI é de uma hora.");
            return new MidiSequence
            {
                Events = timed.ToArray(), Duration = time, Tracks = tracks, Division = division,
                InitialTempo = initialTempo, HasTempoChanges = tempoChanges, HasOtherMeter = otherMeter
            };
        }

        private static long ReadTrack(byte[] data, List<MidiEvent> events, int track, ref bool otherMeter)
        {
            long tick = 0;
            int offset = 0, running = 0;
            while (offset < data.Length)
            {
                tick += Variable(data, ref offset);
                int status = Byte(data, ref offset);
                if (status < 0x80)
                {
                    offset--;
                    status = running;
                    if (status == 0) throw new InvalidDataException("Evento MIDI sem status.");
                }

                if (status < 0xF0)
                {
                    running = status;
                    int kind = status & 0xF0;
                    int one = Byte(data, ref offset);
                    int two = kind == 0xC0 || kind == 0xD0 ? 0 : Byte(data, ref offset);
                    if (one > 127 || two > 127) throw new InvalidDataException("Evento MIDI inválido.");
                    Add(events, new MidiEvent { Tick = tick, Status = status, Data1 = one, Data2 = two, Track = track });
                    continue;
                }

                running = 0;
                if (status != 0xFF && status != 0xF0 && status != 0xF7)
                    throw new InvalidDataException("Evento de sistema MIDI inválido.");
                int type = status == 0xFF ? Byte(data, ref offset) : -1;
                int size = Variable(data, ref offset);
                if (size > data.Length - offset) throw new InvalidDataException("Evento MIDI incompleto.");
                if (type == 0x2F)
                {
                    if (size != 0) throw new InvalidDataException("Fim de pista MIDI inválido.");
                    return tick;
                }
                if (type == 0x51)
                {
                    if (size != 3) throw new InvalidDataException("Andamento MIDI inválido.");
                    int tempo = (data[offset] << 16) | (data[offset + 1] << 8) | data[offset + 2];
                    if (tempo == 0) throw new InvalidDataException("Andamento MIDI inválido.");
                    Add(events, new MidiEvent { Tick = tick, Tempo = tempo });
                }
                if (type == 0x58)
                {
                    if (size != 4) throw new InvalidDataException("Fórmula de compasso MIDI inválida.");
                    otherMeter |= data[offset] != 4 || data[offset + 1] != 2;
                }
                offset += size;
            }
            throw new InvalidDataException("Pista MIDI sem evento de fim.");
        }

        private static void Add(List<MidiEvent> events, MidiEvent item)
        {
            if (events.Count >= 250000) throw new InvalidDataException("O arquivo excede 250 mil eventos MIDI.");
            events.Add(item);
        }

        private static int Byte(byte[] data, ref int offset)
        {
            if (offset >= data.Length) throw new InvalidDataException("Evento MIDI incompleto.");
            return data[offset++];
        }

        private static int Variable(byte[] data, ref int offset)
        {
            int value = 0;
            for (int i = 0; i < 4; i++)
            {
                int next = Byte(data, ref offset);
                value = (value << 7) | (next & 127);
                if (next < 128) return value;
            }
            throw new InvalidDataException("Delta MIDI inválido.");
        }

        private static string Tag(BinaryReader reader) { return Encoding.ASCII.GetString(reader.ReadBytes(4)); }

        private static uint Number(BinaryReader reader, int bytes)
        {
            uint value = 0;
            for (int i = 0; i < bytes; i++) value = (value << 8) | reader.ReadByte();
            return value;
        }

        private sealed class EventData
        {
            public int Tick, Order;
            public byte[] Data;
        }

        public static void Export(Score score, Stream stream)
        {
            score.Validate();
            int tempo = (int)Math.Round(60000000.0 / score.Tempo);
            using (var writer = new BinaryWriter(stream, Encoding.ASCII, true))
            {
                writer.Write(Encoding.ASCII.GetBytes("MThd"));
                BigEndian(writer, 6, 4);
                BigEndian(writer, 1, 2);
                BigEndian(writer, 1 + score.Parts.Count, 2);
                BigEndian(writer, 480, 2);
                WriteTrack(writer, new[] {
                    new EventData { Data = new byte[] { 0xFF, 0x51, 3, (byte)(tempo >> 16), (byte)(tempo >> 8), (byte)tempo } },
                    new EventData { Data = new byte[] { 0xFF, 0x58, 4, 4, 2, 24, 8 } }
                }, score.Bars * 1920);

                for (int channel = 0; channel < score.Parts.Count; channel++)
                {
                    var part = score.Parts[channel];
                    var name = Encoding.UTF8.GetBytes(part.Name);
                    var data = new List<EventData> {
                        new EventData { Order = -3, Data = new byte[] { 0xFF, 3, (byte)name.Length }.Concat(name).ToArray() },
                        new EventData { Order = -2, Data = new byte[] { (byte)(0xC0 | channel),
                            (byte)(part.Instrument == "Violão" ? 24 : part.Instrument == "Voz" ? 52 : 0) } }
                    };

                    // Some synths stop every instance of a pitch on note-off. Merge overlapping
                    // instances of the same pitch on a channel while retaining adjacent attacks.
                    var attacks = new List<Tuple<Note, int>>();
                    foreach (var staff in part.Staves)
                    {
                        int loudness = 88;
                        foreach (var note in staff.Where(n => !n.IsRest))
                        {
                            if (note.Dynamic != null) loudness = Score.Velocity(note.Dynamic);
                            attacks.Add(Tuple.Create(note, loudness));
                        }
                    }
                    foreach (var group in attacks.GroupBy(n => n.Item1.Midi))
                    {
                        int start = -1, end = -1, velocity = 88;
                        foreach (var attack in group.OrderBy(n => n.Item1.Tick))
                        {
                            var note = attack.Item1;
                            int onset = note.Tick * 120;
                            int release = onset + (int)Math.Round(note.Length * 120 * Articulations.SoundLength(note.Articulation));
                            if (onset < end) { end = Math.Max(end, release); continue; }
                            if (start >= 0) AddPitch(data, channel, group.Key, start, end, velocity);
                            start = onset;
                            end = release;
                            velocity = Articulations.Attack(note.Articulation, attack.Item2);
                        }
                        AddPitch(data, channel, group.Key, start, end, velocity);
                    }
                    WriteTrack(writer, data, score.Bars * 1920);
                }
            }
        }

        private static void AddPitch(List<EventData> events, int channel, int pitch, int start, int end, int velocity)
        {
            events.Add(new EventData { Tick = start, Order = 1, Data = new[] { (byte)(0x90 | channel), (byte)pitch, (byte)velocity } });
            events.Add(new EventData { Tick = end, Data = new[] { (byte)(0x80 | channel), (byte)pitch, (byte)0 } });
        }

        private static void WriteTrack(BinaryWriter output, IEnumerable<EventData> events, int end)
        {
            using (var track = new MemoryStream())
            using (var writer = new BinaryWriter(track))
            {
                int previous = 0;
                foreach (var item in events.OrderBy(e => e.Tick).ThenBy(e => e.Order))
                {
                    WriteVariable(writer, item.Tick - previous);
                    writer.Write(item.Data);
                    previous = item.Tick;
                }
                WriteVariable(writer, end - previous);
                writer.Write(new byte[] { 0xFF, 0x2F, 0 });
                output.Write(Encoding.ASCII.GetBytes("MTrk"));
                BigEndian(output, (int)track.Length, 4);
                output.Write(track.ToArray());
            }
        }

        private static void BigEndian(BinaryWriter writer, int value, int count)
        {
            for (int i = count - 1; i >= 0; i--) writer.Write((byte)(value >> (i * 8)));
        }

        private static void WriteVariable(BinaryWriter writer, int value)
        {
            int shift = 0;
            while ((value >> (shift + 7)) != 0) shift += 7;
            for (; shift > 0; shift -= 7) writer.Write((byte)(((value >> shift) & 127) | 128));
            writer.Write((byte)(value & 127));
        }
    }
}
