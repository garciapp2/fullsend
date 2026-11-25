# FULLSEND · GDD

Organizado como o quadro do Milanote: cada `##` é uma coluna, cada `###` é um cartão.

## Visão geral

### FULLSEND
Entrega de pizza a 100 m/s.

Jogo de trenó em 3D, rápido, com visual de história em quadrinhos. Unity 6, Windows.

Página: garciapepe.itch.io/fullsend (rascunho).

### Pitch
Você é a entregadora da Fullsend Pizza. A pizzaria fica no topo da montanha e os clientes moram lá embaixo, cada um num
bioma. O único veículo da firma é um trenó. Você desce com tudo, voa nas rampas, tenta pousar sempre na descida e
chega antes de a pizza esfriar. Se bater, a pizza perde uma fatia.

### Ficha
**Gênero:** corrida de descida (arcade), um jogador, 3D

**Plataforma:** Windows (itch.io)

**Sessão:** uma entrega leva de 1 a 2 minutos; uma Corrida tem 5 entregas, uns 10 minutos

(contorno, meio-tom e cores chapadas)

**Pergunta central:** é divertido descer uma montanha o mais rápido possível sabendo que cada erro custa um pedaço do
que você está entregando?

## História

### Premissa
A Fullsend Pizza fica no pico mais alto da região e entrega para quem mora embaixo, em qualquer bioma, em qualquer
clima. "Full send" é ir com tudo. "Send" é entregar. O nome é o plano de negócios.

A firma tem uma entregadora, uma raposa, e um trenó. A pizza vai numa pilha de caixas amarrada atrás. Cada entrega
tira uma caixa da pilha.

### Tom e regras de escrita
Seco e rápido, como o jogo. Ninguém faz discurso.

- Frases curtas, em maiúsculas na interface. Cabem numa tag.
- Piada pela situação: o cliente reclama da pizza fria do jeito que reclamaria de verdade.
- Os perigos são tratados como trânsito ruim, não como guerra.
- Nada de "jornada", "lenda" ou "destino". É uma entrega.
- A raposa não fala. Quem fala são os clientes, uma frase na porta de casa.

### O turno
Uma Corrida é um turno de 5 entregas. Cada entrega começa mais alto e termina na casa de um cliente, num bioma
diferente: neve, floresta, deserto e o Etéreo, um lugar que ninguém sabe explicar e que mesmo assim pede pizza.

A última entrega do turno é a pior: começa a chover meteoro.

### A Gigapizza (anotado, não implementado)
Os lasers, drones e torres na estrada seriam da Gigapizza S.A., a rede rival que quer fechar a estrada da montanha. Dá
cara aos perigos sem mudar a jogabilidade: um logo vermelho na torre e no drone.

## Personagens

### A raposa (você)
Entregadora da Fullsend Pizza. Cara séria, trenó de madeira com a pilha de caixas atrás. Desce sentada; a pé é
lenta e meio perdida.

### Os clientes
Um por bioma, esperando na porta de casa. Acenam quando você está chegando e reagem à nota na tela de entrega
(dançam no S, ficam tristes no E).

- **Sr. Pinguim:** Neve
- **Dona Pata:** Floresta
- **Sr. Sapo:** Deserto
- **Dona Panda:** Etéreo

### As falas
Uma frase por nota, dita na porta:

- S: "NEM ESFRIOU! VALEU!"
- A: "TÁ QUENTE AINDA. BOA!"
- B: "MORNA, MAS TÁ VALENDO."
- C: "ESFRIOU UM POUCO, HEIN."
- D: "TÁ FRIA. VOU PAGAR MENOS."
- E: "ISSO É PICOLÉ DE PIZZA?"
- Com 3 fatias ou menos, a nota não importa: "CADÊ O RESTO DA MINHA PIZZA?"

## Mecânicas

### Loop da entrega
Contagem 3, 2, 1. Desce a montanha (5 a 8 km). Pega gorjeta no caminho, desvia dos perigos, pousa bem. Chega na casa do
cliente. Tela ENTREGUE! com a nota e a frase do cliente. Escolhe 1 item grátis. Próxima entrega.

### Movimento
O trenó desce sozinho. O mouse curva; não precisa segurar W. Velocidade vem da inclinação: descida
acelera, subida freia. Abaixo de 50 m/s parado no chão, a raposa cai do trenó.

