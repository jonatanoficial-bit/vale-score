# Requisitos obrigatórios para a versão final do Vale Score

Escopo aprovado em 14/11/2026. Inclui os 194 itens das 15 seções do Inventário de Recursos para Edição Profissional de Partituras, novembro de 2026, páginas 1–6.

Documento de origem: `Inventario_Profissional_Recursos_Editor_Partituras.pdf`. SHA-256: `0afb9e1d0e1878a1dfc95b02933269e519d65ae5db3b9fb92466912bc3ba1a0c`.

Cada linha é requisito de aceite da versão final, inclusive os itens que o inventário descreve como complementares ou condicionais. Os qualificadores delimitam a validação ou a forma de distribuição. O núcleo permanece utilizável offline. Nuvem e colaboração são recursos adicionais que requerem conexão. VST3 é obrigatório no produto final como integração opcional para quem usa instrumentos virtuais. Compatibilidade com Kontakt e Reason depende de ensaios com versões identificadas.

Pré-visualizações podem sair em etapas. Para declarar a versão final completa, todos os itens precisam de implementação, teste de aceite aprovado e evidência no repositório. Um item composto é concluído quando todos os componentes aplicáveis foram validados. Preserve os IDs e acrescente links de evidência ao atualizar a matriz.

Estados da matriz

- **Pendente** — falta implementação ou validação integral.
- **Parcial** — há implementação utilizável de parte do item, com lacunas de escopo ou de validação.
- **Validado** — todos os componentes foram verificados, com evidência vinculada.

As exigências anteriores da Bíblia Mestra do Vale Score continuam aplicáveis ao produto.

Base da matriz: prévia 0.10.0. A [suíte automatizada](tests/Smoke.cs) verifica, entre outros fluxos, a leitura de MIDI tipos 0 e 1, a exportação MIDI, a geração de sinal de áudio e a edição de notas, pausas e letras. Os testes em hardware MIDI, a compatibilidade entre editores e a validação comercial precisam de evidências próprias.

## 01 · Estrutura de projetos e organização

| ID | Requisito | Estado | Evidência de aceite |
| --- | --- | --- | --- |
| 01.01 | Projetos com múltiplas peças ou movimentos (flows) | Pendente | |
| 01.02 | Partituras gerais e partes instrumentais vinculadas | Pendente | |
| 01.03 | Múltiplos layouts e configurações de impressão | Pendente | |
| 01.04 | Instrumentistas que alternam instrumentos durante a obra | Pendente | |
| 01.05 | Conjuntos de câmara, coral, banda, big band e orquestra | Pendente | |
| 01.06 | Templates de instrumentação e modelos personalizados | Pendente | |
| 01.07 | Partes separadas e partitura do regente | Pendente | |
| 01.08 | Organização de instrumentos, pautas e grupos | Parcial | |
| 01.09 | Alterações de instrumento, clave e transposição | Pendente | |
| 01.10 | Instrumentos com uma ou várias pautas | Parcial | |
| 01.11 | Múltiplos projetos abertos e gerenciamento de arquivos | Pendente | |

## 02 · Entrada e edição musical

| ID | Requisito | Estado | Evidência de aceite |
| --- | --- | --- | --- |
| 02.01 | Inserção de notas por teclado, mouse e controlador MIDI | Parcial | |
| 02.02 | Entrada passo a passo e gravação MIDI em tempo real | Parcial | |
| 02.03 | Entrada de acordes e intervalos | Pendente | |
| 02.04 | Inserção de notas em diferentes vozes | Pendente | |
| 02.05 | Modo de inserção que desloca conteúdo rítmico | Pendente | |
| 02.06 | Seleção, filtros avançados e edição em massa | Pendente | |
| 02.07 | Copiar, colar, duplicar e colagem múltipla | Parcial | [Teste de cópia e colagem de um compasso](tests/Smoke.cs). Seleção de trechos e colagem múltipla pendentes. |
| 02.08 | Transposição cromática, diatônica e por intervalo | Parcial | [Testes](tests/Smoke.cs) de transposição por semitons e graus naturais. |
| 02.09 | Inversão, retrogradação e transformações musicais | Pendente | |
| 02.10 | Redução e expansão de material entre pautas | Pendente | |
| 02.11 | Distribuição de vozes e instrumentos | Pendente | |
| 02.12 | Mudanças globais de duração e ritmo | Pendente | |
| 02.13 | Desfazer/refazer e histórico de edição | Parcial | |
| 02.14 | Comandos rápidos e barra de busca/ações | Pendente | |
| 02.15 | Atalhos configuráveis e fluxos de trabalho com teclado | Parcial | |

