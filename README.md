# No Man's Ground

Prototipo Unity 6 di un gestionale 2D sulle trincee della prima guerra mondiale,
con visuale dall'alto. Il progetto Unity si trova in `No Man's Ground`.

**Versione corrente: 0.5.0.**

## Changelog

Le versioni e le novita implementate sono raccolte in [CHANGELOG.txt](CHANGELOG.txt),
con la versione piu recente in alto. README e changelog vengono aggiornati dopo
che l'utente conferma che il task e corretto. La versione del changelog resta
invariata, salvo richiesta esplicita dell'utente.
Ogni voce indica versione, data e novita rispetto alla precedente;
la prima versione, 0.1.0, riepiloga il prototipo esistente.
I lavori futuri restano nelle checklist TODO di questo README.

## Avvio del gioco

Aprire `Assets/Scenes/Splash.unity` e premere **Play** per provare l'avvio
completo: l'immagine `Assets/Art/UI/Splash/SplashScreen.jpg` viene mostrata
per almeno **13 secondi**, poi attende un **nuovo clic sinistro** per caricare
la scena di test `MapPrototype`. I clic anticipati, anche se mantenuti premuti,
non fanno proseguire lo splash; il destro non conferma.
La mappa viene precaricata durante lo splash; il passaggio attende anche
che il caricamento sia pronto. Il tempo dello splash non dipende dalla pausa.

L'immagine mantiene le proporzioni senza ritagli; eventuali spazi vuoti
vengono riempiti in nero. Sul componente **Game Splash Screen** dell'oggetto
**Game Startup** si possono modificare immagine, durata e scena successiva.
La lista delle scene di build inizia con Splash, seguita da MapPrototype;
SampleScene viene conservata. Aprire direttamente MapPrototype in Editor
continua a permettere di provare soltanto la mappa, saltando lo splash.

## Step 1: mappa e navigazione

Aprire il progetto con Unity **6000.6.6f1**, quindi aprire
`Assets/Scenes/MapPrototype.unity` e premere **Play**.
La scena originale `SampleScene` non viene modificata.

La mappa iniziale misura 128 x 128 celle sul piano XY; ogni cella occupa
1 unita Unity. Terra, erba, fango, roccia e montagna sono generati con tile provvisori.
Roccia e montagna sono non percorribili; la fascia di arrivo resta libera.
La **Montagna** ha una texture piu scura della roccia e occupa le zone piu alte
del rumore: la soglia e `rockThreshold + (1 - rockThreshold) * 0.25`.
Gli alberi coprono il 40% delle celle non rocciose e non montane fuori dalle aree riservate; sono
disegnati sopra il terreno e restano percorribili; il seed rende riproducibile
la loro posizione.
Accanto alla strada viene creato un deposito di **4 x 4 celle**; nella mappa
standard si trova sul lato destro della strada (a sinistra se manca spazio).
Subito sotto c'e una dispensa automatica di **2 x 4 celle**. Entrambe le aree
restano percorribili e libere da rocce e alberi; mappe troppo piccole possono
non avere spazio per crearle. Nel deposito ci sono **6 pale, 2 accette e 2
picconi e 2 martelli da costruzione**; nella dispensa **1000 Razioni**, in quattro stack da **250**.
Gli item hanno categorie **Tool**, **Weapon**, **Food**, **Resource** e **Equipment**. Per ora i Tool sono
Pala (**Scava trincea**), Piccone (**Rompi roccia**), Accetta (**Taglia albero**)
e Martello da Costruzione (**Costruisci**, inclusa l'installazione delle scale);
Food contiene Razioni; Resource contiene Legna e Pietra da costruzione;
Equipment contiene **Borraccia piena (acqua)** e **Borraccia vuota**, non impilabili.
Gli utensili vengono ritirati per i lavori ma non consumati al completamento.
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

### Fog of war

La fog of war si attiva automaticamente nella scena di gioco e usa la visuale
condivisa di tutti i soldati attivi, indipendentemente da Guardia/Lavoro o compito.
Ogni soldato scopre un **cono di 180 gradi**, con raggio di **12 tile di giorno**
e **6 tile di notte** dalla propria cella, orientato nella direzione dello sguardo:
non vede a 360 gradi. Il raggio varia gradualmente all'alba e al tramonto,
arrotondato alla cella; le zone che escono dalla visuale restano esplorate.
Camminando guarda verso il passo che sta percorrendo; all'arrivo conserva
l'ultima direzione. Lo sprite selezionato corrisponde a uno degli otto orientamenti
cardinali e diagonali.

Una **guardia ferma e senza incarichi** scruta alternativamente a sinistra
e a destra della direzione base, con oscillazione morbida di **45 gradi per lato**
e ciclo completo di **6 secondi di gioco**. Il cono segue lo sguardo e scopre
progressivamente i fianchi; le zone che escono dal cono restano esplorate ma
attenuate. Camminando o tornando in Lavoro la scansione si interrompe.
Pausa e velocita della simulazione si applicano anche all'oscillazione.

Per orientare una guardia ferma, **premere il destro sul soldato e trascinare**:
lo sguardo segue il cursore senza impartire movimento e senza cambiare il soldato
selezionato. Durante il trascinamento la scansione automatica e sospesa;
al rilascio riprende centrata sulla nuova direzione. La guardia deve essere
visibile, libera e senza cambi di dotazione pendenti. Anche in pausa si puo
orientare manualmente. Uscire dall'area di gioco, passare sulla UI o cambiare
modalita annulla il trascinamento, conservando l'ultimo orientamento valido.
Un clic destro sul terreno resta invece un normale ordine di movimento.

- **Nero**: terreno mai esplorato, completamente nascosto.
- **Attenuato**: zona gia esplorata ma fuori dalla visuale attuale. Si vede
  soltanto il terreno ricordato dall'ultima osservazione, non gli oggetti presenti.
- **Normale**: zona attualmente visibile, con terreno, risorse e strutture aggiornati.

Rocce, montagne, celle bloccate, pareti e porte completate interrompono la
visuale; il primo ostacolo resta visibile ma le celle dietro sono nascoste.
Ogni tile di parete o porta completato rivela inoltre le celle entro 2 tile
in tutte le direzioni, anche durante la costruzione parziale dell'edificio.
Oltre questo raggio la parete continua a bloccare la visuale.
Ogni tile di trincea completato rivela a sua volta le celle entro 2 tile in
tutte le direzioni. Non si vede diagonalmente attraverso gli angoli chiusi.
Alberi e trincee non bloccano la visuale. Una porta designata ma non ancora
costruita non la blocca.
Entrando su una porta o in una galleria il soldato scopre lo spazio dall'interno.

I comandi di movimento, scavo, minatura, costruzione e zona richiedono celle
gia esplorate. Per scoprire nuove aree, spostare una guardia verso il bordo
della zona nota; avanzando amplia la visuale. Taglia e Raccogli possono essere
designati anche su alberi e risorse remembered: il terreno ricordato resta
attenuato e il lavoro viene annullato se il bersaglio non esiste piu al momento
dell'esecuzione. Le celle mai esplorate restano inaccessibili. I lavori gia
designati non vengono annullati quando escono dalla visuale. Non si possono
ispezionare strutture o usare ostacoli debug attraverso la nebbia.

La visuale viene aggiornata ogni **0,1 secondi reali**, anche durante la pausa;
nessun movimento o lavoro avanza per questo motivo. Rigenerare elimina tutta
la memoria esplorata e scopre nuovamente soltanto l'area della squadra.
Il componente **Soldier Fog Of War** viene aggiunto all'oggetto Soldiers
durante Play: **Sight Radius** regola il raggio diurno e **Night Sight Radius**
quello notturno, entrambi tra 1 e 64 tile. Il raggio notturno effettivo non puo
superare quello diurno. Senza un componente **Day Night Cycle** sulla mappa,
resta attivo il raggio diurno. Per conservare valori personalizzati si puo
aggiungere il componente alla scena prima di Play e salvarla; il sistema
riutilizza quello presente.

### Comandi

| Input | Azione |
| --- | --- |
| WASD / frecce | Sposta la camera |
| P | Attiva/disattiva la pausa; riprendendo torna all'ultima velocita selezionata |
| Tasto centrale + trascinamento | Trascina la visuale |
| Rotella | Zoom sul punto sotto il cursore |
| Clic sinistro su un soldato | Seleziona e mostra la scheda |
| Clic sinistro sul terreno | Deseleziona |
| Pulsanti Guardia / Lavoro nella scheda | Cambia lo stato del soldato selezionato |
| Clic destro sul terreno | Muove soltanto il soldato selezionato in Guardia |
| Destro su una guardia ferma + trascinamento | Orienta lo sguardo; al rilascio la scansione riprende attorno alla nuova direzione |
| T / pulsante Scava | Attiva/disattiva la designazione degli scavi |
| A / pulsante Taglia | Attiva/disattiva il taglio degli alberi con clic o rettangolo |
| Sinistro + trascinamento in modalita Taglia | Accoda il taglio di tutti gli alberi nel rettangolo |
| M / pulsante Mina | Attiva/disattiva la designazione di rocce da minare |
| C / pulsante Raccogli | Attiva/disattiva la raccolta con clic o rettangolo |
| Sinistro + trascinamento in modalita Raccogli | Accoda la raccolta delle risorse nel rettangolo |
| L / pulsante Installa scala | Clic su una trincea completata per ordinare l'installazione di una scala |
| Sinistro + trascinamento in modalita Scava | Designa una linea di celle; un clic designa una cella |
| Pulsanti Discarica / Materiali / Cimitero | Seleziona il tipo di zona da disegnare |
| Sinistro + trascinamento in modalita zona | Designa un rettangolo di celle |
| Esc / destro in modalita zona | Esce dalla modalita senza impartire ordini |
| X / pulsante Annulla scavi | Attiva/disattiva la cancellazione lungo una linea |
| Esc / destro in modalita Scava o Annulla | Esce dalla modalita, senza impartire movimento |
| Maiusc + clic destro sul terreno | Aggiunge/rimuove un ostacolo debug, a soldati fermi e senza scavi in coda |
| Rotella sul pannello dei soldati | Nessuno zoom sulla mappa |

Se un soldato occupa una cella di deposito o dispensa, il clic su quella cella
seleziona il soldato; gli altri tile della struttura restano ispezionabili.

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
- Oggetto **Soldiers**, componente **Soldier Fog Of War**: raggio della visuale
  condivisa diurna (**Sight Radius**, 12) e notturna (**Night Sight Radius**, 6);
  viene aggiunto automaticamente durante Play se non presente.
- Oggetto **Map**, componente **Day Night Cycle**: orari di inizio e fine
  di alba e tramonto, tonalita di giorno, alba, tramonto e notte. Il sistema
  prevede l'aggiunta automatica durante Play; si puo aggiungere prima di Play
  per salvare i parametri personalizzati. Vedere il limite noto sotto.
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
- `Assets/Scripts/Map/MapItems.cs`: categorie, definizioni, azioni e stockpile degli item.
- `Assets/Scripts/Map/MapGenerator.cs`: generazione con seed e rumore Perlin.
- `Assets/Scripts/Map/MapTilemapView.cs`: disegno del terreno e colori persistenti delle zone.
- `Assets/Scripts/Map/MapCameraController.cs`: input tramite Input System.
- `Assets/Scripts/Map/MapPrototype.cs`: collegamento tra generazione, vista e camera.
- `Assets/Scripts/Map/GameClock.cs`: orologio e ora frazionaria della simulazione.
- `Assets/Scripts/Map/DayNightCycle.cs`: fasi, luminosita e filtro visivo del mondo.
- `Assets/Resources/DayNightTint.shader`: filtro moltiplicativo del ciclo.
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
I pulsanti 0x, 1x, 2x, 5x e 10x sotto l'orologio impostano pausa o velocita;
0x ferma la simulazione. Anche P attiva/disattiva la pausa e, alla ripresa,
resta selezionata l'ultima velocita attiva. Il movimento della camera resta disponibile.
Rigenerare la mappa riporta anche l'orologio al Giorno 1, ore 09:00.
Il ciclo modifica la tonalita del mondo e il raggio visivo dei soldati.
Velocita di consumo dei bisogni, durata dei job e preparazione del rancio
mantengono i tempi esistenti, senza modificatori notturni.
Alle 13:00 viene avviata automaticamente la distribuzione del rancio.

### Illuminazione e visibilita notturna

Il componente **Day Night Cycle** usa direttamente l'ora frazionaria
dell'orologio, senza un secondo timer. Le fasi predefinite sono:

| Fase | Orario | Raggio visivo dei soldati |
| --- | --- | --- |
| Alba | 05:00-07:00 | Da 6 a 12 tile |
| Giorno | 07:00-18:00 | 12 tile |
| Tramonto | 18:00-20:00 | Da 12 a 6 tile |
| Notte | 20:00-05:00 | 6 tile |

Le transizioni di colore e luminosita sono morbide: tonalita calde all'alba
e al tramonto, neutre di giorno e piu scure di notte. Il filtro moltiplicativo
agisce sul mondo, non sui pannelli UI, e non intercetta i comandi del mouse.
Le celle inesplorate restano nere; i colori del terreno conservati dalla
fog of war non vengono modificati. La notte riduce la visuale corrente,
senza cancellare la memoria delle celle gia esplorate.

Orari e colori sono configurabili nell'Inspector. Il raggio segue la luminosita
del ciclo, anche se il filtro grafico e disattivato, e viene aggiornato dalla
fog of war ogni 0,1 secondi reali. Pausa e accelerazione seguono l'orologio;
rigenerare ripristina la fase diurna e il raggio iniziale. Sonno, recupero
notturno e turni automatici non sono ancora implementati.

**Verifiche e limite noto della 0.5.0:** superati 38 dei 39 test mirati
di orologio e ciclo, incluso il rendering del filtro, e tutti i 17 test
mirati della visibilita notturna e della fog of war. Resta aperto il test
di avvio automatico del ciclo: la fixture di integrazione non trova il
componente. L'avvio automatico nella scena non e quindi completamente verificato.

## Step 2: soldati, stato e skill

La stessa scena include **9 personaggi italiani**, visibili con sprite provvisori.
Tutti iniziano in **Lavoro**, con compito **Trasportatore**; il cuoco va assegnato
dal giocatore attraverso il menu Compito nella scheda Info.
Il primo viene selezionato all'avvio; il contorno giallo indica la selezione.
Il pannello del soldato compare in basso a sinistra e offre quattro tab:
**Info** (identita, lavoro/guardia, compito, stato della dotazione e carico massimo), **Stato**
(fame, sete, bisogni fisiologici, termico, stress, paura, stanchezza e salute),
**Skill** e **Inventario**. Il tab Inventario mostra gli utensili, le risorse
trasportate e la borraccia personale, piena o vuota, aggiornandosi dopo bevuta
e rifornimento. Il pannello laterale destro mantiene orologio,
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
**Paura, stanchezza e stress iniziano sempre a 0/100**, incluso per il cuoco.
Temperatura e paura restano statiche; la stanchezza cresce con movimento e lavori.
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
| Salute | 100 salute piena, 0 morto |

La temperatura e uno stato relativo, non una misura in gradi Celsius.
La barra termica indica l'intensita del disagio, blu per freddo e arancione per caldo.
Il coraggio e una skill distinta dalla paura attuale.

### Salute e morte

Ogni soldato, incluso il cuoco, parte con **Salute 100/100**. La barra nella
scheda **Stato** si aggiorna immediatamente quando subisce danni e passa
gradualmente dal verde al rosso. A **0 il soldato muore definitivamente**:
non puo piu muoversi, lavorare, ricevere ordini o contribuire alla fog of war.
Viene rimosso dalla squadra e dai conteggi Guardia/Lavoro; se era selezionato,
la scheda viene deselezionata. Le celle gia esplorate restano memorizzate.

I lavori non conclusi restano in coda per gli altri soldati, senza annullare
gli incarichi dei sopravvissuti. Se il soldato stava consegnando una risorsa
gia raccolta, la consegna viene rimossa e la risorsa resta recuperabile a terra.
L'inventario viene rilasciato sulle celle libere vicine, un oggetto per cella;
se non c'e spazio, il resto viene conservato nel deposito. Anche le borracce
piene e vuote possono essere disegnate a terra.

Se muore il cuoco, viveri e razioni in trasporto vengono conservati e puo
subentrare un altro cuoco disponibile. I morti non ricevono rancio o acqua.
Non sono ancora implementate cure o rigenerazione automatica della salute.
**Regenerate Map** ricrea la squadra con salute 100/100.

### Danni da fame e sete

Quando una riserva arriva a **0**, la salute diminuisce continuamente:

| Condizione | Perdita di salute per giorno di gioco |
| --- | --- |
| Fame a 0 | 5 punti |
| Sete a 0 | 30 punti |
| Fame e sete entrambe a 0 | 35 punti |

Il giorno di riferimento e quello dell'orologio: **2400 secondi di simulazione**,
pari a **40 minuti reali a velocita 1x**, esclusa la pausa. Il danno conserva
la precisione frazionaria, non viene applicato a scatti a mezzanotte e rispetta
pausa e accelerazione. Se una riserva si esaurisce durante un aggiornamento,
viene conteggiato soltanto il tempo successivo all'esaurimento.

Mangiare interrompe il danno da fame e bere quello da sete, ma **non recuperano
la salute gia persa**. La bevuta automatica dalla borraccia viene considerata
prima di applicare danni da disidratazione. I bisogni fisiologici a zero non
causano danni alla salute. Finche e vivo, il soldato puo continuare a muoversi
e lavorare anche con fame o sete esaurite.

I **63 test mirati** dei dati e della salute sono superati: verificano ratei,
soglie di esaurimento, aggiornamenti a 60 frame al secondo, pausa, velocita,
borracce, morte e integrazione con squadra, lavori, cucina, fog of war e UI.

### Stanchezza da movimento e lavoro

- **+1 per ogni tile raggiunto**, anche diagonale, in Guardia o in Lavoro.
  Il conteggio avviene all'arrivo: mezzo tile, sosta o rotazione dello sguardo
  non aggiungono punti. Vale anche per i viaggi al deposito e i trasporti.
- **+5 per ogni job completato**: scavo, taglio, minatura, costruzione, porta,
  scala e raccolta con consegna al deposito. Il ritiro di utensili o materiali
  e preparatorio: non aggiunge un secondo costo job.
- Per il cuoco, **+5 per ogni razione preparata** e **+5 per ogni consegna
  effettiva a un altro soldato**. Mangiare o bere non sono job aggiuntivi.

Un lavoro parziale, annullato o rilasciato non aggiunge i 5 punti; gli eventuali
tile gia percorsi restano conteggiati. I valori si sommano fino a **100/100**
senza superarlo. La barra si aggiorna immediatamente. Pausa e velocita della
simulazione agiscono sui tempi di esecuzione, non sul costo per tile o job.
Non sono ancora previsti recupero automatico, sonno, penalita o blocchi a 100.
Rigenerare la mappa azzera nuovamente stanchezza, paura e stress.

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
un secondo reale corrisponde a un secondo di gioco; pausa 0x sospende il consumo.
I pulsanti 0x, 1x, 2x, 5x e 10x sotto l'orologio cambiano la velocita; P attiva
e disattiva la pausa.

Le barre si svuotano gradualmente e cambiano da verde a rosso. I valori
visualizzati sono arrotondati, mentre il consumo mantiene la precisione
frazionaria e non scende mai sotto zero. La fame torna a 100 quando il cuoco
consegna una razione durante il giro delle 13. La sete torna a 100 bevendo dalla
borraccia sotto il 20%. Non sono ancora presenti servizi igienici o interruzioni
preventive dei lavori per soddisfare i bisogni; la morte li interrompe.
La rigenerazione ripristina le tre riserve e la salute a 100,
ricrea gli stessi rate e assegna una nuova borraccia piena a ogni personaggio.

`SoldierNeeds` espone le durate `HungerDepletionSeconds`,
`ThirstDepletionSeconds`, `PhysicalNeedsDepletionSeconds` e i rispettivi
`ConsumptionPerSecond`; `AdvanceTime(gameSeconds)` applica il consumo.
`SoldierData.AdvanceTime(gameSeconds)` coordina consumo dei bisogni, bevuta
automatica e danni alla salute; `SoldierView` lo richiama durante Play con
`Time.deltaTime`. Il pannello si aggiorna tramite gli eventi `Changed`
e `HealthChanged`, anche quando il soldato e fermo. `TakeDamage(amount)`
applica danni diretti; `IsAlive` indica se e vivo e `Died` notifica la morte.

### Borracce e acqua

Ogni soldato generato, **incluso il cuoco**, parte con **1 Borraccia piena
(acqua)** nell'inventario. Quando la riserva di sete scende **strettamente sotto
20/100**, beve automaticamente: la sete torna esattamente a **100/100**,
la borraccia piena viene consumata e al suo posto resta **1 Borraccia vuota**.
A 20 esatti non beve. La borraccia vuota non disseta: senza una borraccia piena,
la sete continua a diminuire normalmente, senza creare o duplicare item.

La bevuta e immediata e non interrompe marcia, Guardia o lavoro. Durante la
consegna effettiva del rancio, il cuoco **sostituisce la borraccia vuota con
una piena**; una borraccia gia piena non viene duplicata. Anche il cuoco viene
rifornito quando consuma la propria razione in cucina. Soldati non ancora
serviti non ricevono acqua a distanza, e un giro gia concluso non rifornisce
di nuovo una borraccia svuotata successivamente nello stesso giorno.

Riempire la borraccia non ripristina direttamente la sete: occorre berla.
Se al rifornimento la sete e gia sotto soglia, il soldato beve subito.
Per ora il rifornimento non richiede scorte d'acqua separate, viaggi o tempi
aggiuntivi; fonti d'acqua e riempimento autonomo fuori dal rancio non sono implementati.

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

- Oggetto **Soldiers**, componente **Soldier Roster**: numero di soldati,
  seed e otto sprite direzionali, nell'ordine Nord, Nord-Est, Est, Sud-Est,
  Sud, Sud-Ovest, Ovest, Nord-Ovest. Usare canvas, pivot e Pixels Per Unit
  uguali tra i frame; se manca un frame viene usato lo sprite di fallback.
  **Spawn Cook** aggiunge un personaggio, anch'esso Trasportatore,
  entro il limite totale di 64
  personaggi e la capienza della mappa. Cambiare prima di Play o rigenerare.
- Le posizioni iniziali sono celle distinte centrate orizzontalmente vicino al
  bordo inferiore, la zona d'arrivo dei soldati. La camera iniziale inquadra
  questa zona rispettando i limiti della mappa. Lo spawn e spostato sei tile
  verso nord (y 7-9 sulla mappa standard), evitando le aree di deposito. Terra,
  erba, fango e strada sono
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
- Se la linea attraversa una cella con un albero, viene accodato anche il job di
  taglio: prima si rimuove l'albero, poi il job scava la stessa cella. Lo scavo
  resta in attesa finche il taglio non e completato.
- Le celle gialle sono lavori pendenti; il colore tende al verde durante lo scavo.
  Duplicati, celle gia scavate, bloccate o fuori mappa non generano altri lavori.
  Il rilascio fuori mappa o sopra la UI scarta il trascinamento.
- **X** o **Annulla scavi** attiva una modalita analoga, con anteprima rossa,
  che rimuove i lavori di scavo o installazione scala lungo la linea.
  Sulla cella con albero annulla insieme il taglio associato e lo scavo.
  Le trincee e le scale completate non vengono rimosse.
- **Esc** o **destro** esce dalla modalita; quel clic non genera un ordine di marcia.
  In queste modalita il sinistro non cambia il soldato selezionato.
- **A** o **Taglia** permette di cliccare un singolo albero oppure trascinare
  un rettangolo per accodare il taglio di tutti gli alberi al suo interno.
  L'anteprima mostra il contorno; i job partono al rilascio del sinistro,
  indipendentemente dal verso del trascinamento. Celle senza alberi e lavori
  gia presenti vengono ignorati. Esc o destro annullano il trascinamento.
  Il job richiede un'**Accetta**: il lavoratore la ritira dal deposito se non l'ha gia
  nell'inventario. Senza Accette il lavoro resta in attesa. Completato il taglio,
  l'albero viene sostituito da un item **Legna** sulla stessa cella.
  Il taglio non attende che la cella sia libera dai percorsi altrui: rimuovere
  un albero non cambia la percorribilita, quindi job adiacenti non restano
  bloccati al 100% mentre altri soldati transitano.
- **C** o **Raccogli** attiva la raccolta: cliccare un item Resource a terra
  oppure trascinare un rettangolo per raccogliere tutte le risorse al suo interno.
  L'anteprima mostra il contorno; gli ordini vengono accodati al rilascio del
  sinistro, in qualunque verso si trascini. Celle vuote, utensili e risorse gia
  assegnate alla raccolta o prenotate da un costruttore vengono ignorati.
  Esc o destro annullano il trascinamento senza aggiungere ordini.
  Un lavoratore in Lavoro lo prende, lo mostra nell'Inventario e lo porta al
  deposito; la quantita compare nello stockpile.
- Annullare perde il progresso di quella cella e libera l'assegnazione senza XP.
  Un soldato gia in marcia raggiunge la posizione prenotata prima di tornare
  disponibile: nessun teletrasporto o interruzione a meta cella.

### Assegnazione ed esecuzione

- La coda usa l'ordine di designazione. Per ciascun lavoro viene scelto il soldato
  libero con il percorso piu breve verso una delle **quattro celle adiacenti
  ortogonali**, mai la cella da scavare. I lavori irraggiungibili non impediscono
  l'assegnazione di quelli successivi.
- Per iniziare uno scavo il soldato deve avere una **Pala** nell'inventario.
  Se non la possiede, raggiunge il deposito e ne prende una se disponibile;
  senza Pale lo scavo resta in attesa. L'attrezzo raccolto resta nel suo
  inventario e appare nel tab **Inventario**.
- **M** o **Mina** designa rocce e montagne lungo una linea. Un lavoratore con Piccone
  raggiunge una cella adiacente; se non lo possiede lo ritira dal deposito.
  Dopo **10 secondi di lavoro**, la roccia diventa terra e lascia **Pietra da
  costruzione** sulla stessa cella. La montagna diventa invece una **Galleria**
  percorribile e lascia la stessa Pietra da costruzione.
  Il viaggio al deposito e al lavoro non conta.
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

### Montagne, gallerie e rifugi scavati

La montagna e un terreno solido distinto dalla roccia normale, con texture
scura generata a runtime. Usare **Mina [M]** partendo da un bordo raggiungibile
e proseguire verso l'interno per scavare corridoi o allargare una camera.
Si possono designare anche celle profonde: restano in attesa finche il fronte
diventa raggiungibile, senza bloccare gli altri lavori in coda.

Ogni cella montana minata diventa **Galleria**: pavimento scuro e bordi rocciosi
sui lati ancora chiusi. Quando si scava una cella adiacente, il bordo comune
si apre automaticamente. I soldati entrano a piedi, senza scale, attraverso
il varco scavato; la montagna non minata resta invalicabile.

Le gallerie sono riconosciute come **interni sotto copertura naturale**, senza
dover costruire un rettangolo: la copertura non implica che siano rifugi chiusi.
La visuale lascia visibile lo scavo;
il tetto naturale non nasconde il pavimento. Non si possono scavare trincee o
sovrapporre progetti di edifici rettangolari alle gallerie. La pietra rilasciata
si raccoglie normalmente o viene ritirata per le costruzioni.
Freddo, isolamento, stufe, stabilita e danni alla montagna non sono implementati.
Rigenerare la mappa ricrea la montagna e azzera le gallerie scavate.

#### Porte e rifugi chiusi

Premere **Porta** nel pannello Costruzioni e cliccare una cella di galleria
in un passaggio largo un tile, tra due pareti opposte di roccia o montagna.
Le altre due celle adiacenti devono essere percorribili sullo stesso livello,
e almeno una deve proseguire nella galleria. Prima raccogliere la pietra o gli
altri item rimasti sulla cella della porta e terminare eventuali job presenti.

Un **Costruttore in Lavoro** usa un **Martello da Costruzione** per costruire una **porta di legno**:
costo **1 Legna**, ritirata da terra
o dal deposito, e durata `6 / (1 + Forza / 100)` secondi, esclusi i viaggi.
La risorsa si consuma solo al completamento, che assegna **25 XP Forza**.
Senza legna raggiungibile il job resta in attesa. **Annulla [X]** rimuove solo
il progetto non completato; la legna gia ritirata resta nell'inventario.
Le porte completate restano attraversabili dai soldati, come quelle degli edifici;
non e ancora prevista la demolizione o un comando di apertura manuale.

Il sistema distingue **gallerie aperte** e **rifugi chiusi**: una regione di
galleria diventa chiusa solo se ogni collegamento con l'esterno e separato da
una porta completata. Una porta solo designata non basta; con piu ingressi
bisogna chiuderli tutti. Scavare una nuova uscita riapre il rifugio. Una porta
interna puo anche separare due ambienti. Il pannello mostra i conteggi di rifugi
chiusi e regioni di galleria aperte. Freddo e isolamento restano futuri.
Rigenerare elimina porte, progetti e conteggi dei rifugi.

API: `IsIndoors(cell)` indica la copertura naturale o di un edificio completo;
`IsClosedShelter(cell)` distingue gli interni chiusi dalle gallerie aperte.

### Scale e accesso alle trincee

Premere **L** o **Installa scala**, quindi cliccare su una cella di trincea
gia completata. Il clic aggiunge un lavoro alla stessa coda degli scavi, non
crea subito una scala. **Esc/destro** esce dalla modalita e **X** annulla anche
le installazioni non terminate. Celle di terreno normale, bloccate, con una
scala gia presente o con un lavoro gia in coda vengono rifiutate con un messaggio.

Un soldato libero raggiunge una delle quattro celle adiacenti e installa
la scala senza dover entrare prima nella trincea. Puo lavorare dal terreno
o da un tratto di trincea gia raggiungibile. Serve un **Martello da Costruzione**,
ritirato dal deposito se manca nell'inventario; non vengono richiesti materiali.
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

## Step 5: fame, compiti, cucina e dispensa

Con fame **strettamente sotto 35/100**, il soldato risulta **in attesa del rancio**:
non cerca cibo, non va alla cucina e continua a muoversi e lavorare normalmente.
Lo stress aumenta di **5 punti per minuto di gioco**, fino a 100, soltanto
per il tempo passato sotto soglia (anche nei frame che attraversano la soglia).
A 35 esatti non e ancora in attesa. Per ora lo stress non cala automaticamente.
L'attesa e indicata sulla scheda, insieme al compito.

### Compiti e dotazione

Selezionare un soldato e aprire il tab **Info**: il menu **Compito** assegna
un unico compito operativo. All'avvio **tutti sono Trasportatori**, incluso
il personaggio aggiuntivo precedentemente riservato alla cucina.

| Compito | Utensile | Lavori |
| --- | --- | --- |
| Trasportatore | Nessuno | Raccoglie le risorse designate e le porta al deposito. |
| Taglialegna | Accetta | Taglia gli alberi designati. |
| Minatore | Piccone | Mina roccia e montagna, aprendo anche gallerie. |
| Scavatore | Pala | Scava le trincee designate. |
| Costruttore | Martello da Costruzione | Costruisce pareti, porte e scale designate. |
| Cuoco | Nessuno | Prepara e distribuisce rancio e rifornisce le borracce alla consegna. |

Quando e selezionato **Trasportatore**, sotto il menu compare il checkbox
**Trasporto Automatico**, inizialmente disattivato e salvato per singolo soldato.
Se attivo, il trasportatore raccoglie gli item presenti nelle celle esplorate,
incluse le categorie diverse da Resource, e li consegna al deposito. Serve un
deposito disponibile. Senza l'opzione, raccoglie soltanto le risorse designate
manualmente come prima.

Al cambio compito, il soldato raggiunge fisicamente una cella libera del
**deposito degli utensili**, restituisce i vecchi tool e prende quello nuovo,
se richiesto. Il prelievo non avviene a distanza. Le borracce e le risorse
personali non vengono restituite. Anche il passaggio a un compito senza tool
richiede il viaggio al deposito; se e gia li, lo scambio avviene sul posto.
Senza utensili disponibili attende al deposito e riprova, senza ricevere altri
job. Piu soldati possono avere lo stesso compito, ma ciascuno deve possedere
il proprio utensile: le scorte iniziali restano limitate.

Un lavoro gia in esecuzione termina prima del cambio dotazione. Un percorso
gia iniziato viene completato senza teletrasporti; se il soldato sta soltanto
ritirando un attrezzo, puo passare al cambio prima di iniziare il job.
Richiedere un altro compito durante il viaggio aggiorna la destinazione
operativa: al deposito viene preparata l'ultima dotazione richiesta.
Il pannello mostra compito attuale, eventuale richiesta e stato dello scambio.

Guardia/Lavoro e separato dal compito: soltanto in Lavoro si eseguono i relativi
job. La dotazione viene cambiata anche per una guardia, che poi resta in Guardia.
Scegliere Cuoco porta invece in Lavoro, come richiesto dalla cucina. Una cucina
ha un solo cuoco operativo; eventuali altri Cuochi restano di riserva. Lasciando
il compito, il cuoco termina la razione in preparazione o gestisce il carico
prima del cambio; scorte e progresso della cucina non vengono azzerati.

I compiti lavorano sulle designazioni del giocatore: nessuno sceglie
automaticamente alberi o aree da scavare. La raccolta automatica del
Trasportatore riguarda soltanto gli item gia a terra. La rigenerazione ripristina
tutti a Trasportatore, con trasporto automatico disattivato, senza utensili
personali e con una borraccia piena.
API: `SoldierData.Task`, `SoldierView.RequestTask(task)`, `RequestedTask`,
`TaskReady` e `TaskStatus`. `SoldierData.Profession` resta un dato iniziale,
non limita l'assegnazione dei compiti.

### Cucina e produzione

La scena genera automaticamente la **CUCINA**, il **DEPOSITO** 4x4 e la
**DISPENSA** 2x4 sotto al deposito. Le celle di deposito e dispensa sono
percorribili e il cuoco raggiunge la dispensa con il normale pathfinding.
La dispensa contiene **1000 Razioni** in stack da 250; il cuoco preleva da qui
10 viveri per ciclo.
Se manca spazio per la cucina o il suo accesso e occupato/bloccato, il sistema
lo segnala; gli accessi vengono ritentati automaticamente ogni mezzo secondo.
La cucina resta presente anche senza Cuochi assegnati; su mappe senza spazio
per la struttura non viene generata.

- La cucina parte con **0 razioni pronte**; le Razioni sono gia presenti nella
  dispensa dalla generazione della mappa.
- Nessuno cucina finche un soldato non assume il compito **Cuoco** e completa
  il passaggio al deposito. Il lavoro viene assegnato al primo Cuoco disponibile.
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
- Rigenerare ricrea squadra e cucina, ripristina i compiti a Trasportatore,
  rimuove le zone designate e azzera scorte,
  razioni, progresso e stress accumulato. Deposito, dispensa e stock iniziale
  vengono ricreati.

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
  35, e torna a **100 fame**; l'eventuale borraccia vuota viene sostituita con
  una piena. La sete si ripristina solo bevendo dalla borraccia; bisogni
  fisiologici e stress non vengono ripristinati. La fame riprende poi a diminuire
  al ritmo abituale.
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
- [x] Assegnazione e cambio dei compiti, con scambio fisico degli utensili al deposito.

## Step 6: Guardia e Lavoro

Ogni personaggio ha uno stato distinto dal compito:

- **Lavoro**: partecipa soltanto ai job consentiti dal compito selezionato.
  Non riceve spostamenti manuali con il destro.
- **Guardia**: non prende job. Selezionarlo con il sinistro e usare il destro
  per raggiungere una cella; rimane in Guardia sulla destinazione, anche se
  vengono designati lavori vicini. Non sono ancora presenti tiro o nemici.

Nella scheda del soldato sono presenti i pulsanti **Guardia** e **Lavoro**.
Il contatore **Lavoro: N | Guardia: N** resta visibile anche senza selezione
o quando si ispeziona un edificio, e conta gli stati effettivi di tutti
i personaggi generati, inclusi gli eventuali Cuochi in Lavoro.
All'avvio normale indica Lavoro: 9 e Guardia: 0.

Se si richiede Guardia durante un job, il soldato resta in Lavoro fino al
completamento del job assegnato, incluso l'eventuale viaggio. La scheda indica
**Lavoro -> Guardia a fine job**; fino ad allora il contatore lo considera
ancora in Lavoro. Non ricevera il job successivo. Quando entra effettivamente
in Guardia, raggiunge la cella accessibile piu vicina del deposito e vi ripone
fisicamente l'utensile del proprio compito; durante il rientro non puo ricevere
altri ordini. Premere Lavoro prima della fine annulla la richiesta. Se il job
viene annullato o rilasciato, la richiesta viene applicata al rilascio; un
percorso gia iniziato termina senza teletrasporti.
Tornando in Lavoro durante una marcia di Guardia, termina il percorso prima di
poter prendere job. In Guardia il destro sostituisce anche percorsi gia in corso.

Chi ha il compito **Cuoco resta in Lavoro** e il suo pulsante Guardia e disabilitato;
puo tornare a un altro compito attraverso il menu. Il compito non cambia
passando tra i due stati. I bisogni e lo stress
continuano ad avanzare in entrambi. Rigenerare ripristina tutti in Lavoro,
azzera le richieste pendenti e aggiorna il contatore.

API: `SoldierView.RequestDuty(SoldierDuty.Guard/Work)`, `Duty`, `GuardRequested`;
`SoldierRoster.WorkCount` e `GuardCount` espongono i totali.
`SoldierMovementOrders.Selected` segue la selezione del pannello;
`IssueMove(cell)` valida e muove esclusivamente quella guardia.

## Step 7: zone designate

I pulsanti **Discarica**, **Materiali** e **Cimitero** attivano
la modalita di designazione. Trascinare con il sinistro dal tile iniziale a
quello finale per creare un rettangolo; il verso del trascinamento non conta.
L'anteprima segue il cursore e, al rilascio, il colore viene applicato ai tile
e resta visibile: grigio per la discarica, rosso per i materiali e grigio scuro
per il cimitero. **Esc** o destro escono dalla modalita.

Si possono aggiungere piu rettangoli dello stesso tipo. Le zone di tipo diverso
non si sovrappongono; celle fuori mappa, bloccate o in trincea non sono valide.
Le zone restano percorribili. Per ora non si possono cancellare e durano solo
fino alla rigenerazione della mappa.

- **Dispensa**: area automatica 2x4 sotto il deposito, con 1000 Razioni in stack
  da 250. Il cuoco raggiunge una sua cella e la usa come origine del prelievo.
- **Discarica**: area disponibile per future consegne di detriti e rifiuti.
- **Deposito materiali**: destinazione disponibile per futuri materiali da
  costruzione e rifornimenti dalle retrovie.
- **Cimitero**: area designata, senza ancora funzioni di sepoltura.

I lavori di bagno, raccolta e smaltimento dei rifiuti/detriti, trasporto dei
materiali e rifornimento dalle retrovie non sono ancora implementati. Le zone
forniscono le destinazioni, non assegnano ancora questi lavori ai soldati.
Rigenerare la mappa elimina le zone e ripristina lo stato iniziale.

## Step 8: costruzioni tramite muri

Nel pannello **COSTRUZIONI**, scegliere **Muro Legno** o **Muro Pietra** e
trascinare dal primo all'ultimo tile. Si possono designare linee ortogonali o
diagonali, anche in piu passaggi; gli estremi possono condividere un angolo.
Un clic singolo designa un muro di un tile. Le linee devono usare tutte lo stesso
materiale e attraversare terreno libero, senza alberi, rocce, trincee, strada,
depositi, zone o costruzioni sovrapposte. Il terreno deve essere esplorato, ma
non deve essere attualmente visibile. Gli item gia presenti sul tile del muro
vengono prima portati al deposito da un Hauler. I placeholder dei muri pianificati
restano visibili sulle celle remembered, ma non vengono mostrati su celle mai
esplorate.

Quando le linee chiudono un perimetro rettangolare di almeno **3 x 3 tile**, il
sistema lo riconosce automaticamente come edificio. I soldati all'interno o sul
perimetro vengono evacuati; completano prima l'eventuale job corrente. I
costruttori lavorano da celle esterne. I muri autonomi che non fanno parte di un
perimetro restano senza tetto.

Dopo aver costruito tutti i lati, scegliere **Porta** e cliccare un tile di muro
gia costruito, non un angolo. Il costruttore sostituisce quel tile con una porta
dello stesso materiale usando un martello e un'unita aggiuntiva di materiale.
Il tetto compare quando la porta e completata; fino ad allora il perimetro non
e considerato un rifugio chiuso. Esc o destro escono dalla modalita Porta.

Se sul tile di una parete designata c'e un oggetto, il job di costruzione resta
in attesa: un Hauler lo raccoglie e lo porta al deposito, poi il Costruttore
puo iniziare. Il materiale della parete viene ritirato dal deposito o da un'altra
cella a terra raggiungibile.

Il **Costruttore in Lavoro** costruisce pareti e porta tramite la coda dei lavori:
richiede un **Martello da Costruzione**, ritirato dal deposito prima dei materiali
se non e gia nell'inventario. Senza martelli disponibili i job aspettano e
riprovano. Il martello non viene consumato e resta al lavoratore dopo il job.
ogni tile costa provvisoriamente **1 Legna** oppure **1 Pietra da costruzione**.
Il Costruttore ritira fisicamente il materiale dal deposito oppure da un'altra
cella a terra raggiungibile, scegliendo la fonte piu vicina. Gli item sul tile
del muro sono invece rimossi prima dalla fase di sgombero. Ogni risorsa a terra
viene prenotata per un solo costruttore; quelle con un ordine Raccogli restano
affidate a quell'ordine. Non vengono aggiunte scorte gratuite. Senza materiale
raggiungibile i job aspettano e riprovano.
Durata per tile: `6 / (1 + Forza / 100)` secondi, esclusi i viaggi; premio 25 XP Forza.

Le pareti finite bloccano il movimento, la porta finita resta attraversabile.
Il completamento aspetta che celle occupate e percorsi gia assegnati siano liberi.
La stanza e **completa solo quando tutto il perimetro, porta inclusa, e costruito**.
Il pannello indica stanze complete/totali, rifugi in galleria, gallerie aperte
e risorse rimaste nel deposito.
Quando la stanza e completa viene generato automaticamente un **tetto visibile**
su tutto il rettangolo, senza costi o lavori aggiuntivi. Passare con il mouse
su pareti, porta o interno nasconde soltanto il tetto di quella costruzione,
per vedere e selezionare i soldati all'interno; uscendo dal rettangolo il tetto
ricompare. Il passaggio del mouse sull'interfaccia non scopre gli edifici
sottostanti. Nascondere il tetto non modifica lo stato interno della stanza
ne la percorribilita. La rigenerazione elimina anche i tetti.

### Stanze specializzate

Dopo aver completato un edificio, usare il menu **Tipo** nella sezione **STANZE**
del pannello destro, premere **Disegna stanza** e trascinare un rettangolo
nell'interno. Sono disponibili **Comando**, **Stazione Telegrafo**, **Cucina**,
**Armeria**, **Ricovero** e **Ospedale**. Il rettangolo deve rimanere interamente
all'interno dello stesso edificio completato e non sovrapporsi ad altre aree;
ridisegnare esattamente la stessa area permette di cambiarne il tipo. **Esc** o
destro escono dalla modalita. Ogni area mostra il nome della specializzazione
in alto a sinistra, anche con il tetto visibile, oltre al colore distintivo.
Per ora la specializzazione e solo descrittiva:
non modifica ancora cucina, soldati, inventario o altre meccaniche.

**Annulla [X]** su un muro ferma il job selezionato; annullare sul perimetro di
un edificio ferma i job ancora in coda, conservando le parti gia costruite e i
materiali trasportati. Per completare un perimetro parziale, selezionare **Porta**
e cliccare un muro gia costruito. Non e prevista demolizione.
Rigenerare la mappa elimina progetti e costruzioni.

### TODO costruzioni

- [ ] Quando verra implementato il **freddo**, l'interno delle costruzioni
  dovra proteggere dalle basse temperature rispetto all'esterno.
- [ ] Implementare **stufe all'interno delle costruzioni** per aumentare il caldo.
- [ ] Assegnare una **durabilita specifica per materiale** a pareti e porte,
  per resistere ai bombardamenti; danni e distruzione non sono ancora implementati.

## Step successivi (non implementati)

### TODO grafica e risoluzione

- [ ] **Passaggio a tile e sprite 32 x 32, predisposto per 64 x 64**:
  introdurre una configurazione condivisa della risoluzione grafica e dei
  Pixels Per Unit (PPU), inizialmente 32, mantenendo ogni cella pari a
  1 unita Unity e invariati mappa, movimento, pathfinding e visuale.
- [ ] Aggiornare asset importati, otto direzioni del soldato e sprite di
  fallback a 32 x 32 con PPU 32, conservando pivot, proporzioni e riferimenti;
  adeguare gli strumenti Editor anche alla migrazione degli asset esistenti.
- [ ] Rendere configurabili tutti i generatori grafici procedurali di terreno,
  scale, soldati, alberi, oggetti, pareti, porte, tetti, montagne e gallerie:
  dimensioni, buffer, indici, coordinate e spessori devono dipendere dalla
  risoluzione scelta, senza vincoli fissi a 16 o 32 pixel. Lasciare invariate
  le texture uniformi 1 x 1 dove non serve dettaglio aggiuntivo.
- [ ] Creare o rifinire la grafica a 32 x 32: il solo ingrandimento delle
  immagini 16 x 16 non aggiunge dettaglio. Conservare il filtro Point e
  verificare impostazioni di importazione, nitidezza allo zoom, allineamento,
  selezione e sovrapposizioni.
- [ ] Adeguare i test grafici alle dimensioni configurate e verificare sia
  32 x 32 / PPU 32 sia 64 x 64 / PPU 64, senza modificare la logica di gioco.
  Il futuro passaggio a 64 dovra richiedere configurazione e asset adeguati,
  non una nuova riscrittura dei generatori.

### TODO zone e logistica

- [ ] Collegare i bisogni fisiologici alla **discarica**: senza una zona valida
  i soldati non possono andare al bagno; implementare raccolta e smaltimento
  di rifiuti e detriti.
- [ ] Collegare raccolta e trasporto di risorse al **deposito materiali**;
  aggiungere anche i rifornimenti in arrivo dalle retrovie.
- [ ] Implementare le funzioni del **cimitero**.

### TODO risorse e rifugi

- [x] **Alberi**: generazione e rendering, taglio tramite job **Taglia** con
  Accetta e drop di **Legna** sulla cella. Non si puo scavare una trincea in una
  cella che contiene un albero.
- [x] **Estrazione della roccia**: il job Mina richiede un Piccone, trasforma
  Rock in terra dopo 10 secondi di lavoro e lascia Pietra da costruzione a terra.
- [x] **Montagna e gallerie**: terreno piu scuro, non percorribile finche non
  viene minato; lo scavo produce Pietra da costruzione e apre gallerie e camere
  percorribili, riconosciute come interni sotto la montagna.
- [x] **Raccolta risorse**: cliccare un item a terra e assegnare un job che lo
  porta al deposito. Legna e Pietra da costruzione sono registrate nello stockpile.
- [x] Usare legna e pietra come materiali per le costruzioni.
- [x] Automatizzare il ritiro delle risorse a terra necessarie ai cantieri.
- [x] **Rifugi rettangolari con costruzione a celle, in stile RimWorld**: designare pareti
  e porte, richiedere materiali e assegnare i lavori di costruzione ai soldati.
- [x] **Tetti automatici**: quando le pareti e le porte costruite chiudono
  un perimetro, riconoscere lo spazio interno e generare automaticamente
  il tetto del rifugio; nasconderlo al passaggio del mouse sulla costruzione.

### TODO stati e inventario

- [x] **Salute e morte**: salute iniziale 100, morte a zero e rilascio degli incarichi.
- [x] **Danni da riserve esaurite**: fame -5 e sete -30 punti salute per giorno;
  effetti cumulativi, interrotti da cibo e acqua senza guarigione.
- [ ] **Termico**: sostituire il valore statico con variazioni ambientali e
  gestione di caldo/freddo.
- [ ] **Stress**: aggiungere recupero e gestione degli effetti; al momento cresce
  soltanto quando la fame scende sotto 35.
- [ ] **Paura**: definire cause, variazione e gestione del valore; parte da zero.
- [x] **Stanchezza da attivita**: parte da zero, +1 per tile raggiunto e +5 per job completato.
- [ ] **Riposo e recupero della stanchezza**: aggiungere sonno ed effetti sull'attivita.
- [x] **Peso degli item e carico**: ogni item ha un peso unitario; il pannello
  mostra il carico corrente rispetto al massimo, determinato dalla Forza. Scavi,
  materiali, utensili, risorse e viveri/rancio in transito non vengono ritirati
  se superano il limite.
- [ ] **Inventario**: estendere raccolta e assegnazione a cibo e armi.

Pesi unitari attuali: Pala 1,8 kg; Accetta 1,2 kg; Piccone 2,4 kg; Razioni
0,5 kg; Legna 1 kg; Pietra da costruzione 2,5 kg; Borraccia piena 1,5 kg,
vuota 0,5 kg; Martello da Costruzione 1,5 kg.

Priorita modificabili dei lavori, costruzioni e rifornimenti, soddisfacimento
dei bisogni, evoluzione degli altri stati, copertura e combattimento.
Anche con riserve a zero i soldati possono ancora muoversi e lavorare finche
sono vivi; fame e sete esaurite riducono la salute fino alla morte.