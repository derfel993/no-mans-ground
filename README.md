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
| Maiusc + clic destro sul terreno | Aggiunge/rimuove un ostacolo di debug, a soldati fermi |
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
- Oggetto **Ground**: riferimenti ai tre tile.
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

Le future attivita assegneranno esperienza con
`soldier.GetSkill(SoldierSkill.Aim).AddExperience(amount)`.
Per ora nessuna skill cresce da sola. Velocita influenza gia lo spostamento;
le altre skill non modificano ancora attivita o combattimento.
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
Un soldato in marcia e occupato e non riceve altri ordini fino all'arrivo.
Se tutti sono occupati, il comando viene rifiutato con un messaggio: non c'e
ancora una coda di job.

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
quando tutti i soldati sono fermi. Il ricalcolo per ostacoli dinamici verra
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

## Step successivo (non implementato)

Lavori condivisi con priorita, coda, durata e completamento, a partire dallo scavo.
Evoluzione dei bisogni, effetti delle altre skill e combattimento rimangono
fuori da questo step. I bisogni a 100 non impediscono ancora di muoversi.