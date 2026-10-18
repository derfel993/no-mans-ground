# Item di No Man's Ground

Catalogo degli item attualmente definiti nel gioco. Le funzionalita future
restano nei TODO del [README.md](README.md).

Aggiornare questo catalogo ogni volta che viene richiesto un nuovo item,
senza attendere una richiesta separata. Distinguere sempre gli item previsti
da quelli implementati e mantenere aggiornati categoria, stack e utilizzo.

## Item Implementati

Lo stack massimo indica quante unita possono appartenere allo stesso stack,
non il limite totale dell'inventario o del deposito.

| Item | Identificatore | Categoria | Stack massimo | Utilizzo |
| --- | --- | --- | --- | --- |
| Pala | `Shovel` | Tool | 1 | Necessaria per scavare trincee. Il lavoratore la ritira dal deposito se non la possiede; non viene consumata. |
| Accetta | `Axe` | Tool | 1 | Necessaria per tagliare gli alberi e ottenere Legna. Non viene consumata. |
| Piccone | `Pickaxe` | Tool | 1 | Necessario per minare roccia e montagna e ottenere Pietra da costruzione. Non viene consumato. |
| Razioni | `Rations` | Food | 250 | Viveri conservati nella dispensa: il cuoco ne utilizza 10 per preparare una razione di rancio. |
| Legna | `Wood` | Resource | 250 | Ottenuta tagliando alberi. Materiale per edifici in legno e porte delle gallerie; viene consumata dai relativi lavori. |
| Pietra da costruzione | `BuildingStone` | Resource | 250 | Ottenuta minando roccia o montagna. Materiale consumato per edifici in pietra. |
| Borraccia piena (acqua) | `FullCanteen` | Equipment | 1 | Ogni soldato, incluso il cuoco, ne riceve una alla generazione. Sotto il 20% di sete viene usata automaticamente: sete a 100% e sostituzione con una borraccia vuota. |
| Borraccia vuota | `EmptyCanteen` | Equipment | 1 | Resta nell'inventario dopo la bevuta. Alla consegna effettiva del rancio viene sostituita con una borraccia piena; lo stesso vale per il cuoco quando mangia. |

## Disponibilita Iniziale

- Deposito: 6 Pale, 2 Accette e 2 Picconi.
- Dispensa: 1000 Razioni, in quattro stack da 250.
- Ogni personaggio generato: 1 Borraccia piena (acqua).
- Legna e Pietra da costruzione: ottenute dai lavori, senza scorte iniziali gratuite.
- Borraccia vuota: prodotta dalla bevuta, non assegnata alla generazione.

## Previsto E Da Definire

- **Weapon**: categoria gia definita, senza ancora tipi di armi specifici nel catalogo.
- **Acqua separata dalla borraccia**: non esiste ancora come item autonomo;
  fonti, scorte e approvvigionamento restano da implementare.
- **Rancio pronto**: esiste come conteggio della cucina e del carico del cuoco,
  non come tipo distinto di item nell'inventario.
- **Stufe**: previste come strutture per riscaldare i rifugi, non ancora definite
  come item. Anche peso degli oggetti e consumo del limite di carico restano futuri.

Pareti, porte, scale, alberi e terreni (inclusi Montagna e Galleria) non sono
item d'inventario: sono strutture, elementi della mappa o tipi di terreno.

## Riferimento Tecnico

Definizioni e categorie sono in
[No Man's Ground/Assets/Scripts/Map/MapItems.cs](No%20Man's%20Ground/Assets/Scripts/Map/MapItems.cs).