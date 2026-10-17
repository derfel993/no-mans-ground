# No Man's Ground

Prototipo Unity 6 di un gestionale 2D sulle trincee della prima guerra mondiale,
con visuale dall'alto. Il progetto Unity si trova in `No Man's Ground`.

## Changelog

Le versioni e le novita implementate sono raccolte in [CHANGELOG.txt](CHANGELOG.txt),
con la versione piu recente in alto. Il file viene aggiornato soltanto quando
l'utente lo richiede confermando di aver effettuato un commit, non automaticamente
a ogni modifica. Ogni voce indica versione, data e novita rispetto alla precedente;
la prima versione, 0.1.0, riepiloga il prototipo esistente.
I lavori futuri restano nelle checklist TODO di questo README.

## Step 1: mappa e navigazione

Aprire il progetto con Unity **6000.6.6f1**, quindi aprire
`Assets/Scenes/MapPrototype.unity` e premere **Play**.
La scena originale `SampleScene` non viene modificata.

La mappa iniziale misura 128 x 128 celle sul piano XY; ogni cella occupa
1 unita Unity. Terra, erba, fango e roccia sono generati con tile provvisori.
La roccia e non percorribile; la fascia iniziale resta libera per gli arrivi.
Gli alberi coprono il 40% delle celle non rocciose, sono disegnati sopra il
terreno e restano percorribili; il seed rende riproducibile la loro posizione.
Il terreno viene generato all'avvio, non salvato come migliaia di tile nella scena.

Sul bordo inferiore viene generata una **strada larga 4 tile e lunga 6 tile**
verso l'interno, centrata orizzontalmente (x 62-65, y 0-5 nella mappa 128 x 128).
Il tile provvisorio grigio-terra distingue la strada dal terreno circostante.
E percorribile come il terreno normale, senza bonus di velocita; la cucina viene
collocata fuori dalla carreggiata. Le zone designate restano percorribili.
La strada viene ricreata anche alla
rigenerazione, indipendentemente dal seed. Su mappe piu piccole di 4 x 6 viene
ridotta alle dimensioni disponibili.
E il futuro accesso per i rifornimenti e l'evacuazione dei feriti:
convogli, consegne e trasporto dei feriti **non sono ancora implementati**.

### Comandi

| Input | Azione |
| --- | --- |
| WASD / frecce | Sposta la camera |
| Tasto centrale + trascinamento | Trascina la visuale |
| Rotella | Zoom sul punto sotto il cursore |
| Clic sinistro su un soldato | Seleziona e mostra la scheda |
| Clic sinistro sul terreno | Deseleziona |
| Pulsanti Guardia / Lavoro nella scheda | Cambia lo stato del soldato selezionato |
| Clic destro sul terreno | Muove soltanto il soldato selezionato in Guardia |
| T / pulsante Scava | Attiva/disattiva la designazione degli scavi |
| L / pulsante Installa scala | Clic su una trincea completata per ordinare l'installazione di una scala |
| Sinistro + trascinamento in modalita Scava | Designa una linea di celle; un clic designa una cella |
| Pulsanti Discarica / Materiali / Dispensa / Cimitero | Seleziona il tipo di zona da disegnare |
| Sinistro + trascinamento in modalita zona | Designa un rettangolo di celle |
| Esc / destro in modalita zona | Esce dalla modalita senza impartire ordini |
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
  inferiore a quella dell'erba, e quella della roccia superiore a entrambe.
  Una scala del rumore maggiore produce zone piu ampie.
- Oggetto **Ground**: riferimenti ai tile di terra, erba, fango, roccia, strada,
  trincea e scala.
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
- `Assets/Scripts/Map/MapTilemapView.cs`: disegno del terreno e colori persistenti delle zone.
- `Assets/Scripts/Map/MapCameraController.cs`: input tramite Input System.
- `Assets/Scripts/Map/MapPrototype.cs`: collegamento tra generazione, vista e camera.
- `Assets/Editor/MapPrototypeSetup.cs`: creazione della scena e degli asset provvisori.

### Test

Aprire **Window > General > Test Runner**, selezionare **EditMode** ed eseguire
`NoMansGround.Tests` (incluse le prove input con il fixture ufficiale Input System).
I test verificano riproducibilita, variazione del seed, validazione dei parametri,
tile e riferimenti della scena, panning con mouse, punto di zoom, limiti della
camera e designazione/colorazione delle zone.
Il manifest abilita i test del pacchetto Input System per rendere disponibile
il suo fixture ufficiale; selezionare il namespace del progetto per eseguire
soltanto i test di No Man's Ground.

### Orologio e ciclo dei giorni

La partita inizia al **Giorno 1, ore 09:00**. L'orologio resta visibile in cima
al pannello destro anche senza un soldato selezionato o mentre si ispeziona un edificio.
Un ciclo di **24 ore dura 40 minuti di gioco non in pausa** a velocita normale:
1 secondo reale corrisponde a 36 secondi dell'orologio, 100 secondi a un'ora.
Il giorno aumenta a mezzanotte; la prima arriva dopo 25 minuti, perche si parte
dalle 09:00. Dopo 40 minuti si raggiunge il Giorno 2, ore 09:00.

Il tempo segue `Time.deltaTime`: rispetta pausa e velocita della simulazione.
I pulsanti 1x, 2x, 5x e 10x sotto l'orologio accelerano insieme orologio, movimento
dei soldati e sistemi che usano il tempo scalato.
Rigenerare la mappa riporta anche l'orologio al Giorno 1, ore 09:00.
Per ora il ciclo non modifica illuminazione, velocita di consumo dei bisogni,
durata dei job o preparazione del rancio. I loro tempi esistenti restano invariati.
Alle 13:00 viene avviata automaticamente la distribuzione del rancio.

## Step 2: soldati, stato e skill

La stessa scena include **8 soldati italiani del Genio e un cuoco aggiuntivo**,
visibili con sprite provvisori.
Il primo viene selezionato all'avvio; il contorno giallo indica la selezione.
Il pannello del soldato compare in basso a sinistra e offre quattro tab:
**Info** (identita, istruzioni, lavoro/guardia e carico massimo), **Stato**
(fame, sete, bisogni fisiologici, termico, stress, paura e stanchezza),
**Skill** e **Inventario**. Il tab Inventario e vuoto; la gestione degli oggetti
non e ancora implementata. Il pannello laterale destro mantiene orologio,
comandi, zone e informazioni sulle strutture.
Selezionare un altro soldato con clic sinistro o il terreno per deselezionare.
Zoom e panning restano disponibili; i clic sul pannello non selezionano soldati
dietro l'interfaccia.

### Anagrafica e stato

Nome e cognome sono generati da un repertorio italiano, senza duplicati nella
stessa squadra. Il seed dei soldati e indipendente da quello del terreno.
I dati del soldato sono separati dal suo sprite e dal pannello.

Fame, sete e bisogni fisiologici sono **riserve che diminuiscono nel tempo**.
Alla nascita sono sempre **100/100**, cioe completamente soddisfatti:
la barra e piena e verde. A **0** il bisogno deve essere soddisfatto.
Temperatura, paura e stanchezza restano statici.
Lo stress cresce con la fame sotto 35, senza impedire il lavoro.

| Indicatore | Significato |
| --- | --- |
| Fame | 100 sazio, 0 deve mangiare |
| Sete | 100 dissetato, 0 deve bere |
| Bisogni fisiologici | 100 nessuna urgenza, 0 deve soddisfare il bisogno |
| Temperatura percepita | -100 freddo estremo, 0 neutra, +100 caldo estremo |
| Stress | 0 rilassato, 100 stress massimo |
| Paura | 0 nessuna paura, 100 paura massima |
| Stanchezza | 0 riposato, 100 esausto |

La temperatura e uno stato relativo, non una misura in gradi Celsius.
La barra termica indica l'intensita del disagio, blu per freddo e arancione per caldo.
Il coraggio e una skill distinta dalla paura attuale.

### Consumo dei bisogni

Per ciascun soldato e per ciascuna delle tre riserve viene estratta una durata
indipendente e uniforme tra **17 e 23 minuti di gioco** (20 minuti +/-15%).
La variazione si applica al **tempo da 100 a 0**, non direttamente al rate.
Il rate costante e `100 / durataInSecondi` punti al secondo:
circa **5 punti al minuto** in media nominale (4,35-5,88 agli estremi).
Il tempo medio di esaurimento della distribuzione e 20 minuti; una piccola
squadra non ha necessariamente questa media esatta.

Le durate sono generate con il seed dei soldati e restano costanti nella
sessione, senza alterare anagrafica, skill e altri stati generati.
Il consumo avviene sia da fermi sia in marcia o al lavoro e segue la velocita
della simulazione. Usa il tempo scalato Unity (`Time.deltaTime`): a velocita 1x
un secondo reale corrisponde a un secondo di gioco, `Time.timeScale = 0` sospende
il consumo. I pulsanti 1x, 2x, 5x e 10x sotto l'orologio cambiano la velocita;
un controllo di pausa dedicato non e ancora disponibile.

Le barre si svuotano gradualmente e cambiano da verde a rosso. I valori
visualizzati sono arrotondati, mentre il consumo mantiene la precisione
frazionaria e non scende mai sotto zero. La fame torna a 100 quando il cuoco
consegna una razione durante il giro delle 13. Non sono ancora presenti acqua,
servizi igienici o interruzioni automatiche dei lavori.
La rigenerazione ripristina le tre riserve a 100 e ricrea gli stessi rate.

`SoldierNeeds` espone le durate `HungerDepletionSeconds`,
`ThirstDepletionSeconds`, `PhysicalNeedsDepletionSeconds` e i rispettivi
`ConsumptionPerSecond`; `AdvanceTime(gameSeconds)` applica il consumo.
`SoldierView` avanza i bisogni durante Play e il pannello si aggiorna tramite
l'evento `Changed`, anche quando il soldato e fermo.

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
La capacita massima trasportabile dipende dalla Forza: parte da **20 kg** al
livello 1 e cresce linearmente fino a **35 kg** al livello 20. Il carico massimo
attuale appare nella scheda del soldato e si aggiorna quando la Forza aumenta.
Per provare la progressione in Play, selezionare un soldato e usare
**Debug/Add 100 Aim XP to Selected Soldier** nel menu contestuale del componente
**Soldier Details Panel**, sull'oggetto **Soldier Selection**.
Il pannello aggiorna immediatamente XP e livello.

### Configurazione e limiti

- Oggetto **Soldiers**, componente **Soldier Roster**: numero di soldati del Genio,
  seed e sprite. **Spawn Cook** aggiunge un cuoco, entro il limite totale di 64
  personaggi e la capienza della mappa. Cambiare prima di Play o rigenerare.
- Le posizioni iniziali sono celle distinte centrate orizzontalmente vicino al
  bordo inferiore, la zona d'arrivo dei soldati. La camera iniziale inquadra
  questa zona rispettando i limiti della mappa. Terra, erba, fango e strada sono
  percorribili; la roccia blocca movimento e scavi, mentre la trincea segue le
  regole di accesso tramite scala. Non vengono valutate coperture.
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
`SoldierInputTests` verifica selezione, movimento della guardia selezionata e ostacoli tramite clic simulati
con il fixture ufficiale Input System.

## Step 3: movimento e pathfinding

Con **clic destro** su una cella si muove soltanto il soldato selezionato,
che deve essere **in Guardia**. Il soldato resta in Guardia all'arrivo.
Un nuovo clic destro durante la marcia sostituisce la destinazione: il soldato
termina il passo verso il prossimo centro cella e segue il nuovo percorso,
senza teletrasporti o tagli degli ostacoli. Vale sempre l'ultimo ordine valido;
una destinazione bloccata, prenotata o irraggiungibile conserva il percorso precedente.
In Lavoro il destro viene rifiutato con un messaggio, senza scegliere un altro
soldato. Il movimento manuale non ha una coda, mentre gli scavi si.

### Percorsi e movimento

- Otto direzioni; una diagonale e consentita solo se entrambe le celle laterali
  sono percorribili, evitando di tagliare gli angoli degli ostacoli.
- Ricerca Dijkstra inversa dalla destinazione: per gli ordini di Guardia calcola
  il percorso del selezionato; per i lavori confronta i lavoratori liberi.
  Costo di griglia 10 per passo ortogonale e 14 per diagonale.
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
  materiale e richiesto. Le riserve continuano a calare al rate normale.
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

## Step 5: fame, mestieri, cucina e dispensa

Con fame **strettamente sotto 35/100**, il soldato risulta **in attesa del rancio**:
non cerca cibo, non va alla cucina e continua a muoversi e lavorare normalmente.
Lo stress aumenta di **5 punti per minuto di gioco**, fino a 100, soltanto
per il tempo passato sotto soglia (anche nei frame che attraversano la soglia).
A 35 esatti non e ancora in attesa. Per ora lo stress non cala automaticamente.
L'attesa e indicata sulla scheda, insieme al mestiere.

### Mestieri iniziali

- **Genio**: gli 8 soldati iniziali eseguono scavi e installazioni di scale.
- **Cuoco**: un nono personaggio con nome italiano e tinta chiara raggiunge
  la cucina, prepara il rancio e lo distribuisce. Non riceve scavi, scale o spostamenti
  manuali con il destro; la sua posizione resta prenotata per gli altri.
- **Soldato**: ruolo previsto per il solo combattimento futuro, escluso dai
  lavori del Genio. Il combattimento non e ancora implementato.

Il mestiere appartiene a `SoldierData.Profession`. Non e ancora presente
un'interfaccia per scegliere o cambiare il mestiere dei soldati.

### Cucina e produzione

La scena genera automaticamente la sola **CUCINA**, con ingombro non percorribile
e non scavabile. Il giocatore deve disegnare una zona **Dispensa** sulla mappa:
le sue celle restano percorribili e il cuoco raggiunge una cella al suo interno
con il normale pathfinding. Senza dispensa non ci sono viveri e la produzione
resta ferma. La dispensa designata riceve le scorte iniziali di **1000 viveri**.
Se manca spazio per la cucina o il suo accesso e occupato/bloccato, il sistema
lo segnala; gli accessi vengono ritentati automaticamente ogni mezzo secondo.
Su mappe senza capienza per il cuoco la cucina non viene generata.

- La cucina parte con **0 razioni pronte**; i viveri sono disponibili solo dopo
  aver designato la dispensa.
- La produzione e continua, indipendente dal numero di soldati affamati,
  tranne durante gli spostamenti e il giro di distribuzione.
- Ogni razione richiede **30 secondi di gioco**, senza bonus delle skill.
- Prima della prima razione, e dopo ogni razione completata, il cuoco raggiunge
  il deposito e preleva fisicamente **10 viveri**, poi torna alla cucina.
- I viveri vengono scalati dal deposito al prelievo e trasferiti in cucina;
  il completamento della razione non li sottrae una seconda volta.
- Il viaggio non conta come preparazione. Pausa, assenza o disabilitazione
  del cuoco sospendono la produzione conservando il progresso.
- Con meno di 10 viveri il cuoco si ferma. La scorta iniziale consente
  **100 razioni**, per 50 minuti di cottura effettiva, oltre ai viaggi.
- **Clic sinistro sulla cucina**: mostra razioni pronte, viveri in cucina e
  dispensa, avanzamento della razione, carico trasportato e stato del cuoco.
  Sulla dispensa mostra i viveri residui e quelli in trasporto.
  Uscire prima dalle modalita Scava/Scala/Annulla con Esc.
- Rigenerare ricrea cuoco e cucina, rimuove le zone designate e azzera scorte,
  razioni, progresso e stress accumulato.

`RationStock` gestisce tempi e contabilita; `KitchenSystem` gestisce strutture,
prenotazione del cuoco, preparazione e distribuzione.

### Distribuzione quotidiana alle 13:00

Ogni giorno alle **13:00**, il cuoco sospende la produzione (conservando il
progresso) e ritira dalla cucina fino a una razione per ogni soldato, incluso
se stesso. Mangia la propria razione in cucina; raggiunge gli altri uno alla
volta, scegliendo il percorso raggiungibile piu breve verso una cella adiacente.
Le razioni trasportate sono gia sottratte alle razioni pronte della cucina.

- La consegna richiede la vicinanza effettiva e un passaggio valido tra le
  celle: non si serve attraverso pareti di trincea senza scala.
- Ogni destinatario riceve **una sola razione al giorno**, anche con fame sopra
  35, e torna a **100 fame**. Sete, bisogni fisiologici e stress non vengono
  ripristinati. La fame riprende poi a diminuire al ritmo abituale.
- I soldati non interrompono ordini o lavoro per mangiare. Se si spostano,
  il cuoco termina il percorso gia iniziato e cerca nuovamente dove raggiungerli.
- Se mancano razioni, distribuisce quelle disponibili, torna a cucinare e
  riparte per i destinatari rimasti, anche dopo l'esaurimento dei viveri se
  ci sono ancora razioni pronte.
- I destinatari irraggiungibili restano in attesa e vengono ritentati.
  A mezzanotte il giro incompleto termina; le razioni ancora trasportate
  tornano al conteggio della cucina. Il giorno dopo alle 13 inizia un nuovo giro.
- La cucina mostra anche razioni in consegna e destinatari in attesa.
  Terminato il giro, il cuoco torna alla produzione normale.
- Rigenerazione della mappa: azzera anche il giro e il registro dei pasti.

### TODO rancio

- [ ] Prevedere le **provvigioni necessarie a creare il rancio** con tipi,
  ricette, approvvigionamento e rifornimento del deposito. Il prototipo
  implementa soltanto il costo fisso di 10 unita per razione.
- [x] **Ritiro fisico dei viveri per ogni razione**: dopo aver terminato una
  razione, il cuoco raggiunge il deposito, preleva **10 viveri** e torna alla
  cucina per preparare la successiva. Gestire anche il prelievo iniziale prima
  della prima razione; il viaggio non conta come tempo di cottura e i viveri
  prelevati non devono essere sottratti nuovamente al completamento.
- [x] Distribuzione del rancio in trincea da parte del cuoco e consumo dei soldati.
- [ ] Assegnazione e cambio dei mestieri da parte del giocatore.

## Step 6: Guardia e Lavoro

Ogni personaggio ha uno stato distinto dal mestiere:

- **Lavoro**: partecipa soltanto ai job consentiti dal mestiere (Genio: scavi e
  scale; Cuoco: rancio). Non riceve spostamenti manuali con il destro.
- **Guardia**: non prende job. Selezionarlo con il sinistro e usare il destro
  per raggiungere una cella; rimane in Guardia sulla destinazione, anche se
  vengono designati lavori vicini. Non sono ancora presenti tiro o nemici.

Nella scheda del soldato sono presenti i pulsanti **Guardia** e **Lavoro**.
Il contatore **Lavoro: N | Guardia: N** resta visibile anche senza selezione
o quando si ispeziona un edificio, e conta gli stati effettivi di tutti
i personaggi generati, **incluso il cuoco in Lavoro**.
All'avvio normale indica Lavoro: 9 e Guardia: 0.

Se si richiede Guardia durante un job, il soldato resta in Lavoro fino al
completamento del job assegnato, incluso l'eventuale viaggio. La scheda indica
**Lavoro -> Guardia a fine job**; fino ad allora il contatore lo considera
ancora in Lavoro. Non ricevera il job successivo. Premere Lavoro prima della
fine annulla la richiesta. Se il job viene annullato o rilasciato, la richiesta
viene applicata al rilascio; un percorso gia iniziato termina senza teletrasporti.
Tornando in Lavoro durante una marcia di Guardia, termina il percorso prima di
poter prendere job. In Guardia il destro sostituisce anche percorsi gia in corso.

Il **cuoco resta sempre in Lavoro** e il suo pulsante Guardia e disabilitato.
Il mestiere non cambia passando tra i due stati. I bisogni e lo stress
continuano ad avanzare in entrambi. Rigenerare ripristina tutti in Lavoro,
azzera le richieste pendenti e aggiorna il contatore.

API: `SoldierView.RequestDuty(SoldierDuty.Guard/Work)`, `Duty`, `GuardRequested`;
`SoldierRoster.WorkCount` e `GuardCount` espongono i totali.
`SoldierMovementOrders.Selected` segue la selezione del pannello;
`IssueMove(cell)` valida e muove esclusivamente quella guardia.

## Step 7: zone designate

I pulsanti **Discarica**, **Materiali**, **Dispensa** e **Cimitero** attivano
la modalita di designazione. Trascinare con il sinistro dal tile iniziale a
quello finale per creare un rettangolo; il verso del trascinamento non conta.
L'anteprima segue il cursore e, al rilascio, il colore viene applicato ai tile
e resta visibile: grigio per la discarica, rosso per i materiali, verde per la
dispensa e grigio scuro per il cimitero. **Esc** o destro escono dalla modalita.

Si possono aggiungere piu rettangoli dello stesso tipo. Le zone di tipo diverso
non si sovrappongono; celle fuori mappa, bloccate o in trincea non sono valide.
Le zone restano percorribili. Per ora non si possono cancellare e durano solo
fino alla rigenerazione della mappa.

- **Dispensa**: il cuoco non ha viveri finche non viene designata. La prima
  dispensa fornisce le scorte iniziali di 1000 viveri; il cuoco raggiunge una
  sua cella e la usa come origine del prelievo.
- **Discarica**: area disponibile per future consegne di detriti e rifiuti.
- **Deposito materiali**: destinazione disponibile per futuri materiali da
  costruzione e rifornimenti dalle retrovie.
- **Cimitero**: area designata, senza ancora funzioni di sepoltura.

I lavori di bagno, raccolta e smaltimento dei rifiuti/detriti, trasporto dei
materiali e rifornimento dalle retrovie non sono ancora implementati. Le zone
forniscono le destinazioni, non assegnano ancora questi lavori ai soldati.
Rigenerare la mappa elimina le zone e ripristina lo stato iniziale.

## Step successivi (non implementati)

### TODO zone e logistica

- [ ] Collegare i bisogni fisiologici alla **discarica**: senza una zona valida
  i soldati non possono andare al bagno; implementare raccolta e smaltimento
  di rifiuti e detriti.
- [ ] Collegare raccolta e trasporto di risorse al **deposito materiali**;
  aggiungere anche i rifornimenti in arrivo dalle retrovie.
- [ ] Implementare le funzioni del **cimitero**.

### TODO risorse e rifugi

- [ ] **Alberi**: permettere al giocatore di designarne l'abbattimento come job
  per ottenere **legna**. La generazione e il rendering degli alberi sono presenti;
  non si potra scavare una trincea in una cella che contiene un albero.
- [ ] **Estrazione della roccia**: designare celle Rock per ottenere
  **pietra da costruzione** tramite un job del Genio.
- [ ] **Risorse raccolte**: gestire legna e pietra come materiali disponibili
  per le costruzioni.
- [ ] **Rifugi con costruzione a celle, in stile RimWorld**: designare pareti
  e porte, richiedere materiali e assegnare i lavori di costruzione ai soldati.
- [ ] **Tetti automatici**: quando le pareti e le porte costruite chiudono
  un perimetro, riconoscere lo spazio interno e generare automaticamente
  il tetto del rifugio.

### TODO stati e inventario

- [ ] **Termico**: sostituire il valore statico con variazioni ambientali e
  gestione di caldo/freddo.
- [ ] **Stress**: aggiungere recupero e gestione degli effetti; al momento cresce
  soltanto quando la fame scende sotto 35.
- [ ] **Paura**: definire cause, variazione e gestione del valore.
- [ ] **Stanchezza**: implementare consumo, riposo e recupero.
- [ ] **Inventario**: introdurre gli oggetti trasportabili, il peso corrente e
  le regole di carico usando la capacita massima gia mostrata in Info.

Priorita modificabili dei lavori, costruzioni e rifornimenti, soddisfacimento
dei bisogni, evoluzione degli altri stati, copertura e combattimento.
Anche con riserve a zero i soldati possono ancora muoversi e lavorare.