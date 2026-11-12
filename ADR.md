# Decisão 001 — Primeira versão funcional

Status: adotada para a prévia 0.1.0.

Tauri 2 com React/TypeScript e Verovio é candidato à editoração avançada. Verovio documenta entrada MusicXML e saída SVG. O editor ainda precisaria manter o próprio modelo musical e mapear a seleção à notação gerada. A comparação prática e a auditoria de redistribuição desse conjunto ficam pendentes.

Para a prévia foi escolhido C# com Windows Forms e desenho vetorial GDI+, com compilador local do .NET Framework e sem pacotes externos. O modelo de partitura, a paginação e o sintetizador são independentes da interface. A prova inicial exercita uma grade de semicolcheias, pauta dupla e alinhamento de compassos.

1. Consolidar edição, formato nativo, histórico e a renderização básica.
2. Integrar áudio, MusicXML de saída e impressão com o mesmo modelo.
3. Antes da versão comercial, ampliar os casos rítmicos, avaliar Verovio com partituras de referência, implementar intercâmbio e entrada MIDI e validar instalador, acessibilidade e desempenho.

Critérios da prévia: inserir e alterar notas sem sobrepor eventos da mesma pauta; preservar o projeto ao salvar e abrir; gerar MusicXML com compassos completos e WAV audível; executar no Windows x64. Critérios editoriais e de paridade da especificação permanecem pendentes.

Referências: [Windows Forms](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/overview/) e [Verovio](https://book.verovio.org/toolkit-reference/toolkit-options.html).
