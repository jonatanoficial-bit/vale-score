using System;

namespace ValeScore
{
    internal sealed class ExampleScore
    {
        public readonly string Title, Detail;
        public readonly Func<Score> Create;
        public ExampleScore(string title, string detail, Func<Score> create) { Title = title; Detail = detail; Create = create; }

        public static readonly ExampleScore[] Library = {
            new ExampleScore("Prelúdio em dó", "Piano · letra e pauta dupla", () => Score.New(true)),
            new ExampleScore("Valsa da tarde", "Piano · 3/4 e dinâmica", Waltz),
            new ExampleScore("Canção ao entardecer", "Melodia · cifras e articulações", LeadSheet)
        };

        private static Score Waltz()
        {
            var score = Score.New(false);
            score.Title = "Valsa da tarde";
            score.Author = "Partitura de exemplo";
            score.SetMeter(3, 4);
            score.Tempo = 118;
            int[] melody = { 32, 34, 35, 36, 35, 34, 33, 35, 32, 29, 30, 28 };
            int[] bass = { 21, 25, 25, 24, 26, 26, 25, 27, 27, 21, 23, 23 };
            for (int i = 0; i < melody.Length; i++)
            {
                score.Put(0, 0, i * 4, 4, melody[i], 0);
                score.Put(0, 1, i * 4, 4, bass[i], 0);
            }
            score.SetExpression(0, 0, 0, null, "mp");
            score.SetExpression(0, 0, 24, null, "f");
            return score;
        }

        private static Score LeadSheet()
        {
            var score = new Score { Title = "Canção ao entardecer", Author = "Partitura de exemplo", Tempo = 84 };
            score.AddPart("Voz");
            score.SetKey(1, false);
            int[] notes = { 32, 34, 36, 35, 34, 33, 31, 30, 32, 35, 36, 37, 36, 35, 34, 32 };
            for (int i = 0; i < notes.Length; i++)
                score.Put(0, 0, i * 4, 4, notes[i], KeySignature.Effective(1, notes[i]));
            score.SetExpression(0, 0, 0, "Gmaj7", "mf");
            score.SetExpression(0, 0, 16, "D/F#", null);
            score.SetExpression(0, 0, 32, "Em7", null);
            score.SetExpression(0, 0, 48, "Cadd9", null);
            score.SetArticulation(0, 0, 4, "staccato");
            score.SetArticulation(0, 0, 60, "tenuto");
            return score;
        }
    }
}