No ar, Espaço é descida rápida: o trenó mergulha e volta ao chão mais cedo.

### Pousos
Todo pouso recebe uma nota pela velocidade que sobrou depois do impacto:

- **Perfeito:** pousou acompanhando a descida. Ganha velocidade e enche a energia do turbo.
- **Bom:** perde pouco.
- **Ok:** perde velocidade.
- **Ruim:** perde velocidade e 3 fatias × a intensidade da entrega.

O jogo inteiro gira em torno disso: quem pousa bem vai rápido.

### Turbo
Shift. Gasta a energia, que só vem de pousos bons e perfeitos. Dura pelo menos 0,65 s, puxa até 100 m/s e depois
acelera mais. Sem energia, não tem turbo.

### A pé
Caiu do trenó (bateu de frente, parou numa subida): você está a pé. Segure Shift parado por 1 s para pular de volta no
trenó e sair a 90 m/s. Funciona em qualquer chão, até numa borda inclinada.

### Manobras e combo
No ar: W/S dá mortal, A/D gira, Q/E e os botões do mouse agarram o trenó. Pousar com a manobra completa vale pontos e
abre um combo de 2,4 s; cada manobra emendada sobe o multiplicador. Pousar torto no meio da manobra é tombo: perde o
combo e 2 fatias × a intensidade.

### Trilhos e anéis
Encostou num trilho, desliza nele sozinho. Espaço pula fora. Anéis no caminho dão impulso para a frente.

## A pizza

### 8 fatias
A pizza é a vida: 80 pontos, 10 por fatia. A pizza no canto da tela mostra as fatias; cada uma que se perde sai voando.
0 fatias é entrega fracassada.

Pizza não se remonta sozinha. A cura vem da loja ("pizza nova") ou do Forno Portátil.

### O que custa fatia
- **Bater de raspão** num poste, pedra ou parede: 1,5 fatia, e você desliza.
- **Bater de frente:** 8,5 fatias, mais que a pizza inteira. Só sobrevive quem tem Pizza Família; aí quica para trás e
 cai do trenó.
- **Pouso ruim:** 3 fatias × intensidade.
- **Perigos:** cada um tem o seu dano (ver Perigos).
- **Tombo** no meio de uma manobra: 2 fatias × intensidade.

A intensidade sobe de 0,7 na primeira entrega para 1,3 na quinta.

### A temperatura
A linha no topo da tela é a temperatura da pizza. Cada entrega tem um tempo máximo (comprimento ÷ 80 m/s, um pouco
menos a cada entrega). Ir mais rápido que o ritmo mantém a pizza QUENTINHA; ficar para trás vai esfriando: MORNA, FRIA,
GELADA.

Passou do tempo máximo + 5 s: "A PIZZA ESFRIOU". Fim do turno.

### Nota e gorjeta
A nota sai do tempo.: S abaixo de 85% do tempo máximo, E perto do limite, com A, B, C e D entre eles. A
gorjeta da entrega é o que você pegou no caminho, mais um bônus pela nota (S paga mais).

## Modos

### Corrida
5 entregas contra o relógio; os biomas se revezam, então um deles se repete. Nota de S a E em cada uma e uma tela de fim de turno com todas.

### Fuga
O mundo desaba atrás de você: é o colapso. Rachaduras pretas e brancas avançam pelo chão, o terreno vira
violeta chapado e, atrás da linha, a montanha tomba e cai no vazio. A tela fica roxa nas bordas quando ele chega perto.
Encostou, machuca; foi engolido, acabou ("O MUNDO TE ENGOLIU").

O colapso segue o ritmo da entrega, 600 m atrás. Quem para, perde.

### Infinito
Entrega atrás de entrega, sem fim. Cada uma um pouco mais rápida (+6% de ritmo por entrega) e mais cheia de perigo. A
partir da sexta, chove meteoro.

## Biomas

### Neve
Branco, azul e pontos laranja. Cliente: Sr. Pinguim.

Perigos próprios: tempestade de gelo (espinhos que caem do céu) e gêiseres.

### Floresta
Verde, céu azul, pedras e árvores. Cliente: Dona Pata.

