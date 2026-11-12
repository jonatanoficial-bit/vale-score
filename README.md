# Vale Score

**0.1.0 · Prévia para Windows x64**

[Baixar a versão portátil](https://github.com/jonatanoficial-bit/vale-score/raw/refs/heads/main/downloads/ValeScore-0.1.0-win-x64.zip) · [SHA-256](downloads/ValeScore-0.1.0-win-x64.zip.sha256)

Editor de partituras desktop em português, com projetos locais e funcionamento offline. Abra `ValeScore.exe` para começar. A janela inicial traz uma partitura de exemplo que pode ser editada e reproduzida.

- **Escrever.** Crie uma partitura, escolha a duração e clique na pauta. Clique direito para selecionar. Use Delete para remover a nota e restaurar a pausa. F1 abre os atalhos.
- **Instrumentar.** Adicione até quatro instrumentos. Piano usa pautas de sol e fá. Violão e voz usam uma pauta em clave de sol.
- **Ouvir e compartilhar.** Use Espaço para tocar ou parar. Exporte MusicXML ou WAV pelo menu Arquivo. Para PDF, escolha Microsoft Print to PDF na caixa de impressão, quando disponível.
- **Salvar.** O formato `.valescore` armazena a partitura localmente. Ctrl+S salva. Ctrl+Z e Ctrl+Y navegam pelo histórico de edição.

A prévia suporta até 64 compassos em 4/4, uma linha melódica por pauta, sustenidos e bemóis, e figuras de semibreve a semicolcheia. Polifonia na mesma pauta, MIDI, importação MusicXML/MXL, quiálteras, recuperação automática e editoração avançada fazem parte das etapas posteriores. A distribuição inicial é portátil e sem assinatura digital.

## Compilar e testar

No PowerShell em Windows x64 com .NET Framework 4.x:

```powershell
.\build.ps1 -Test
```

O executável sai em `dist\ValeScore.exe`. O teste exercita o modelo, os arquivos, a saída musical e a criação da janela. Consulte [a decisão técnica](ADR.md) para o escopo da prévia.

## Formato de projeto

JSON UTF-8, versão de esquema `1`. Cada instrumento contém uma ou duas listas de notas, uma por pauta. Cada nota possui ID, posição e duração em semicolcheias, grau diatônico inteiro (Dó4 = 28) e alteração em semitons. Intervalos vazios geram pausas. Título, compositor, andamento e quantidade de compassos pertencem à partitura.
