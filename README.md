# Vale Score

**0.3.0 · Prévia para Windows x64**

[Baixar a versão portátil](https://github.com/jonatanoficial-bit/vale-score/raw/refs/heads/main/downloads/ValeScore-0.3.0-win-x64.zip) · [SHA-256](https://github.com/jonatanoficial-bit/vale-score/blob/main/downloads/ValeScore-0.3.0-win-x64.zip.sha256)

Editor de partituras desktop em português, com projetos locais e funcionamento offline. Extraia o ZIP e abra `ValeScore.exe`. A partitura de exemplo inclui notas e letras para experimentar a edição.

- **Escrever.** Escolha a figura na paleta e clique na pauta. Passe o mouse pelo símbolo para ver o nome. No modo **Notas** (**N**), **A S D F G H J** inserem Dó a Si. As teclas **1–5** selecionam a figura da ferramenta ativa.
- **Inserir pausas.** Clique no símbolo da pausa desejada ou use **R** e escolha a duração. Clique na pauta ou use **Enter** para inserir no cursor. A duração escolhida é salva e exportada.
- **Editar.** Em **Selecionar** (**V**), clique numa nota ou pausa. A paleta muda a duração da figura selecionada. Em notas, **↑** e **↓** mudam a altura e **♭**, **♮** e **♯** alteram o acidente. Use **Delete** ou **Apagar** (**E**) para retirar a figura.
- **Ler a partitura.** Colcheias ou semicolcheias consecutivas de mesma duração são ligadas dentro do tempo. As barras de compasso unem as duas pautas do piano.
- **Colocar letra.** Selecione uma nota e pressione **Shift+L**. Digite sob a pauta. **Espaço** encerra a palavra e **hífen** separa sílabas. Ambos avançam para a próxima nota na mesma pauta. **Enter** confirma, **Esc** cancela o texto atual e **Shift+Tab** volta à nota anterior.
- **Navegar.** Use o zoom e as setas de página na barra inferior. **F1** abre a lista de atalhos.
- **Instrumentar.** Adicione até quatro instrumentos. Piano usa pautas de sol e fá. Violão e voz usam uma pauta em clave de sol.
- **Ouvir e compartilhar.** Use Espaço para tocar ou parar fora da edição de letras. Exporte MusicXML com a letra ou WAV pelo menu Arquivo. Para PDF, escolha Microsoft Print to PDF na caixa de impressão, quando disponível.
- **Salvar.** O formato `.valescore` armazena a partitura localmente. Ctrl+S salva. Ctrl+Z e Ctrl+Y navegam pelo histórico de edição.

A prévia suporta até 64 compassos em 4/4, uma linha melódica e uma linha de letra por pauta, sustenidos e bemóis, e figuras de semibreve a semicolcheia. A entrada de letra aceita até 24 caracteres por nota. Em compassos excepcionalmente densos o desenho pode abreviar sílabas, com o texto integral disponível na edição e no MusicXML. A distribuição é portátil e sem assinatura digital.

## Compilar e testar

No PowerShell em Windows x64 com .NET Framework 4.x:

```powershell
.\build.ps1 -Test
```

O executável sai em `dist\ValeScore.exe`. A suíte exercita a abertura de projetos anteriores, a edição na janela, a entrada de sílabas, o espaçamento e as exportações. Consulte [a decisão técnica](ADR.md) para o escopo da prévia.

## Formato de projeto

JSON UTF-8, versão de esquema `3`. A versão 0.3 abre projetos dos esquemas 1 e 2 e grava no esquema 3. Cada instrumento tem uma ou duas pautas, com eventos de ID estável e posição e duração em semicolcheias. Notas contêm altura, alteração e letra opcional. Pausas explícitas conservam a duração escolhida e intervalos vazios geram pausas automaticamente.