Só os perigos comuns: é a entrega para aprender.

### Deserto
Areia laranja, céu lilás, pedras vermelhas. Cliente: Sr. Sapo.

Perigos próprios: espinhos que sobem do chão, buraco de mineração (cospe pedras) e laser de mineração.

### Etéreo
Roxo, ciano e rosa, cogumelos gigantes e pedras que parecem mãos. Cliente: Dona Panda.

Os perigos comuns, num lugar onde nada parece chão.

### Como uma entrega é feita
A pista é gerada a cada entrega a partir de peças: descida livre, rampa, penhasco, abismo, halfpipe, trilhos, curva
inclinada, anéis e a chegada. Por cima, morros de ruído, o cenário do bioma (árvores, pedras, ruínas) e os perigos, que
seguem o posicionamento numa estrada de 100 m de largura.

Duas entregas no mesmo bioma nunca são iguais.

## Perigos

### Regra
Todo perigo avisa antes: cúpula ou coluna translúcida onde vai cair, anel no chão, linha de mira. Os perigos são os do
com o mesmo tamanho, a mesma hitbox e o mesmo tempo. Cada entrega tem um orçamento de perigos que sobe de 10 para
30.

### Lasers
- **Drone de laser:** sozinho ou em grupo (linha, quadrado, X), varre a pista.
- **Postes com laser:** pares de postes ligados por um feixe na altura do trenó.
- **Orbes de grade:** três tamanhos, giram uma grade de lasers.
- **Laser giratório e anéis de laser:** passar no meio, na hora certa.

### Explosivos
- **Bomba:** cúpula laranja no chão. Ficou 0,35 s dentro, explode. Tem a versão que anda.
- **Torre de mísseis:** mira e dispara mísseis que seguem você. A versão "super" aparece da quarta entrega em diante.
- **Barragem:** uma chuva de projéteis numa faixa da pista.

### Do céu e do chão
- **Cápsula:** cai do céu com aviso e abre.
- **Muro:** sobe do chão na sua frente.
- **Meteoro:** só na última entrega da Corrida e no Infinito a partir da sexta. Cai a cada 14 s com uma onda de choque.

### Os de bioma
- **Deserto:** espinhos móveis, buraco de mineração, laser de mineração.
- **Neve:** tempestade de gelo, gêiseres.

### O colapso (Fuga)
Não é um perigo da pista, é a pista inteira acabando. Ver Modos.

## Loja e itens

### Como funciona
Depois de cada entrega: 3 cartões, 1 grátis. Com a gorjeta dá para trocar os 3 (30) ou comprar uma pizza nova, que
repõe todas as fatias (40). Os itens acumulam ao longo do turno.

Raridades: comum, incomum, raro e lendário. Cada cartão tem o ícone do item, a raridade na cor e o que ele faz.

### Boost e energia
- **Turbina:** turbo acelera 30% mais.
- **Bateria Extra:** turbo dura 30% mais.
- **Marmita Térmica:** pousos dão 35% mais energia.

### Pousos e velocidade
- **Amortecedor:** pousos julgados com mais folga.
- **Pouso Explosivo** (raro): pouso perfeito dá +12 de velocidade.
- **Embalo** (raro): cada manobra acertada dá +4 de velocidade.
- **Trilho Turbo** (raro): o trilho acelera em vez de frear.
- **Gravitron:** descida rápida 50% mais forte.
- **Anel Fantasma** (lendário): pouso perfeito cria um anel de impulso na sua frente.

### Trenó e manobras
- **Prancha Leve:** curva 20% melhor no chão e no ar.
- **Giro Rápido:** mortais e giros 25% mais rápidos.
- **Combo Longo:** +1 s para emendar manobras.

### Pizza e gorjeta
- **Pizza Família:** pizza maior, aguenta 25% mais.
- **Caixa Reforçada:** um tombo por entrega não te freia.
- **Forno Portátil** (raro): acima de 100 m/s a pizza se recupera.
- **Ímã de Gorjeta:** puxa gorjeta de longe.
- **Gorjeta em Dobro:** cada gorjeta vale 2.

## Level design, feedback e UI

### Level design
A pista é uma descida contínua; não tem sala nem checkpoint. As peças ensinam por repetição: rampa para aprender a
pousar, penhasco para aprender a descida rápida, abismo para levar a sério a velocidade.

