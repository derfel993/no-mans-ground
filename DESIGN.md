# Game Design

## Visione

Gestionale single player ambientato nelle trincee della Prima Guerra Mondiale. Il giocatore comanda il proprio esercito; l'avversario e controllato dall'AI. Le missioni condividono le stesse regole di mappa, soldati, trincee, visibilita, armi e rifornimenti, ma hanno obiettivi diversi.

## Modalita di gioco

### Campagna

- Il giocatore sceglie il tipo di campagna.
- Ogni campagna contiene una sequenza di missioni di Attacco e di Difesa.
- Completare una missione sblocca le missioni successive previste dalla campagna.

### Battaglia Storica

- Il giocatore sceglie una campagna o uno scenario storico e la fazione da comandare.
- Le missioni storiche possono essere di Attacco o di Difesa e ripercorrono campagne ed eventi storici.

### Battaglia Libera

- Partita 1 contro 1 tra giocatore e AI.
- La partita inizia su una mappa non costruita.
- Entrambi gli schieramenti possono organizzare e costruire la propria posizione prima e durante la battaglia.

## Modalita di missione

### Difesa

- Il giocatore organizza la propria trincea e prepara i soldati a difenderla.
- L'AI avversaria pianifica e conduce gli attacchi.
- Vittoria: resistere all'attacco e mantenere la posizione fino al termine della missione.
- Sconfitta: l'avversario conquista l'obiettivo difensivo.

### Attacco

- Il giocatore guida una missione per espugnare la trincea nemica.
- L'AI organizza la guarnigione e dirige la difesa.
- Vittoria: conquistare l'obiettivo nemico e mantenerne il controllo.
- Sconfitta: la missione termina senza aver conquistato l'obiettivo.

Il controllo degli obiettivi deve dipendere dalla presenza e dal controllo dell'area, non dal semplice passaggio di un soldato su una cella.

## AI Avversaria

L'AI controlla l'esercito opposto al giocatore e opera su due livelli:

- **Comando**: sceglie quando costruire, attaccare o rinforzare, quale settore prendere di mira e quali forze impiegare.
- **Squadre**: eseguono ordini circoscritti, come preparare la posizione, avanzare, raggiungere un settore, ingaggiare nemici visibili, difendere o ripiegare.

L'AI non deve conoscere informazioni nascoste al giocatore: usa la visibilita e le informazioni ottenute dalle proprie unita. Gli attacchi devono essere leggibili e lasciare al giocatore il tempo di reagire.

## Primo Prototipo

Per contenere il perimetro iniziale:

- una missione di Difesa e una di Attacco;
- un solo obiettivo principale per missione;
- un attacco nemico composto da una o poche squadre;
- niente campagna strategica, economia nemica completa o pianificazione su larga scala;
- priorita alla preparazione della posizione, all'avanzata, al combattimento e al controllo dell'obiettivo.

## Decisioni Da Definire

- Durata della missione di Difesa e momento dell'arrivo dei rinforzi.
- Condizioni precise di sconfitta per entrambe le modalita.
- Tempo necessario per conquistare e mantenere un obiettivo.
- Numero e composizione delle squadre, riserve e ritmo degli attacchi.
- Eventuali obiettivi secondari e progressione fra missioni.
- Tipi di campagna, struttura delle campagne storiche e fazioni selezionabili per ciascuno scenario.
- Regole e limiti di costruzione per entrambi gli schieramenti in Battaglia Libera.
- Configurazione iniziale della Battaglia Libera: mappa, risorse, tempi di preparazione e condizioni di vittoria.

## Stato

Queste regole descrivono il design previsto, non tutte le funzioni gia implementate. Al momento il progetto contiene sistemi di costruzione, trincee, fog of war, armi e tiro di prova; modalita di gioco, campagne, fazioni giocabili, nemici, missioni, obiettivi e condizioni di vittoria non sono ancora implementati.
