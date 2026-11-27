using System;

namespace ValeScore
{
    public sealed class ScoreTemplate
    {
        public static readonly ScoreTemplate[] Library = {
            new ScoreTemplate("Piano solo", "Pautas de sol e fá", "Piano"),
            new ScoreTemplate("Voz e piano", "Voz e piano com pauta dupla", "Voz", "Piano"),
            new ScoreTemplate("Melodia e cifras", "Pauta única para melodia, letra e cifras", "Voz"),
            new ScoreTemplate("Violão solo", "Pauta em clave de sol", "Violão"),
            new ScoreTemplate("Guitarra elétrica", "Pauta e TAB com afinação configurável", "Guitarra"),
            new ScoreTemplate("Baixo elétrico", "Pauta em clave de fá e TAB", "Baixo"),
            new ScoreTemplate("Voz e violão", "Duas partes para voz e violão", "Voz", "Violão"),
            new ScoreTemplate("Três pianos e violão", "Sete pautas em quatro partes", "Piano", "Piano", "Piano", "Violão")
        };
        public readonly string Title, Detail;
        private readonly string[] instruments;

        private ScoreTemplate(string title, string detail, params string[] instruments)
        {
            Title = title;
            Detail = detail;
            this.instruments = instruments;
        }

        public Score Create()
        {
            var score = new Score();
            foreach (var instrument in instruments) score.AddPart(instrument);
            score.Validate();
            return score;
        }
    }
}