A primeira entrega da primeira vez é o tutorial (ver UI). A dificuldade sobe por entrega: ritmo, intensidade do dano e
quantidade de perigo.

### Feedback
Tudo o que importa aparece no mundo antes do HUD.

**Pouso:** a palavra grande na tela (PERFEITO!), o trenó amassa, a câmera treme, o campo de visão abre.

**Perigo:** aviso translúcido antes, flash e tremor quando acerta.

**Dano:** a fatia sai voando da pizza, a imagem se parte em duas cores.

**Velocidade:** o campo de visão abre até 118°, linhas de velocidade, a câmera chega mais perto.

**Colapso:** borda roxa na tela e aberração cromática quando ele chega perto.

### UI e menus
HUD: pizza em fatias, retrato da raposa, temperatura e nota ao vivo no topo, velocímetro e barra de turbo embaixo.

Menu: pôster com o logo, a raposa no trenó e os 3 modos. Cada modo tem um enquadramento de câmera próprio.

Carregando: a pizza se monta fatia por fatia com o progresso real da geração da pista, e uma dica.

Entregue!: nota grande, retrato do cliente com a frase dele, tempo, fatias, gorjeta, pontos e a loja.

Esc abre pausa ou opções em qualquer tela. Tudo que tem tecla também é clicável.

Tutorial: na primeira entrega, as lições aparecem na hora certa com câmera lenta (mouse, Espaço no ar, pouso, turbo,
manobras). Sem colapso e sem limite de tempo nessa entrega. Dá para repetir no menu (T).

## Som e arte

### Música
Uma trilha por bioma em três partes; a parte muda a cada terço da entrega. Na Fuga, o som de perigo cresce
conforme o colapso se aproxima.

### Efeitos sonoros
Pousos (um som por nota), contagem, gorjeta, tombo, batida, turbo, avisos dos perigos e a vitória na chegada.

### Direção de arte: HQ
Contorno de tinta, meio-tom e cores chapadas. Uma paleta por bioma, e a interface troca de cor junto.
A interface é inclinada como a, com as fontes Franklin Gothic Heavy Italic e Akzidenz.

Tudo em 3D com shader toon. Nada de foto realista, nada de cor fora da paleta.

### Assets de terceiros (provisório)
o protótipo; para publicar, precisam ser trocados por próprios ou licenciados.

## Rejogabilidade, balanceamento e estados

### Rejogabilidade
Pista, cenário e perigos gerados a cada entrega. O bioma da primeira entrega é sorteado e os outros vêm em sequência. 17 itens que mudam o jeito de
jogar (mais turbo, mais curva, mais pouso). Três modos, nota por entrega.

### Balanceamento
Base: a dificuldade. Vida 80, energia só de pouso, inclinação 0,05.

Ritmo: 80 m/s na primeira entrega, +3% por entrega na Corrida e +6% no Infinito.

Colapso: 600 m atrás do ritmo (original era 280; ficou difícil demais).

Meta (a testar): um jogador novo fecha a Corrida em B ou C nas primeiras tentativas; S exige pousar bem quase sempre.

Nenhum item pode ser obrigatório.

### Estados e erros
Pausa a qualquer momento (Esc); a contagem também pausa.

Fim do turno: entrega fracassada (0 fatias), "A PIZZA ESFRIOU" (passou do tempo), "CAIU NO ABISMO", "O MUNDO TE
ENGOLIU" (Fuga). R recomeça, M volta ao menu.

Caiu do trenó: aviso na tela "SEGURE SHIFT" com a barra de carga.

O progresso não é salvo entre sessões; só o tutorial fica marcado como feito.

## Escopo

### Entrou
Movimento, pousos e turbo. 4 biomas com cliente e casa. 24 obstáculos. Corrida, Fuga e Infinito. Loja com
17 itens. Colapso. Tutorial. Interface completa com tela de carregamento. Build para Windows.

### Ficou de fora
- A pizzaria no topo como largada (feita e desligada).
- A Gigapizza como dona dos perigos.
- Falas dos clientes por bioma (hoje são por nota).
- Ajuste de tempo e nota por bioma depois dos morros novos.
- Trocar os assets de terceiros para poder publicar.
