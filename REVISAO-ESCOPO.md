# Revisão de escopo — Vale Score 0.20.0

A matriz obrigatória contém 194 requisitos. Nesta entrega, 73 estão parciais e 121 pendentes. A classificação mede o aceite completo de cada requisito composto. A suíte automatizada passou em 187 verificações. O aceite comercial continua pendente.

## Entrega 0.20.0

Mixer com volume de 0 a 100%, silenciar e solo por instrumento. Os ajustes são salvos no projeto, participam de desfazer/refazer e são aplicados ao som interno, WAV e MIDI exportado. Aplicar interrompe a reprodução atual quando há mudanças. A reprodução seguinte usa o novo estado. Silenciar tem prioridade sobre solo. Vários instrumentos podem ficar em solo juntos.

Paleta visual para as cinco articulações existentes. O usuário seleciona uma nota e clica no símbolo. A barra permite rolagem horizontal quando os grupos excedem o espaço disponível.

O mixer usa volume geral por instrumento. Panorâmica, efeitos, automação e controles individuais por voz continuam pendentes. O arquivo MIDI externo aberto para audição usa seus próprios eventos.

## Lacunas por área

| Área | Trabalho restante |
| --- | --- |
| Projetos | Movimentos, partes vinculadas, layouts persistentes, modelos personalizados, alternância de instrumentos e múltiplos projetos ativos. |
| Entrada e edição | Gravação MIDI em tempo real, inserção com deslocamento, seleção de trechos, filtros, edição em massa, transformações, distribuição e expansão de vozes, atalhos configuráveis. |
| Ritmo | Anacruse, compassos incompletos, quiálteras, métricas irregulares e variáveis, agrupamento manual, pausas multicompasso, polimetria e notação de repetição. |
| Alturas | Claves adicionais, instrumentos transpositores, alterações de clave e tonalidade durante a peça, microtonalidade, ossias e cabeças alternativas. |
| Expressão | Fermatas, respirações, cesuras, ornamentos, glissandos, tremolos, crescendo e diminuendo, tempo variável, pedais, ajuste manual de ligaduras e ligaduras entre pautas. |
| Harmonia | Diagramas de acordes, baixo cifrado, múltiplas linhas de cifras, swing, articulações de jazz, acompanhamento automático e repetições com casas e saltos. |
| Instrumentos | Tablatura, afinações alternativas, técnicas de cordas, dedilhados, notação e mapeamento de percussão, divisi, harpa e cues. |
| Voz e texto | Múltiplas estrofes, extensores e melismas completos, SATB, textos de sistema e pauta, marcas de ensaio, créditos ampliados e estilos de texto. |
| Editoração | Prevenção completa de colisões, espaçamento e posicionamento manuais, quebras configuráveis, páginas-mestre, pautas vazias, condensação, cutaway e fontes SMuFL. |
| Revisão | Avisos musicais e de ambiguidades, filtros, comentários, comparação de versões e regras de notação configuráveis. |
| Reprodução | Pan, VST3, bibliotecas sonoras, mapas de expressão, piano roll, edição de velocidade, automação MIDI, tempo gradual, repetições, swing, humanização e efeitos. |
| Exportação | PDF vetorial direto, SVG, impressão em drivers reais, validação em leitores externos, publicação editorial e formatos de áudio adicionais. |
| Ensino e ensaio | Exercícios, versões simplificadas, referências de ensaio, reduções para piano e ferramentas específicas para coro e conjuntos escolares. |
| Complementos | Nuvem, colaboração, Braille, scripting, processamento em lote, notação textual, vídeo e outros sistemas operacionais. |
| Produto final | Instalador, auditoria de acessibilidade, recuperação sob falha real, desempenho em máquinas modestas, testes de dispositivos MIDI, Kontakt/Reason e auditoria de licenças. |

## Dependências de evolução

A grade rítmica atual precisa ser ampliada antes de oferecer quiálteras e valores muito curtos com precisão. A representação temporal precisa incorporar mudanças por posição antes de oferecer métrica e andamento variáveis. A instrumentação precisa representar claves, transposição e técnicas antes de ampliar o catálogo orquestral.

O acabamento editorial depende de posicionamento de objetos e regras de colisão que atendam acordes, vozes, texto e símbolos simultaneamente. O aceite de interoperabilidade exige abrir arquivos produzidos pelo Vale Score em leitores externos identificados e conferir os resultados.

Todos os itens continuam obrigatórios para o produto final. A evidência detalhada e os identificadores originais permanecem em REQUISITOS-FINAIS.md.