## 03 · Ritmo, métrica e compassos

| ID | Requisito | Estado | Evidência de aceite |
| --- | --- | --- | --- |
| 03.01 | Figuras e pausas de valores longos e muito curtos | Parcial | |
| 03.02 | Notas pontuadas, duplamente e triplamente pontuadas | Parcial | [Teste do editor e do MusicXML](tests/Smoke.cs). Um e dois pontos conforme a grade. Triplo ponto pendente. |
| 03.03 | Anacruse e compassos incompletos | Pendente | |
| 03.04 | Fórmulas de compasso simples e compostas | Parcial | Seis fórmulas fixas. [Testes de métrica e intercâmbio](tests/Smoke.cs). |
| 03.05 | Fórmulas irregulares e alternadas | Pendente | |
| 03.06 | Mudanças de fórmula durante a peça | Pendente | |
| 03.07 | Quiálteras complexas e aninhadas | Pendente | |
| 03.08 | Agrupamento personalizado de hastes e barras | Parcial | |
| 03.09 | Pausas multicompasso e compassos de espera | Pendente | |
| 03.10 | Compassos de duração irregular | Pendente | |
| 03.11 | Polimetria e soluções de notação contemporânea | Pendente | |
| 03.12 | Ritmos sem barra e escrita livre conforme ferramenta | Pendente | |
| 03.13 | Notação de repetição de compassos e slash notation | Pendente | |
| 03.14 | Hastes parciais, barras em leque e tremolos | Pendente | |

## 04 · Altura, tonalidade e microtonalidade

| ID | Requisito | Estado | Evidência de aceite |
| --- | --- | --- | --- |
| 04.01 | Claves de sol, fá, dó, oitava e percussão | Parcial | |
| 04.02 | Armaduras tradicionais e personalizadas | Pendente | |
| 04.03 | Alterações simples, duplas e de cortesia | Parcial | |
| 04.04 | Enarmonia e alteração da grafia das notas | Pendente | |
| 04.05 | Afinação e transposição de instrumentos | Pendente | |
| 04.06 | Notas escritas versus alturas soantes | Pendente | |
| 04.07 | Acidentes microtonais e sistemas personalizados | Pendente | |
| 04.08 | Mudanças de clave e armadura no meio da música | Pendente | |
| 04.09 | Ossias e pautas auxiliares | Pendente | |
| 04.10 | Cabeças de nota personalizadas | Pendente | |
| 04.11 | Notação sem altura definida | Pendente | |

## 05 · Articulações e expressão

| ID | Requisito | Estado | Evidência de aceite |
| --- | --- | --- | --- |
| 05.01 | Staccato, staccatissimo, tenuto, marcato e acentos | Parcial | Uma marca por nota. [Testes de intercâmbio e reprodução](tests/Smoke.cs). |
| 05.02 | Ligaduras de expressão e prolongamento | Pendente | |
| 05.03 | Fermatas, respirações e cesuras | Pendente | |
| 05.04 | Trinados, mordentes e ornamentos | Pendente | |
| 05.05 | Glissandi, portamenti e arpejos | Pendente | |
| 05.06 | Tremolos de uma e várias notas | Pendente | |
| 05.07 | Dinâmicas de ppp a fff e níveis adicionais | Parcial | [Testes de MIDI, WAV e MusicXML](tests/Smoke.cs). Oito níveis de ppp a fff, sem níveis personalizados. |
| 05.08 | Crescendos, diminuendos e hairpins | Pendente | |
| 05.09 | Textos expressivos e instruções técnicas | Pendente | |
| 05.10 | Mudanças de tempo, ritardando e accelerando | Pendente | |
| 05.11 | Linhas de oitava, pedais e indicações de execução | Pendente | |
| 05.12 | Técnicas personalizadas e símbolos gráficos | Pendente | |

