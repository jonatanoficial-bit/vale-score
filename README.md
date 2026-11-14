# Vale Score

**0.6.0 · Prévia para Windows x64**

O [inventário obrigatório da versão final](REQUISITOS-FINAIS.md) reúne os critérios de aceite do produto e o estado de cada recurso.

[Baixar a versão portátil](https://github.com/jonatanoficial-bit/vale-score/raw/refs/heads/main/downloads/ValeScore-0.6.0-win-x64.zip) · [SHA-256](https://github.com/jonatanoficial-bit/vale-score/blob/main/downloads/ValeScore-0.6.0-win-x64.zip.sha256)

Editor de partituras desktop em português, com projetos locais e funcionamento offline. Extraia o ZIP e abra `ValeScore.exe`. A partitura de exemplo inclui notas e letras para experimentar a edição.

- **Escrever.** Escolha a figura na paleta e clique na pauta. Passe o mouse pelo símbolo para ver o nome. No modo **Notas** (**N**), **A S D F G H J** inserem Dó a Si. As teclas **1–5** selecionam a figura da ferramenta ativa.
- **Inserir pausas.** Clique no símbolo da pausa desejada ou use **R** e escolha a duração. Clique na pauta ou use **Enter** para inserir no cursor. A duração escolhida é salva e exportada.
- **Editar.** Em **Selecionar** (**V**), clique numa nota ou pausa. A paleta muda a duração da figura selecionada. Em notas, **↑** e **↓** mudam a altura e **♭**, **♮** e **♯** alteram o acidente. Use **Delete** ou **Apagar** (**E**) para retirar a figura.
- **Ler a partitura.** Colcheias ou semicolcheias consecutivas de mesma duração são ligadas dentro do tempo. As barras de compasso unem as duas pautas do piano.
- **Colocar letra.** Selecione uma nota e pressione **Shift+L**. Digite sob a pauta. **Espaço** encerra a palavra e **hífen** separa sílabas. Ambos avançam para a próxima nota na mesma pauta. **Enter** confirma, **Esc** cancela o texto atual e **Shift+Tab** volta à nota anterior.
- **Navegar.** Use o zoom e as setas de página na barra inferior. **F1** abre a lista de atalhos.
- **Instrumentar.** Adicione até quatro instrumentos. Piano usa pautas de sol e fá. Violão e voz usam uma pauta em clave de sol.
- **Ouvir.** Use **Tocar** ou **Espaço** para a partitura. **Abrir MIDI** carrega e toca um arquivo MIDI tipo 0 ou 1. Em **Reprodução**, é possível repetir o MIDI carregado. Selecione **Som interno** ou uma saída MIDI do Windows no transporte.
- **Escrever com teclado MIDI.** Em **Reprodução → Teclado MIDI**, selecione a entrada e escolha uma figura. Cada tecla insere uma nota e avança o cursor na pauta ativa. A entrada é nota por nota, com a duração escolhida na paleta.
- **Exportar.** O botão **Exportar** oferece MIDI tipo 1 com uma pista por instrumento, MusicXML com letras, WAV e impressão. Para PDF, escolha **Microsoft Print to PDF** quando disponível no Windows.
- **Salvar.** O formato `.valescore` armazena a partitura localmente. Ctrl+S salva. Ctrl+Z e Ctrl+Y navegam pelo histórico de edição.

A prévia suporta até 64 compassos em 4/4, uma linha melódica e uma linha de letra por pauta, sustenidos e bemóis, e figuras de semibreve a semicolcheia. A entrada de letra aceita até 24 caracteres por nota. Em compassos excepcionalmente densos o desenho pode abreviar sílabas, com o texto integral disponível na edição e no MusicXML. A distribuição é portátil e sem assinatura digital.

Use **Importar MIDI** ou **Ctrl+I** para criar um projeto editável. A conversão usa semicolcheias, 4/4 e andamento inicial, e distribui as notas em até quatro pianos com uma voz por pauta. O aplicativo informa ajustes e omissões. O MIDI original fica disponível em **Reprodução → Tocar ou parar MIDI aberto**. O som interno de arquivos MIDI usa um timbre simples, lê andamento e pedal e tem limite de 10 minutos. A saída MIDI do Windows permite usar os timbres do dispositivo selecionado.

## Compilar e testar

No PowerShell em Windows x64 com .NET Framework 4.x:

```powershell
.\build.ps1 -Test
```

O executável sai em `dist\ValeScore.exe`. A suíte exercita a abertura de projetos anteriores, a edição na janela, a entrada de sílabas, o espaçamento e as exportações. Consulte [a decisão técnica](ADR.md) para o escopo da prévia.

## Novidades da 0.6

- **Ctrl+G** navega por número de compasso.
- **Ctrl+D** duplica o compasso atual em todas as pautas e desloca o material posterior.
- **Ctrl+T** transpõe um trecho por semitons ou graus. A opção diatônica usa graus naturais e conserva o acidente original.
- **Exportar → Instrumento selecionado** gera um MusicXML apenas com o instrumento ativo, incluindo todas as suas pautas.
- **Exportar → Página atual** gera um PNG de 1560 × 2200 pixels.
- A recuperação grava uma cópia dos projetos editados a cada 12 segundos se houver mudanças. Após uma interrupção, use **Arquivo → Recuperar projeto**. Salvar ou fechar normalmente limpa a cópia da sessão.

## Formato de projeto

JSON UTF-8, versão de esquema `3`. A versão 0.3 abre projetos dos esquemas 1 e 2 e grava no esquema 3. Cada instrumento tem uma ou duas pautas, com eventos de ID estável e posição e duração em semicolcheias. Notas contêm altura, alteração e letra opcional. Pausas explícitas conservam a duração escolhida e intervalos vazios geram pausas automaticamente.
