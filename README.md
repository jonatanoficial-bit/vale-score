# Vale Score

**0.15.0 · Prévia para Windows x64**

O [inventário obrigatório da versão final](REQUISITOS-FINAIS.md) reúne os critérios de aceite do produto e o estado de cada recurso.

[Baixar a versão portátil](https://github.com/jonatanoficial-bit/vale-score/raw/refs/heads/main/downloads/ValeScore-0.15.0-win-x64.zip) · [SHA-256](https://github.com/jonatanoficial-bit/vale-score/blob/main/downloads/ValeScore-0.15.0-win-x64.zip.sha256)

Editor de partituras desktop em português, com projetos locais e funcionamento offline. Extraia o ZIP e abra `ValeScore.exe`. Na Central de projetos, abra um exemplo, escolha um modelo ou carregue seu projeto.

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

A prévia suporta até 64 compassos, uma linha melódica e uma linha de letra por pauta, sustenidos e bemóis, e figuras de semibreve a semicolcheia. A entrada de letra aceita até 24 caracteres por nota. Em compassos excepcionalmente densos o desenho pode abreviar sílabas, com o texto integral disponível na edição e no MusicXML. A distribuição é portátil e sem assinatura digital.

Use **Importar MIDI** ou **Ctrl+I** para criar um projeto editável. A conversão usa semicolcheias, fórmula inicial compatível e andamento inicial, e distribui as notas em até quatro pianos com uma voz por pauta. O aplicativo informa ajustes e omissões. O MIDI original fica disponível em **Reprodução → Tocar ou parar MIDI aberto**. O som interno de arquivos MIDI usa um timbre simples, lê andamento e pedal e tem limite de 10 minutos. A saída MIDI do Windows permite usar os timbres do dispositivo selecionado.

## Compilar e testar

No PowerShell em Windows x64 com .NET Framework 4.x:

```powershell
.\build.ps1 -Test
```

O executável sai em `dist\ValeScore.exe`. A suíte exercita a abertura de projetos anteriores, a edição na janela, a entrada de sílabas, o espaçamento e as exportações. Consulte [a decisão técnica](ADR.md) para o escopo da prévia.

## Novidades da 0.15

- O programa abre na **Central de projetos**, com criação por modelo, abertura de arquivo, projetos recentes e exemplos clicáveis.
- Ao criar uma partitura, escolha a formação e configure título, compositor, fórmula de compasso, tonalidade, quantidade de compassos e andamento.
- A central oferece **Prelúdio em dó**, **Valsa da tarde** e **Canção ao entardecer**. Cada exemplo abre como uma cópia editável.
- Os arquivos .valescore abertos ou salvos ficam na lista de recentes deste computador. Remover da lista conserva o arquivo.
- No editor, clique em **Vale Score** ou use **Ctrl+Shift+H** para voltar à central. **Continuar edição** retoma o mesmo projeto.

## Novidades da 0.14

- A área de edição ganhou uma paleta visual redesenhada, contraste distinto para passos ativos e foco por teclado, e uma página de partitura mais fácil de distinguir da mesa de trabalho.
- **Ctrl+K** busca comandos por nome ou categoria, aceitando palavras sem acentos. Use as setas e **Enter** para escolher e executar.
- **Ctrl+N** oferece seis modelos de formação e permite reutilizar os instrumentos, a armadura, a fórmula e o andamento do projeto atual em uma nova partitura vazia.
- **Editar → Renomear instrumento selecionado** atualiza a pauta e a identificação das exportações.

## Novidades da 0.13

- **Ctrl+P** abre a prévia paginada. Escolha partitura completa ou instrumento selecionado, papel A4, Carta ou A3, retrato ou paisagem e margens de 5 a 35 mm. Cada ajuste repagina a prévia.
- **Imprimir / PDF** usa o driver de impressão do Windows. Para gerar PDF, selecione **Microsoft Print to PDF** quando disponível.
- **Páginas PNG** exporta um ZIP com todas as páginas numeradas do conteúdo escolhido. A configuração de publicação vale para a janela atual.

## Novidades da 0.12

- **Ctrl+J** ou **Trecho…** abre o ensaio por intervalo de compassos. Escolha tocar uma vez ou repetir em loop. **Reprodução → Tocar a partir do compasso atual** inicia no cursor e segue até o fim.
- O trecho usa a velocidade escolhida no transporte e conserva pausas, articulações e a dinâmica vigente no início. Funciona com som interno e com saída MIDI selecionada.
- O cursor de reprodução também aparece durante as pausas. **Reprodução → Acompanhar cursor na partitura** controla a rolagem e a troca automática de páginas.

## Novidades da 0.11

- **Editar → Armadura de clave** oferece armaduras tradicionais maiores e menores, com até sete sustenidos ou bemóis, para toda a partitura. A alteração da armadura preserva a altura das notas existentes.
- **Editar → Grafia e acidentes** inclui dobrado bemol, dobrado sustenido, acidente de cortesia e grafia enarmônica para o grau vizinho. A entrada automática segue a armadura e os acidentes vigentes no compasso.
- MusicXML e MXL preservam a armadura inicial, as alterações duplas e os acidentes de cortesia compatíveis. MIDI preserva armadura inicial e alturas soantes. A grafia importada do MIDI é inferida.
- O esquema nativo 8 grava armadura e cortesia. A migração conserva a fórmula de compasso dos projetos 0.10.

## Novidades da 0.10

- **Editar → Fórmula de compasso** oferece 2/4, 3/4, 4/4, 6/8, 9/8 e 12/8. A fórmula é fixa para a partitura. Ao mudar, as notas conservam a posição dentro do compasso. Se uma figura ultrapassar a nova barra, o editor recusa a mudança.
- O controle de velocidade vai de **50% a 150%** para ensaiar a partitura com som interno ou saída MIDI. A velocidade escolhida vale ao iniciar a próxima reprodução e não altera o projeto nem os arquivos exportados.
- **Reprodução → Metrônomo da partitura** acentua o início do compasso e marca o pulso composto. Funciona com **Som interno** durante a reprodução da partitura.
- MusicXML e MXL preservam fórmulas fixas compatíveis. MIDI exporta a fórmula e a importação editável aproveita a fórmula inicial quando compatível. O andamento continua em semínimas por minuto.
- O esquema nativo 7 salva a fórmula de compasso. Projetos dos esquemas anteriores abrem em 4/4.

## Novidades da 0.9

- Selecione uma nota e abra **Editar → Articulação da nota** ou **Ctrl+H**. Há staccato, staccatissimo, tenuto, acento e marcato.
- Cada nota aceita uma articulação, preservada no projeto e no MusicXML/MXL. No MIDI e no WAV, as marcas ajustam a duração soante ou a força do ataque conforme a articulação.

## Novidades da 0.8

- **Ctrl+H** abre **Cifra e dinâmica** na nota selecionada. Use cifras como Cmaj7, F#m7, Bbadd9 e G7/B. As cifras são exibidas acima da pauta e acompanham a transposição.
- Dinâmicas de **ppp a fff** valem na pauta até a próxima marca e alteram o volume interno, o WAV e a velocidade MIDI.
- MusicXML e MXL transportam as cifras e dinâmicas compatíveis. A cifra funciona como indicação para o músico. O acompanhamento automático de acordes permanece pendente.
- O esquema nativo 5 grava a expressão. Projetos dos esquemas 1 a 4 abrem na 0.8.

## Novidades da 0.7

- **Arquivo → Importar MusicXML ou MXL** abre partituras editáveis com relatório. A prévia aceita score-partwise com fórmula fixa compatível com até quatro partes, duas pautas por parte e uma voz por pauta. Acordes e outras vozes simultâneas podem ser omitidos. O arquivo original fica intacto.
- **Exportar → MusicXML compactado** gera um arquivo MXL interoperável com leitores que implementam o [contêiner MusicXML](https://www.w3.org/2021/06/musicxml40/tutorial/compressed-mxl-files/).
- A paleta **Pontos** aplica ou retira um ou dois pontos em notas e pausas quando a duração cabe na grade. Para um ponto, também vale a tecla de ponto.
- O menu **Editar** insere ou remove compassos e copia ou cola o conteúdo de um compasso nas pautas correspondentes. **Ctrl+Shift+C** copia, **Ctrl+Shift+V** cola substituindo o conteúdo do compasso atual e **Ctrl+Z** desfaz. A cópia permanece disponível durante a sessão.
- O esquema nativo 4 acrescenta figuras pontuadas. Projetos dos esquemas 1, 2 e 3 abrem normalmente na 0.7.

## Novidades da 0.6

- **Ctrl+G** navega por número de compasso.
- **Ctrl+D** duplica o compasso atual em todas as pautas e desloca o material posterior.
- **Ctrl+T** transpõe um trecho por semitons ou graus. A opção diatônica usa graus naturais e conserva o acidente original.
- **Exportar → Instrumento selecionado** gera um MusicXML apenas com o instrumento ativo, incluindo todas as suas pautas.
- **Exportar → Página atual** gera um PNG de 1560 × 2200 pixels.
- A recuperação grava uma cópia dos projetos editados a cada 12 segundos se houver mudanças. Após uma interrupção, use **Arquivo → Recuperar projeto**. Salvar ou fechar normalmente limpa a cópia da sessão.

## Formato de projeto

JSON UTF-8, versão de esquema `8`. A versão 0.11 abre projetos dos esquemas 1 a 7 e grava no esquema 8. Cada instrumento tem uma ou duas pautas, com eventos de ID estável e posição e duração em semicolcheias. Notas contêm altura, alteração e letra opcional. Pausas explícitas conservam a duração escolhida e intervalos vazios geram pausas automaticamente.