## 06 · Harmonia, jazz e música popular

| ID | Requisito | Estado | Evidência de aceite |
| --- | --- | --- | --- |
| 06.01 | Cifras de acordes complexas e personalizáveis | Parcial | [Testes de intercâmbio e transposição](tests/Smoke.cs). Qualidades comuns e baixos alternativos. Personalização pendente. |
| 06.02 | Acordes com tensões, alterações, inversões e baixos alternativos | Pendente | |
| 06.03 | Múltiplas linhas de cifras e extensores | Pendente | |
| 06.04 | Diagramas de acordes para instrumentos de cordas | Pendente | |
| 06.05 | Baixo cifrado e análise harmônica | Pendente | |
| 06.06 | Notação rítmica com barras e cabeças de acorde | Pendente | |
| 06.07 | Swing e interpretação rítmica | Pendente | |
| 06.08 | Articulações de jazz: fall, scoop, doit e plop | Pendente | |
| 06.09 | Notação de lead sheets | Pendente | |
| 06.10 | Arranjos automáticos ou semiautomáticos de vozes de acordes | Pendente | |
| 06.11 | Sinais de repetição, codas e casas de primeira/segunda vez | Pendente | |

## 07 · Instrumentos especializados

| ID | Requisito | Estado | Evidência de aceite |
| --- | --- | --- | --- |
| 07.01 | Piano com duas ou mais pautas | Parcial | |
| 07.02 | Cruzamento de notas e barras entre pautas | Pendente | |
| 07.03 | Pedais e indicações de dedilhado para teclado | Pendente | |
| 07.04 | Violão, guitarra e baixo com tablatura | Pendente | |
| 07.05 | Afinações alternativas, capotraste e diagramas de acordes | Pendente | |
| 07.06 | Harmônicos, bends, slides e técnicas de cordas | Pendente | |
| 07.07 | Divisi de cordas orquestrais | Pendente | |
| 07.08 | Indicações de arco e técnicas estendidas | Pendente | |
| 07.09 | Notação de bateria e percussão sem altura definida | Pendente | |
| 07.10 | Mapeamento personalizado de instrumentos de percussão | Pendente | |
| 07.11 | Pedais e diagramas de harpa | Pendente | |
| 07.12 | Instrumentos transpositores de sopros e metais | Pendente | |
| 07.13 | Cues e indicações de instrumentos de apoio | Pendente | |
| 07.14 | Percussão de marcha e instrumentos étnicos quando disponíveis | Pendente | |

## 08 · Voz, coral e texto

| ID | Requisito | Estado | Evidência de aceite |
| --- | --- | --- | --- |
| 08.01 | Letras sob as notas e múltiplas estrofes | Parcial | |
| 08.02 | Melismas, hifens e extensores silábicos | Parcial | |
| 08.03 | Coral SATB e sistemas vocais complexos | Pendente | |
| 08.04 | Nomes de cantores e indicações de solista | Pendente | |
| 08.05 | Texto de sistema, pauta e expressão | Pendente | |
| 08.06 | Marcas de ensaio e numeração de compassos | Parcial | |
| 08.07 | Títulos, subtítulos, dedicatórias e créditos | Parcial | |
| 08.08 | Cabeçalhos, rodapés e numeração de páginas | Parcial | |
| 08.09 | Fontes de texto, estilos e formatação global | Pendente | |
| 08.10 | Anotações editoriais e instruções de execução | Pendente | |

## 09 · Editoração e gravação musical

