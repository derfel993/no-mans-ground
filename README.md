# No Man's Ground

Prototipo Unity 6 di un gestionale 2D sulle trincee della prima guerra mondiale,
con visuale dall'alto. Il progetto Unity si trova in `No Man's Ground`.

## Step 1: mappa e navigazione

Aprire il progetto con Unity **6000.6.6f1**, quindi aprire
`Assets/Scenes/MapPrototype.unity` e premere **Play**.
La scena originale `SampleScene` non viene modificata.

La mappa iniziale misura 128 x 128 celle sul piano XY; ogni cella occupa
1 unita Unity. Terra, erba e fango sono rappresentati da tile provvisori.
Il terreno viene generato all'avvio, non salvato come migliaia di tile nella scena.

### Comandi

| Input | Azione |
| --- | --- |
| WASD / frecce | Sposta la camera |
| Tasto centrale + trascinamento | Trascina la visuale |
| Rotella | Zoom sul punto sotto il cursore |
| Clic sinistro su un soldato | Seleziona e mostra la scheda |
| Clic sinistro sul terreno | Deseleziona |
| Clic destro sul terreno | Ordina di raggiungere la cella al soldato disponibile piu vicino |
| T / pulsante Scava | Attiva/disattiva la designazione degli scavi |
| L / pulsante Installa scala | Clic su una trincea completata per ordinare l'installazione di una scala |
| Sinistro + trascinamento in modalita Scava | Designa una linea di celle; un clic designa una cella |
| X / pulsante Annulla scavi | Attiva/disattiva la cancellazione lungo una linea |
| Esc / destro in modalita Scava o Annulla | Esce dalla modalita, senza impartire movimento |
| Maiusc + clic destro sul terreno | Aggiunge/rimuove un ostacolo debug, a soldati fermi e senza scavi in coda |
| Rotella sul pannello dei soldati | Nessuno zoom sulla mappa |

In Editor, dare il focus alla finestra Game per usare i comandi.
La camera resta entro la mappa anche cambiando proporzioni della finestra.
Vicino ai bordi, il limite della mappa ha precedenza sul punto di zoom.
Per mappe molto piccole il limite minimo di zoom viene ridotto automaticamente,
in modo da non mostrare spazio esterno.

### Parametri nell'Inspector

- Oggetto **Map**, componente **Map Prototype**: dimensioni (1-512 per asse),
  seed, scala del rumore e soglie del terreno. La soglia del fango deve essere
  inferiore a quella dell'erba. Una scala del rumore maggiore produce zone piu ampie.
- Oggetto **Ground**: riferimenti ai tile del terreno, della trincea e della scala.
- Oggetto **Main Camera**, componente **Map Camera Controller**: velocita di
  movimento, sensibilita dello zoom e limiti della dimensione ortografica.
- Per rigenerare senza riavviare, usare **Regenerate Map** nel menu contestuale
  del componente **Map Prototype**. Le modifiche fatte durante Play non sono persistenti.

Stesso seed e stessi parametri producono le stesse celle nella versione corrente
di Unity; non viene modificato lo stato casuale globale del gioco.
La camera deve restare ortografica, senza rotazione, rivolta verso il piano XY.

Il menu **No Man's Ground > Create Map Prototype Scene** ricrea la scena
configurata, chiedendo conferma prima di sovrascriverla. I tile esistenti vengono
preservati, quindi e possibile sostituire la grafica provvisoria.
La scena non viene inserita automaticamente nelle Build Profiles.

### Organizzazione

- `Assets/Scripts/Map/MapData.cs`: dati del terreno, indipendenti dalla Tilemap.
- `Assets/Scripts/Map/MapGenerator.cs`: generazione con seed e rumore Perlin.
- `Assets/Scripts/Map/MapTilemapView.cs`: disegno e limiti della mappa.
- `Assets/Scripts/Map/MapCameraController.cs`: input tramite Input System.
- `Assets/Scripts/Map/MapPrototype.cs`: collegamento tra generazione, vista e camera.
- `Assets/Editor/MapPrototypeSetup.cs`: creazione della scena e degli asset provvisori.

### Test

Aprire **Window > General > Test Runner**, selezionare **EditMode** ed eseguire
`NoMansGround.Tests` (incluse le prove input con il fixture ufficiale Input System).
I test verificano riproducibilita, variazione del seed, validazione dei parametri,
tile e riferimenti della scena, panning con mouse, punto di zoom e limiti della
camera su finestre orizzontali, verticali e mappe piccole.
Il manifest abilita i test del pacchetto Input System per rendere disponibile
il suo fixture ufficiale; selezionare il namespace del progetto per eseguire
soltanto i test di No Man's Ground.

## Step 2: soldati, stato e skill

La stessa scena include **8 soldati italiani**, visibili con sprite provvisori.
Il primo viene selezionato all'avvio; il contorno giallo indica la selezione.
La scheda sulla destra mostra nome, cognome, stato e tutte le skill senza scroll.
La larghezza e dimezzata (167 unita UI anziche 334), con caratteri e spaziature
compatti. Se l'altezza della finestra non basta, il contenuto viene ridimensionato
per rimanere completamente visibile.
Selezionare un altro soldato con clic sinistro o il terreno per deselezionare.
Zoom e panning restano disponibili; i clic sul pannello non selezionano soldati
dietro l'interfaccia.

### Anagrafica e stato

Nome e cognome sono generati da un repertorio italiano, senza duplicati nella
stessa squadra. Il seed dei soldati e indipendente da quello del terreno.
I dati del soldato sono separati dal suo sprite e dal pannello.

Gli stati sono **statici** in questo step: non cambiano con il tempo e non
producono ancora effetti sulle attivita.
Alla nascita fame, sete e bisogni fisiologici sono sempre **100/100**;
secondo la scala attuale questo indica il massimo bisogno, non la soddisfazione.

| Indicatore | Significato |
| --- | --- |
| Fame | 0 sazio, 100 fame critica |
| Sete | 0 dissetato, 100 sete critica |
| Bisogni fisiologici | 0 nessuna urgenza, 100 urgenza critica |
| Temperatura percepita | -100 freddo estremo, 0 neutra, +100 caldo estremo |
| Stress | 0 rilassato, 100 stress massimo |
| Paura | 0 nessuna paura, 100 paura massima |
| Stanchezza | 0 riposato, 100 esausto |

La temperatura e uno stato relativo, non una misura in gradi Celsius.
La barra termica indica l'intensita del disagio, blu per freddo e arancione per caldo.
Il coraggio e una skill distinta dalla paura attuale.

### Skill ed esperienza

**Mira, Coraggio, Forza, Resistenza e Velocita** hanno progressione indipendente:

- Livelli da **1 a 20**; livello iniziale generato tra 1 e 10, senza XP pregressi.
- Valore caratteristico da 5 a 100, pari a `livello * 5`.
- Per passare al livello successivo servono `100 * livello corrente` XP.
- Gli XP residui si conservano e una singola assegnazione puo superare piu livelli.
- Al livello 20 non si accumulano ulteriori XP.

Le attivita assegnano esperienza con
`soldier.GetSkill(SoldierSkill.Aim).AddExperience(amount)`.
Velocita influenza lo spostamento; Forza influenza lo scavo e cresce
completando celle di trincea. Le altre skill non modificano ancora le attivita.
Per provare la progressione in Play, selezionare un soldato e usare
**Debug/Add 100 Aim XP to Selected Soldier** nel menu contestuale del componente
**Soldier Details Panel**, sull'oggetto **Soldier Selection**.
Il pannello aggiorna immediatamente XP e livello.

### Configurazione e limiti

- Oggetto **Soldiers**, componente **Soldier Roster**: numero di soldati (1-64),
  seed e sprite. Cambiare questi parametri prima di Play oppure rigenerare la mappa.
- Le posizioni iniziali sono celle distinte centrate orizzontalmente vicino al
  bordo inferiore, la zona d'arrivo dei soldati. La camera iniziale inquadra
  questa zona rispettando i limiti della mappa. Tutti i terreni
  attuali sono utilizzabili, salvo ostacoli di debug. Non vengono valutate coperture.
- Se la mappa ha meno celle dei soldati richiesti, il numero viene ridotto alla
  capienza e viene emesso un avviso in Console.
- **Regenerate Map** ricrea anche i soldati: anagrafica e stato iniziali sono
  riproducibili, ma gli XP accumulati vengono azzerati.
- Non e ancora previsto il salvataggio delle partite: dati e XP durano per la
  sessione Play corrente.

La scena fornita e gia aggiornata. Il comando
**No Man's Ground > Add Soldiers to Map Prototype Scene** aggiunge o ricollega
i componenti alla scena esistente, senza ricreare mappa o camera.
Il comando **Create Map Prototype Scene** continua a creare una scena completa,
ora inclusiva dei soldati.

Implementazione in `Assets/Scripts/Soldiers`: `SoldierData` e `SkillProgress`
gestiscono i dati; `SoldierGenerator` crea la squadra; `SoldierRoster` genera
gli oggetti sulla mappa; `SoldierView` visualizza la selezione;
`SoldierDetailsPanel` gestisce clic e scheda. Canvas e input UI vengono creati
all'avvio, riutilizzando un EventSystem esistente se presente.

I test `SoldierDataTests` verificano generazione, limiti degli stati, soglie XP,
passaggi multipli di livello e livello massimo. `SoldierSceneTests` entra in Play
per verificare spawn, selezione, aggiornamento UI e rigenerazione anche su mappa minima.
`SoldierInputTests` verifica selezione, ordini condivisi e ostacoli tramite clic simulati
con il fixture ufficiale Input System.

## Step 3: movimento e pathfinding

Con **clic destro** su una cella viene assegnato un ordine al soldato disponibile
con il percorso raggiungibile piu breve, indipendentemente da quello selezionato.
Un soldato in marcia o assegnato a uno scavo e occupato e non riceve altri
ordini manuali. Se tutti sono occupati, il comando di movimento viene rifiutato
con un messaggio: il movimento manuale non ha una coda, mentre gli scavi si.

### Percorsi e movimento

- Otto direzioni; una diagonale e consentita solo se entrambe le celle laterali
  sono percorribili, evitando di tagliare gli angoli degli ostacoli.
- Ricerca Dijkstra inversa dalla destinazione: una sola ricerca confronta i
  soldati liberi. Costo di griglia 10 per passo ortogonale e 14 per diagonale.
  Non viene scelta la distanza in linea retta ne il tempo di arrivo.
- Movimento continuo tra i centri delle celle, indipendente dal frame rate.
  La skill Velocita determina la velocita come `1.5 + 3 * valore / 100`
  unita Unity al secondo; le diagonali non aumentano la velocita fisica.
- I soldati possono attraversarsi e attraversare le posizioni di altri soldati.
  Le destinazioni, invece, sono esclusive: una cella finale occupata da un
  soldato fermo o prenotata da uno in marcia non puo ricevere altri ordini.
- Quando un soldato parte, la sua vecchia cella diventa disponibile come
  destinazione; all'arrivo resta riservata la nuova posizione.
- La scheda mostra disponibilita, destinazione e velocita del soldato selezionato.
  Il risultato dell'ultimo comando resta visibile anche senza selezione.
- Una linea gialla mostra il percorso residuo dell'ultimo ordine accettato,
  con un indicatore sulla destinazione. Gli altri ordini proseguono comunque.
  La linea scompare all'arrivo e non cambia la selezione del soldato ispezionato.

### Ostacoli di prova

**Maiusc + clic destro** aggiunge/rimuove un blocco sulla cella, colorata di scuro.
E un comando di debug per verificare deviazioni e destinazioni irraggiungibili,
non un ordine di costruzione. Non si puo bloccare una cella occupata.
Per non modificare percorsi in corso, gli ostacoli si possono cambiare solo
quando tutti i soldati sono fermi e non ci sono scavi in coda. Il ricalcolo per ostacoli dinamici verra
introdotto con le attivita che modificano la mappa.

I clic sul pannello non impartiscono ordini e non modificano gli ostacoli.
Destinazioni invalide, prenotate, irraggiungibili o senza soldati disponibili
producono un messaggio nella scheda e un avviso in Console.
**Regenerate Map** annulla gli spostamenti e azzera gli ostacoli di debug.
La scena include gia i componenti; il comando **Add Soldiers to Map Prototype Scene**
aggiorna anche il collegamento degli ordini alle scene esistenti.

`MapData` conserva la percorribilita separata dal terreno e `GridPathfinder`
calcola i percorsi. `SoldierMovementOrders` gestisce input e assegnazione;
`SoldierView` mantiene percorso, stato e spostamento.
I test verificano costi minimi contro una ricerca di riferimento, deviazioni,
angoli, destinazioni esclusive, disponibilita, avanzamento per frame e input reali.

## Step 4: scavo delle trincee

La scena fornita include gia **Trench Work Orders**, collegato a mappa, soldati
e pannello. Dopo un aggiornamento dei file con Unity aperto: fermare Play,
**riaprire la scena MapPrototype dal Project**, quindi avviare Play. Non salvare
prima una vecchia copia della scena ancora aperta in memoria sopra quella aggiornata.
Per aggiornare altre copie usare **No Man's Ground > Update Map Prototype
(Soldiers, Movement, Trenches)**: preserva i parametri esistenti di mappa e camera.

### Designazione e cancellazione

- Premere **T** o **Scava**. Il trascinamento sinistro mostra un'anteprima azzurra;
  al rilascio designa una linea tra partenza e arrivo, non un rettangolo.
  Le linee diagonali sono collegate a otto direzioni. Un clic designa una sola cella.
- Le celle gialle sono lavori pendenti; il colore tende al verde durante lo scavo.
  Duplicati, celle gia scavate, bloccate o fuori mappa non generano altri lavori.
  Il rilascio fuori mappa o sopra la UI scarta il trascinamento.
- **X** o **Annulla scavi** attiva una modalita analoga, con anteprima rossa,
  che rimuove i lavori di scavo o installazione scala lungo la linea.
  Le trincee e le scale completate non vengono rimosse.
- **Esc** o **destro** esce dalla modalita; quel clic non genera un ordine di marcia.
  In queste modalita il sinistro non cambia il soldato selezionato.
- Annullare perde il progresso di quella cella e libera l'assegnazione senza XP.
  Un soldato gia in marcia raggiunge la posizione prenotata prima di tornare
  disponibile: nessun teletrasporto o interruzione a meta cella.

### Assegnazione ed esecuzione

- La coda usa l'ordine di designazione. Per ciascun lavoro viene scelto il soldato
  libero con il percorso piu breve verso una delle **quattro celle adiacenti
  ortogonali**, mai la cella da scavare. I lavori irraggiungibili non impediscono
  l'assegnazione di quelli successivi.
- Lavoro e posizione finale sono esclusivi. Un soldato non riceve due lavori,
  ne un ordine manuale durante lo scavo. Gli attraversamenti restano consentiti.
- Il tempo avanza soltanto dopo l'arrivo. Durata per cella:
  `6 / (1 + Forza / 100)` secondi; al valore massimo sono 3 secondi.
  Ogni cella completata assegna **25 XP Forza** (con normale avanzamento di livello).
  Tempo base e premio sono configurabili sul componente **Trench Work Orders**.
- Quando un soldato si libera, prende altro lavoro automaticamente. I lavori
  pendenti vengono rivalutati ogni 0,5 secondi; il pannello distingue scavi attivi
  e accessi bloccati/occupati. La scheda del soldato mostra la percentuale di scavo.
- Una cella con uno scavo pendente non viene scelta come posizione di lavoro
  per un altro ordine. Se la cella da scavare e occupata da un soldato o serve
  ancora a un percorso gia assegnato (incluse le celle laterali delle diagonali),
  il completamento attende e viene indicato come bloccato: nessun soldato cade
  nella nuova trincea o attraversa una parete. Se un soldato resta fermo sulla
  cella designata, impartire un ordine per liberarla.
- Soldati disabilitati rilasciano il lavoro, conservandone il progresso per un
  sostituto. Disabilitare il sistema scavi o rigenerare la mappa annulla la coda,
  rimuove gli indicatori e libera le assegnazioni.
- Il terreno completato diventa un tile marrone scuro **Trench**, percorribile
  all'interno ma accessibile dal terreno solo attraverso una **scala**.
  Non conferisce ancora copertura o bonus di combattimento; nessun attrezzo,
  materiale o consumo dei bisogni e richiesto.
- Gli scavi, come le altre modifiche runtime, durano solo nella sessione Play.
  La rigenerazione ricrea il terreno originale e azzera anche trincee e XP.

`TrenchWorkOrders` gestisce designazione, coda, accessi, prenotazioni e progresso;
`SoldierView` espone occupazione e avanzamento; `MapTilemapView` applica il tile
completato. I test verificano durata e XP, accessi adiacenti, prenotazioni,
coda automatica, cancellazione, ritentativi, input T/X e trascinamento,
pulsanti runtime, rigenerazione e compatibilita del pannello compatto.

### Scale e accesso alle trincee

Premere **L** o **Installa scala**, quindi cliccare su una cella di trincea
gia completata. Il clic aggiunge un lavoro alla stessa coda degli scavi, non
crea subito una scala. **Esc/destro** esce dalla modalita e **X** annulla anche
le installazioni non terminate. Celle di terreno normale, bloccate, con una
scala gia presente o con un lavoro gia in coda vengono rifiutate con un messaggio.

Un soldato libero raggiunge una delle quattro celle adiacenti e installa
la scala senza dover entrare prima nella trincea. Puo lavorare dal terreno
o da un tratto di trincea gia raggiungibile. Non vengono richiesti materiali.
Durata: `4 / (1 + Forza / 100)` secondi, escluso il viaggio;
premio al completamento: **25 XP Forza**, come per lo scavo.
**Base Ladder Seconds** sul componente **Trench Work Orders** modifica il tempo base.
Il pannello del soldato mostra **Scala: percentuale**; l'indicatore azzurro
diventa un tile con pioli visibili solo a installazione conclusa.

Regole applicate sia agli ordini di movimento sia ai percorsi dei lavori:

- L'ingresso e l'uscita tra terreno e trincea sono permessi soltanto attraverso
  una cella di trincea con scala, con un passo **ortogonale**.
- Una scala rende raggiungibile tutto il tratto di trincea collegato, non solo
  la cella che la contiene; non abilita accessi dalle altre celle del bordo.
- All'interno resta possibile camminare tra celle di trincea senza scala.
  Le diagonali sono consentite solo sullo stesso livello, con entrambe le
  celle laterali percorribili e sullo stesso livello: nessun taglio delle pareti.
  Due tratti che si toccano solo su un angolo non sono collegati.
- Una trincea senza accessi raggiungibili non e una scorciatoia per attraversare
  la mappa. Gli ordini irraggiungibili vengono rifiutati o restano in attesa.
- La rigenerazione elimina anche le scale e gli ordini di installazione.

API: `TrenchWorkOrders.DesignateLadder(cell)` accoda l'installazione;
`MapTilemapView.CompleteLadder(map, cell)` completa dati e vista;
`MapData.AddLadder(cell)` modifica soltanto i dati, con validazione esplicita.
`HasLadder(cell)` legge lo stato. `IsWalkable(cell)` indica che la cella puo
ospitare un soldato, non che sia raggiungibile dal terreno:
le transizioni tra livelli sono validate da `CanStep(from, to)`.

## Step successivi (non implementati)

Priorita modificabili dei lavori, costruzioni e rifornimenti, evoluzione dei
bisogni, copertura e combattimento. I bisogni a 100 non impediscono ancora
di muoversi o scavare.