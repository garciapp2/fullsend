# FULLSEND: plano do tema "entrega de pizza"

Status: **plano + previews para revisão**. Nada disso está implementado ainda.

## A regra deste plano

O tema entra **por cima** do jogo que já existe. Não muda o estilo, não muda a física, não muda os modos.

- Estilo continua o mesmo: HQ (contorno, halftone, cores chapadas) com a UI inclinada, paletas por bioma, fontes Franklin Gothic Heavy Italic + Akzidenz.
- Pizza é desenhada no mesmo estilo: formas chapadas, contorno de tinta, cores da paleta do bioma. Nada de clip-art realista de pizza.
- Personagens novos (clientes) são o **mesmo boneco do jogo com outra skin** (urso polar, pinguim, foca, pato, sapo, panda, sapo laranja), com os chapéus que já existem.
- Textos em português, curtos, com humor seco.

## A premissa (uma frase)

A raposa é entregadora da **Fullsend Pizza**, uma pizzaria no topo da montanha, e desce de trenó levando pizza para clientes espalhados pelos biomas, o mais rápido possível e com a pizza inteira.

"Full send" = ir com tudo. "Send" = entregar. O nome já é o tema.

## O que cada sistema vira

| Hoje | Com o tema | Por que encaixa |
|---|---|---|
| Vida (5 corações) | **Pizza (8 fatias)**. Dano arranca fatias. 0 fatias = entrega fracassada | Torna a punição visível: você vê a pizza se desfazendo |
| Pouso ruim / batida / laser | Arranca fatias (mesmo dano de hoje: 1 fatia = 12,5 de vida) | Nada muda no balanceamento, só a leitura |
| Cura por pouso perfeito (+3) | **Removida**. Pizza não se remonta sozinha | Coerência. A cura vem da loja (ver abaixo) |
| Tempo + nota S..E | **Temperatura**: a linha de ritmo do topo vira uma barra quente→fria. Os ticks S/A/B/C continuam | A nota continua igual, mas agora "S" é "ainda quentinha" |
| Nota no fim da fase | **Gorjeta**: S = gorjeta gorda, E = cliente reclamando | Dá propósito à nota |
| Faíscas (moeda) | **Moedas de gorjeta** (mesmo visual amarelo, nome novo) | Moeda que compra itens já existe |
| Largada | **A pizzaria**: casinha com placa "FULLSEND PIZZA", 3, 2, 1, ENTREGA! | Ponto de partida com cara |
| Portal de chegada | **A casa do cliente**: porta iluminada, cliente esperando, anel de entrega | Fim de fase com cara de entrega |
| 5 fases da Corrida | **5 entregas**. O trenó leva uma pilha de 5 caixas, que diminui a cada entrega | Progresso visível no próprio trenó |
| Tela de resultado | **ENTREGUE!** com o cliente, a frase dele, a gorjeta e os itens | Recompensa com personalidade |
| Tela de morte | **ENTREGA FRACASSADA** / **A PIZZA VIROU PURÊ** / **CAIU NO ABISMO** | Mesmas telas, outro texto |
| "Curar" na loja | **Pizza nova**: repõe as fatias, custa moedas | Cura com sentido |

## Modos (mesma mecânica, novo texto)

- **CORRIDA**: 5 entregas contra o relógio. Gorjeta por nota.
- **FUGA**: o mundo se desfaz atrás de você (o colapso: rachaduras brancas, depois o chão vira violeta escuro). Entregue antes que ele te alcance.
- **INFINITO**: turno sem fim. Entrega atrás de entrega, cada vez mais rápido.

## Perigos na ficção

Os lasers, torres e drones são da **GIGAPIZZA S.A.**, a rede rival que quer fechar a estrada da montanha.

- Isso dá cara aos perigos (logo vermelho na torre e no drone) sem mudar nada da jogabilidade.
- É opcional; os perigos funcionam sem história.

## Clientes por bioma

| Bioma | Clientes (skin + chapéu) | Exemplo de frase (nota S / nota E) |
|---|---|---|
| Neve | Urso polar, pinguim, foca | "Quentinha no meio da nevasca!" / "Chegou um picolé de calabresa." |
| Floresta | Pato, sapo | "Nem amassou a borda!" / "Isso era uma pizza?" |
| Deserto | Sapo laranja | "Ainda tá fumegando!" / "Derreteu e esfriou. Como?" |
| Etéreo | Panda | "Entrega de outro plano." / "Volta pro seu plano." |

## Itens (nomes novos, efeito igual)

| Hoje | Novo nome |
|---|---|
| Botas de Mola | Botas de Mola (fica) |
| Capacete | **Caixa Reforçada** (um tombo por fase não te freia) |
| Coração | **Pizza Família** (+2 fatias máximas e repõe tudo) |
| Adrenalina | **Forno Portátil** (acima de 100 m/s a pizza se recupera) |
| Pouso Mágico | **Amortecedor** (pousos julgados com mais folga) |
| Ímã de Faíscas | **Ímã de Gorjeta** |
| Faíscas em Dobro | **Gorjeta em Dobro** |
| Demais | mantêm o nome |

## UI que muda (e o que não muda)

**Muda:**
- Corações → ícone de pizza em 8 fatias.
- Pace line → barra de temperatura.
- "FAÍSCAS" → "GORJETA".
- Telas de fim de fase e de morte (texto + cliente).
- Uma tag pequena na fase: "ENTREGA 2/5 · FLORESTA".

**Não muda:** layout, fontes, retrato da raposa, velocímetro, boost, combo, callouts de pouso, paletas por bioma, menu pôster (só ganha a caixa de pizza no trenó e uma linha de subtítulo).

## Ordem de implementação (depois da aprovação)

1. Textos e nomes (modos, telas, itens, moeda). Barato, já muda a leitura.
2. HUD: pizza em fatias + barra de temperatura.
3. Caixas de pizza no trenó (pilha que diminui).
4. Chegada: casa do cliente + cliente com skin.
5. Tela ENTREGUE! com cliente, frase e gorjeta.
6. Largada: pizzaria.
7. Gigapizza nos perigos (opcional).

## Previews

Em `Tools/pizza/`:
- `hud.png`: corrida com pizza em fatias, barra de temperatura e caixas no trenó.
- `entregue.png`: tela de fim de fase.
- `menu.png`: menu pôster com o tema.
- `chegada.png`: casa do cliente no fim da fase.