| ID | Requisito | Estado | Evidência de aceite |
| --- | --- | --- | --- |
| 09.01 | Layout automático de qualidade editorial | Parcial | |
| 09.02 | Prevenção de colisões entre símbolos | Parcial | |
| 09.03 | Espaçamento horizontal e vertical inteligente | Parcial | |
| 09.04 | Controle manual de espaçamento rítmico | Pendente | |
| 09.05 | Ajuste gráfico fino de objetos individuais | Pendente | |
| 09.06 | Quebras de sistema e de página | Pendente | |
| 09.07 | Páginas-mestre e modelos de página | Pendente | |
| 09.08 | Margens, molduras de música e texto | Pendente | |
| 09.09 | Tamanho de pauta e escalas diferentes | Pendente | |
| 09.10 | Paginação automática de partes | Pendente | |
| 09.11 | Posicionamento de cifras, letras e dinâmicas | Pendente | |
| 09.12 | Ocultação de pautas vazias | Pendente | |
| 09.13 | Condensação automática de partituras orquestrais | Pendente | |
| 09.14 | Divisi e recombinação de seções | Pendente | |
| 09.15 | Cues vinculados e partes dinâmicas | Pendente | |
| 09.16 | Cutaway scores e trechos com pautas ocultadas | Pendente | |
| 09.17 | Controle de cabeçalhos de movimentos | Pendente | |
| 09.18 | Fontes musicais compatíveis com SMuFL | Pendente | |
| 09.19 | Regras globais de gravação e substituições locais | Pendente | |
| 09.20 | Visualização contínua, paginada e preenchimento da janela | Parcial | |

## 10 · Revisão, correção e produtividade

| ID | Requisito | Estado | Evidência de aceite |
| --- | --- | --- | --- |
| 10.01 | Verificação automática de possíveis erros de partitura | Pendente | |
| 10.02 | Avisos de notação potencialmente ambígua | Pendente | |
| 10.03 | Ferramentas de revisão de compassos e durações | Parcial | |
| 10.04 | Filtros por símbolo, instrumento, voz e seleção | Pendente | |
| 10.05 | Localização e navegação rápida por compassos | Parcial | Ctrl+G navega por número de compasso. |
| 10.06 | Comparação visual e inspeção de partes | Pendente | |
| 10.07 | Comentários e anotações de revisão quando suportados | Pendente | |
| 10.08 | Personalização de opções de notação | Pendente | |
| 10.09 | Configurações globais e locais por projeto | Pendente | |
| 10.10 | Atalhos e comandos rápidos | Parcial | |

## 11 · Reprodução, MIDI e instrumentos virtuais

| ID | Requisito | Estado | Evidência de aceite |
| --- | --- | --- | --- |
| 11.01 | Motor de reprodução sincronizado com a partitura | Parcial | |
| 11.02 | Mixer com canais, volume, pan e mute/solo | Pendente | |
| 11.03 | Instrumentos virtuais VST de terceiros | Pendente | |
| 11.04 | Bibliotecas de sons integradas ou opcionais | Parcial | |
| 11.05 | Mapas de expressão para articulações | Pendente | |
| 11.06 | Interpretação de dinâmicas e técnicas de execução | Pendente | |
| 11.07 | Editor MIDI de piano roll integrado | Pendente | |
| 11.08 | Edição de velocidade e duração de notas | Pendente | |
| 11.09 | Automação de controladores MIDI | Pendente | |
| 11.10 | Mapas de tempo e mudanças graduais | Parcial | |
| 11.11 | Reprodução de repetições e saltos | Pendente | |
| 11.12 | Reprodução cíclica de trechos | Pendente | |
| 11.13 | Humanização e swing | Pendente | |
| 11.14 | Efeitos de áudio, reverberação e processamento | Pendente | |
| 11.15 | Configuração de dispositivos e motor de áudio | Parcial | |
| 11.16 | Exportação de áudio WAV/MP3 quando suportada | Parcial | |

## 12 · Importação, exportação e publicação

| ID | Requisito | Estado | Evidência de aceite |
| --- | --- | --- | --- |
| 12.01 | Importação de MusicXML | Parcial | [Testes MusicXML e MXL](tests/Smoke.cs). Seis fórmulas fixas, até quatro partes, duas pautas e uma voz por pauta. |
| 12.02 | Exportação de MusicXML | Parcial | |
| 12.03 | Importação de MIDI | Parcial | [Suíte Windows](tests/Smoke.cs). Conversão em grade fixa. Métrica variável, percussão e vozes independentes pendentes. |
| 12.04 | Exportação de MIDI | Parcial | |
| 12.05 | Exportação de PDF para impressão | Parcial | |
| 12.06 | Exportação de imagens e gráficos vetoriais | Parcial | [Teste de PNG por página](tests/Smoke.cs). Exportação vetorial pendente. |
| 12.07 | Exportação de áudio | Parcial | |
| 12.08 | Impressão de partitura e partes | Parcial | |
| 12.09 | Definição de páginas, orientação e tamanho | Pendente | |
| 12.10 | Compatibilidade entre versões e formatos de projeto | Parcial | |
| 12.11 | Compartilhamento de arquivos com outros editores | Parcial | |
| 12.12 | Preparação de partituras para editoras e bibliotecas | Pendente | |

## 13 · Ensino, ensaio e performance

| ID | Requisito | Estado | Evidência de aceite |
| --- | --- | --- | --- |
| 13.01 | Materiais para alunos e exercícios de teoria | Pendente | |
| 13.02 | Partituras simplificadas e versões alternativas | Pendente | |
| 13.03 | Partes individuais para ensaios | Parcial | [Extração de instrumento](tests/Smoke.cs). Exportação MusicXML independente. Layout de partes vinculado pendente. |
| 13.04 | Indicações de ensaio e referências de compasso | Pendente | |
| 13.05 | Metrônomo e reprodução em velocidade ajustada | Parcial | Metrônomo com som interno e velocidade da partitura de 50% a 150%. [Testes](tests/Smoke.cs). |
| 13.06 | Loop de trechos para estudo | Pendente | |
| 13.07 | Acompanhamento por instrumentos virtuais | Parcial | |
| 13.08 | Preparação de reduções para piano | Pendente | |
| 13.09 | Transposição de peças para outros instrumentos | Pendente | |
| 13.10 | Criação de partituras para coro e conjuntos escolares | Pendente | |

## 14 · Recursos complementares encontrados em outros editores

| ID | Requisito | Estado | Evidência de aceite |
| --- | --- | --- | --- |
| 14.01 | Compartilhamento de partituras na nuvem e publicação online | Pendente | |
| 14.02 | Comentários e colaboração remota conforme serviço | Pendente | |
| 14.03 | Notação Braille e navegação acessível | Pendente | |
| 14.04 | Plugin scripting e automação de tarefas | Pendente | |
| 14.05 | Processamento em lote de partituras | Pendente | |
| 14.06 | Linguagem textual de notação e composição programática | Pendente | |
| 14.07 | Regras tipográficas configuráveis em código | Pendente | |
| 14.08 | Integração com fluxos Git e versionamento textual | Pendente | |
| 14.09 | Sincronização com vídeo, timecode e hit points | Pendente | |
| 14.10 | Exportação de vídeo de partitura em alguns produtos | Pendente | |
| 14.11 | Bibliotecas de modelos e plugins da comunidade | Pendente | |
| 14.12 | Recursos para tablatura e notação alternativa | Pendente | |
| 14.13 | Suporte a múltiplos sistemas operacionais | Pendente | |

## 15 · Exigências recomendadas para o Vale Score

| ID | Requisito | Estado | Evidência de aceite |
| --- | --- | --- | --- |
| 15.01 | Editor independente e offline, com instalador Windows | Parcial | |
| 15.02 | Inicialização e edição sem baixar bibliotecas sonoras grandes | Parcial | |
| 15.03 | Piano com duas pautas por padrão e suporte a múltiplos pianos | Parcial | |
| 15.04 | Partitura correta para três pianos e violão clássico | Parcial | |
| 15.05 | Motor de notação separado da interface e do motor de áudio | Parcial | |
| 15.06 | Arquivos de projeto versionados, autosave e recuperação | Parcial | [Testes de snapshots e cópia de sessão](tests/Smoke.cs). Validação de interrupção real e backups rotativos pendentes. |
| 15.07 | Exportação MusicXML, MIDI e PDF validada em outros leitores | Parcial | |
| 15.08 | Sintetizador interno leve e VST3 opcional em fase posterior | Parcial | |
| 15.09 | Testes de altura, ritmo, anacruse, vozes e transposição | Parcial | |
| 15.10 | Testes visuais de paginação e prevenção de colisões | Parcial | |
| 15.11 | Acessibilidade, atalhos e interface premium | Parcial | |
| 15.12 | Matriz de recursos implementados, planejados e testados | Parcial | |
| 15.13 | Compatibilidade real com Kontakt/Reason somente após ensaios | Pendente | |
| 15.14 | Controle de desempenho e estabilidade em máquinas modestas | Pendente | |
| 15.15 | Auditoria de licenças e distribuição comercial | Pendente | |
