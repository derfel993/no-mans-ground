using System;
using System.Collections.Generic;
using NoMansGround.Map;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace NoMansGround.Soldiers
{
    public enum DiggingMode { None, Dig, Cancel, Ladder, Cut, Mine, Collect }
    public enum TrenchJobKind { Dig, Ladder, CutTree, MineRock, CollectItem }

    public sealed class TrenchJob
    {
        public Vector2Int Cell { get; }
        public TrenchJobKind Kind { get; }
        public SoldierView Worker { get; internal set; }
        public float Progress { get; internal set; }
        public string Status { get; internal set; } = "In attesa";
        public ItemType? PickingTool { get; internal set; }
        public ItemType? CollectedItem { get; internal set; }
        internal SpriteRenderer Marker;

        internal TrenchJob(Vector2Int cell, TrenchJobKind kind = TrenchJobKind.Dig)
        {
            Cell = cell;
            Kind = kind;
        }
    }

    [DefaultExecutionOrder(-100)]
    public sealed class TrenchWorkOrders : MonoBehaviour
    {
        private static readonly Vector2Int[] Neighbours =
            { Vector2Int.down, Vector2Int.left, Vector2Int.right, Vector2Int.up };

        [SerializeField] private MapPrototype map;
        [SerializeField] private MapTilemapView mapView;
        [SerializeField] private SoldierRoster roster;
        [SerializeField] private Camera mapCamera;
        [SerializeField, Min(0.1f)] private float baseDigSeconds = 6f;
        [SerializeField, Min(0.1f)] private float baseMineSeconds = 10f;
        [SerializeField, Min(0)] private int strengthExperience = 25;
        [SerializeField, Min(0.1f)] private float baseLadderSeconds = 4f;

        private readonly List<TrenchJob> jobs = new List<TrenchJob>();
        private IReadOnlyList<TrenchJob> readOnlyJobs;
        private readonly List<SpriteRenderer> preview = new List<SpriteRenderer>();
        private bool dragging;
        private Vector2Int dragStart;
        private float retryTimer;
        private int consumedFrame = -1;

        public IReadOnlyList<TrenchJob> Jobs => readOnlyJobs ?? (readOnlyJobs = jobs.AsReadOnly());
        public DiggingMode Mode { get; private set; }
        public bool BlocksWorldCommands => isActiveAndEnabled
            && (Mode != DiggingMode.None || consumedFrame == Time.frameCount);
        public string Status { get; private set; } = "T: scava | L: scala\nX: annulla | Esc: esci";
        public event Action Changed;

        public void Configure(MapPrototype prototype, MapTilemapView view, SoldierRoster soldiers, Camera camera)
        {
            map = prototype;
            mapView = view;
            roster = soldiers;
            mapCamera = camera;
        }

        private void Awake()
        {
            if (map == null || mapView == null || roster == null || mapCamera == null
                || !float.IsFinite(baseDigSeconds) || baseDigSeconds <= 0f || strengthExperience < 0
                || !float.IsFinite(baseLadderSeconds) || baseLadderSeconds <= 0f
                || !float.IsFinite(baseMineSeconds) || baseMineSeconds <= 0f)
            {
                Debug.LogError("TrenchWorkOrders requires map, view, roster, camera and valid work settings.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (map != null)
                map.Generated += HandleMapGenerated;
            if (roster != null)
                roster.Rebuilt += ResetJobs;
            Changed?.Invoke();
        }

        private void OnDisable()
        {
            if (map != null)
                map.Generated -= HandleMapGenerated;
            if (roster != null)
                roster.Rebuilt -= ResetJobs;
            ResetJobs();
        }

        public void SetMode(DiggingMode mode)
        {
            if (mode != DiggingMode.None && !isActiveAndEnabled)
            {
                Notify("Sistema scavi disabilitato.");
                return;
            }
            Mode = mode;
            dragging = false;
            ClearPreview();
            Notify(mode == DiggingMode.Dig ? "SCAVA: trascina una linea.\nEsc o destro: esci."
                : mode == DiggingMode.Ladder ? "SCALA: clic su una trincea.\nEsc o destro: esci."
                : mode == DiggingMode.Cut ? "TAGLIA: trascina sugli alberi.\nEsc o destro: esci."
                : mode == DiggingMode.Mine ? "MINA: trascina sulle rocce.\nEsc o destro: esci."
                : mode == DiggingMode.Collect ? "RACCOGLI: clicca una risorsa.\nEsc o destro: esci."
                : mode == DiggingMode.Cancel ? "ANNULLA: trascina sugli scavi.\nEsc o destro: esci."
                : "T: scava | A: taglia | M: mina | C: raccogli | L: scala\nX: annulla | Esc: esci");
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        private void LateUpdate()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.tKey.wasPressedThisFrame)
                    SetMode(Mode == DiggingMode.Dig ? DiggingMode.None : DiggingMode.Dig);
                if (keyboard.aKey.wasPressedThisFrame)
                    SetMode(Mode == DiggingMode.Cut ? DiggingMode.None : DiggingMode.Cut);
                if (keyboard.mKey.wasPressedThisFrame)
                    SetMode(Mode == DiggingMode.Mine ? DiggingMode.None : DiggingMode.Mine);
                if (keyboard.cKey.wasPressedThisFrame)
                    SetMode(Mode == DiggingMode.Collect ? DiggingMode.None : DiggingMode.Collect);
                if (keyboard.xKey.wasPressedThisFrame)
                    SetMode(Mode == DiggingMode.Cancel ? DiggingMode.None : DiggingMode.Cancel);
                if (keyboard.lKey.wasPressedThisFrame)
                    SetMode(Mode == DiggingMode.Ladder ? DiggingMode.None : DiggingMode.Ladder);
                if (keyboard.escapeKey.wasPressedThisFrame)
                {
                    if (Mode != DiggingMode.None)
                        consumedFrame = Time.frameCount;
                    SetMode(DiggingMode.None);
                }
            }
            if (Mode == DiggingMode.None)
                return;
            consumedFrame = Time.frameCount;
            Mouse mouse = Mouse.current;
            if (mouse == null)
                return;
            if (mouse.rightButton.wasPressedThisFrame)
            {
                SetMode(DiggingMode.None);
                return;
            }
            bool valid = TryPointerCell(mouse.position.ReadValue(), out Vector2Int cell);
            if (Mode == DiggingMode.Ladder)
            {
                if (valid) ShowPreview(new[] { cell });
                else ClearPreview();
                if (valid && mouse.leftButton.wasPressedThisFrame)
                    DesignateLadder(cell);
                return;
            }
            if (Mode == DiggingMode.Collect)
            {
                if (mouse.leftButton.wasPressedThisFrame && valid)
                    DesignateCollection(cell);
                return;
            }
            if (mouse.leftButton.wasPressedThisFrame && valid)
            {
                dragging = true;
                dragStart = cell;
            }
            if (dragging && valid)
                ShowPreview(RasterLine(dragStart, cell));
            if (dragging && mouse.leftButton.wasReleasedThisFrame)
            {
                dragging = false;
                ClearPreview();
                if (!valid)
                    return;
                int changed = 0;
                foreach (Vector2Int target in RasterLine(dragStart, cell))
                    if (Mode == DiggingMode.Dig ? Designate(target)
                        : Mode == DiggingMode.Cut ? DesignateTree(target)
                        : Mode == DiggingMode.Mine ? DesignateMine(target) : Cancel(target))
                        changed++;
                string action = Mode == DiggingMode.Dig ? "designate"
                    : Mode == DiggingMode.Cut ? "alberi designati"
                    : Mode == DiggingMode.Mine ? "rocce designate" : "annullate";
                Notify(changed > 0 ? $"{changed} celle {action}."
                    : "Nessuna cella valida: lavoro gia presente,\nbloccato o fuori mappa.", changed == 0);
            }
        }

        private bool TryPointerCell(Vector2 pointer, out Vector2Int cell)
        {
            cell = default;
            if (map.Data == null || !mapCamera.pixelRect.Contains(pointer)
                || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
                return false;
            Vector3 world = mapCamera.ScreenToWorldPoint(new Vector3(pointer.x, pointer.y,
                mapView.transform.position.z - mapCamera.transform.position.z));
            Vector3Int position = mapView.Tilemap.WorldToCell(world);
            cell = new Vector2Int(position.x, position.y);
            return map.Data.Contains(cell);
        }

        public static IReadOnlyList<Vector2Int> RasterLine(Vector2Int from, Vector2Int to)
        {
            var result = new List<Vector2Int>();
            int dx = Mathf.Abs(to.x - from.x);
            int dy = -Mathf.Abs(to.y - from.y);
            int sx = from.x < to.x ? 1 : -1;
            int sy = from.y < to.y ? 1 : -1;
            int error = dx + dy;
            while (true)
            {
                result.Add(from);
                if (from == to)
                    return result;
                int twice = 2 * error;
                if (twice >= dy) { error += dy; from.x += sx; }
                if (twice <= dx) { error += dx; from.y += sy; }
            }
        }

        public bool Designate(Vector2Int cell)
        {
            if (!isActiveAndEnabled || map.Data == null || !map.Data.IsWalkable(cell)
                || map.Data[cell.x, cell.y] == TerrainKind.Trench || map.Data.HasTree(cell)
                || map.Data.TryGetZone(cell, out _)
                || map.Data.IsStorageCell(cell) || map.Data.IsPantryCell(cell)
                || jobs.Exists(job => job.Cell == cell))
            {
                Notify("Scavo non valido: fuori mappa,\nbloccato o gia presente.");
                return false;
            }
            var job = new TrenchJob(cell);
            job.Marker = CreateMarker(cell, new Color(1f, 0.75f, 0.15f, 0.55f), "Dig designation");
            jobs.Add(job);
            retryTimer = 0f;
            Changed?.Invoke();
            return true;
        }

        public bool DesignateTree(Vector2Int cell)
        {
            if (!isActiveAndEnabled || map.Data == null || !map.Data.HasTree(cell)
                || jobs.Exists(job => job.Cell == cell))
            {
                Notify("Taglio non valido: serve un albero senza job gia presente.");
                return false;
            }
            var job = new TrenchJob(cell, TrenchJobKind.CutTree);
            job.Marker = CreateMarker(cell, new Color(0.76f, 0.49f, 0.2f, 0.7f), "Tree cut designation");
            jobs.Add(job);
            retryTimer = 0f;
            Changed?.Invoke();
            return true;
        }

        public bool DesignateMine(Vector2Int cell)
        {
            if (!isActiveAndEnabled || map.Data == null || !map.Data.Contains(cell)
                || map.Data[cell.x, cell.y] != TerrainKind.Rock || jobs.Exists(job => job.Cell == cell))
            {
                Notify("Mina non valida: serve una roccia senza job gia presente.");
                return false;
            }
            var job = new TrenchJob(cell, TrenchJobKind.MineRock);
            job.Marker = CreateMarker(cell, new Color(0.66f, 0.68f, 0.65f, 0.75f), "Mine designation");
            jobs.Add(job);
            retryTimer = 0f;
            Changed?.Invoke();
            return true;
        }

        public bool DesignateCollection(Vector2Int cell)
        {
            if (!isActiveAndEnabled || map.Data == null || !map.Data.TryGetGroundItem(cell, out ItemType item)
                || ItemCatalog.Get(item).Category != ItemCategory.Resource || jobs.Exists(job => job.Cell == cell))
            {
                Notify("Raccolta non valida: clicca una risorsa senza un job gia presente.");
                return false;
            }
            if (!map.Data.HasStorageArea)
            {
                Notify("Nessun deposito disponibile per la risorsa.");
                return false;
            }
            var job = new TrenchJob(cell, TrenchJobKind.CollectItem);
            job.Marker = CreateMarker(cell, new Color(0.3f, 0.8f, 1f, 0.72f), "Collect designation");
            jobs.Add(job);
            retryTimer = 0f;
            Changed?.Invoke();
            return true;
        }

        public bool Cancel(Vector2Int cell)
        {
            int index = jobs.FindIndex(job => job.Cell == cell);
            if (index < 0)
            {
                Notify("Nessuno scavo da annullare.");
                return false;
            }
            Release(jobs[index]);
            jobs.RemoveAt(index);
            retryTimer = 0f;
            Changed?.Invoke();
            return true;
        }

        public bool DesignateLadder(Vector2Int cell)
        {
            if (!isActiveAndEnabled || map.Data == null || !map.Data.IsTrench(cell)
                || !map.Data.IsWalkable(cell) || map.Data.HasLadder(cell) || jobs.Exists(job => job.Cell == cell))
            {
                Notify("Scala non valida: serve una trincea\nsenza scala o lavoro gia presente.");
                return false;
            }
            var job = new TrenchJob(cell, TrenchJobKind.Ladder);
            job.Marker = CreateMarker(cell, new Color(0.3f, 0.8f, 1f, 0.65f), "Ladder designation");
            jobs.Add(job);
            retryTimer = 0f;
            Notify("Installazione scala in coda.");
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (!float.IsFinite(deltaTime) || deltaTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            if (map.Data == null || deltaTime == 0f)
                return;
            bool changed = false;
            for (int i = jobs.Count - 1; i >= 0; i--)
            {
                TrenchJob job = jobs[i];
                if (job.Worker == null)
                    continue;
                SoldierView worker = job.Worker;
                if (!worker.isActiveAndEnabled)
                {
                    worker.SetWorkAssignment(false);
                    job.Worker = null;
                    job.PickingTool = null;
                    job.Status = "In attesa";
                    changed = true;
                    continue;
                }
                if (worker.IsMoving)
                    continue;
                if (job.Kind == TrenchJobKind.CollectItem)
                {
                    if (!job.CollectedItem.HasValue)
                    {
                        if (!map.Data.TryTakeGroundItem(job.Cell, out ItemType item)
                            || ItemCatalog.Get(item).Category != ItemCategory.Resource)
                        {
                            Release(job);
                            jobs.RemoveAt(i);
                            Notify("La risorsa da raccogliere non e piu presente.");
                            changed = true;
                            continue;
                        }
                        worker.Data.Inventory.Add(item, 1);
                        job.CollectedItem = item;
                        mapView.RefreshGroundItems(map.Data);
                        Notify($"{ItemCatalog.Get(item).DisplayName} raccolta; in consegna al deposito.");
                    }

                    if (map.Data.IsStorageCell(worker.Cell))
                    {
                        ItemType item = job.CollectedItem.Value;
                        if (!worker.Data.Inventory.TryTake(item, 1))
                        {
                            Release(job);
                            jobs.RemoveAt(i);
                            Notify("Item non trovato nell'inventario del soldato.", true);
                            changed = true;
                            continue;
                        }
                        map.Data.StorageStockpile.Add(item, 1);
                        Release(job);
                        jobs.RemoveAt(i);
                        Notify($"{ItemCatalog.Get(item).DisplayName} consegnata al deposito.");
                        changed = true;
                        continue;
                    }

                    GridPath storagePath = FindStoragePath(worker);
                    if (storagePath == null)
                    {
                        job.Status = "Deposito non raggiungibile";
                        worker.SetWorkAssignment(false);
                        job.Worker = null;
                        changed = true;
                        continue;
                    }
                    if (storagePath.Cells.Count > 1)
                        worker.BeginMove(mapView, storagePath.Cells);
                    job.Status = worker.IsMoving ? "In consegna al deposito" : "Consegna al deposito";
                    changed = true;
                    continue;
                }
                if (job.PickingTool.HasValue)
                {
                    ItemType tool = job.PickingTool.Value;
                    if (!map.Data.StorageStockpile.TryTake(tool, 1))
                    {
                        worker.SetWorkAssignment(false);
                        job.Worker = null;
                        job.PickingTool = null;
                        job.Status = $"Nessun {ItemCatalog.Get(tool).DisplayName.ToLowerInvariant()} disponibile";
                        changed = true;
                        continue;
                    }
                    worker.Data.Inventory.Add(tool, 1);
                    worker.SetWorkAssignment(false);
                    job.Worker = null;
                    job.PickingTool = null;
                    job.Status = $"{ItemCatalog.Get(tool).DisplayName} raccolta";
                    Notify($"{ItemCatalog.Get(tool).DisplayName} raccolta; assegnazione del lavoro.");
                    retryTimer = 0f;
                    changed = true;
                    continue;
                }
                if (Mathf.Abs(worker.Cell.x - job.Cell.x) + Mathf.Abs(worker.Cell.y - job.Cell.y) != 1)
                    throw new InvalidOperationException("A trench worker must stand next to the designated cell.");
                job.Status = job.Kind == TrenchJobKind.Dig ? "Scavo"
                    : job.Kind == TrenchJobKind.CutTree ? "Taglio albero"
                    : job.Kind == TrenchJobKind.MineRock ? "Mina roccia" : "Installazione scala";
                float multiplier = 1f + worker.Data.GetSkill(SoldierSkill.Strength).Value / 100f;
                float duration = job.Kind == TrenchJobKind.MineRock ? baseMineSeconds
                    : job.Kind == TrenchJobKind.Ladder ? baseLadderSeconds : baseDigSeconds;
                float workRate = job.Kind == TrenchJobKind.MineRock ? 1f : multiplier;
                job.Progress = Mathf.Min(1f, job.Progress + deltaTime * workRate / duration);
                worker.SetWorkAssignment(true, job.Progress, job.Kind);
                job.Marker.color = Color.Lerp(new Color(1f, 0.75f, 0.15f, 0.65f),
                    new Color(0.3f, 0.9f, 0.4f, 0.85f), job.Progress);
                changed = true;
                if (job.Progress < 1f)
                    continue;
                if (job.Kind == TrenchJobKind.Dig || job.Kind == TrenchJobKind.CutTree)
                {
                    if (!CanExcavate(job.Cell))
                    {
                        job.Status = "Attesa: cella o percorso occupato";
                        continue;
                    }
                    if (job.Kind == TrenchJobKind.Dig)
                        mapView.CompleteTrench(map.Data, job.Cell);
                    else
                        mapView.CompleteTreeCut(map.Data, job.Cell);
                }
                else if (job.Kind == TrenchJobKind.MineRock)
                    mapView.CompleteRockMine(map.Data, job.Cell);
                else
                    mapView.CompleteLadder(map.Data, job.Cell);
                worker.Data.GetSkill(SoldierSkill.Strength).AddExperience(strengthExperience);
                Release(job);
                jobs.RemoveAt(i);
            }

            retryTimer -= deltaTime;
            if (retryTimer <= 0f)
            {
                foreach (TrenchJob job in jobs)
                    if (job.Worker == null)
                        changed |= Assign(job);
                retryTimer = 0.5f;
            }
            if (changed)
                Changed?.Invoke();
        }

        private bool Assign(TrenchJob job)
        {
            var available = new List<SoldierView>();
            foreach (SoldierView soldier in roster.Soldiers)
                if (soldier != null && soldier.CanTakeEngineeringWork)
                    available.Add(soldier);
            if (available.Count == 0)
            {
                bool changed = job.Status != "Nessun soldato libero";
                job.Status = "Nessun soldato libero";
                return changed;
            }

            if (job.Kind == TrenchJobKind.CollectItem)
                return AssignCollection(job, available);

            ItemType? requiredTool = job.Kind == TrenchJobKind.Dig ? ItemType.Shovel
                : job.Kind == TrenchJobKind.CutTree ? ItemType.Axe
                : job.Kind == TrenchJobKind.MineRock ? ItemType.Pickaxe : null;
            if (requiredTool.HasValue)
            {
                available.RemoveAll(soldier => soldier.Data.Inventory.Count(requiredTool.Value) == 0);
                if (available.Count == 0)
                    return AssignToolPickup(job, requiredTool.Value);
            }

            var starts = new List<Vector2Int>();
            foreach (SoldierView soldier in available)
                starts.Add(soldier.Cell);
            GridPath best = null;
            foreach (Vector2Int offset in Neighbours)
            {
                Vector2Int workCell = job.Cell + offset;
                if (!map.Data.IsWalkable(workCell)
                    || jobs.Exists(other => other.Kind == TrenchJobKind.Dig && other.Cell == workCell))
                    continue;
                GridPath candidate = GridPathfinder.FindNearest(map.Data, starts, workCell);
                if (candidate == null || (best != null && candidate.Cost >= best.Cost))
                    continue;
                SoldierView worker = available[candidate.StartIndex];
                bool reserved = false;
                foreach (SoldierView other in roster.Soldiers)
                    if (other != null && other != worker && other.Destination == workCell)
                        reserved = true;
                if (!reserved)
                    best = candidate;
            }
            if (best == null)
            {
                bool changed = job.Status != "Accesso bloccato o occupato";
                job.Status = "Accesso bloccato o occupato";
                return changed;
            }
            job.Worker = available[best.StartIndex];
            job.Worker.SetWorkAssignment(true, job.Progress, job.Kind);
            if (best.Cells.Count > 1)
                job.Worker.BeginMove(mapView, best.Cells);
            job.Status = job.Worker.IsMoving ? "In arrivo"
                : job.Kind == TrenchJobKind.Dig ? "Scavo"
                : job.Kind == TrenchJobKind.CutTree ? "Taglio albero"
                : job.Kind == TrenchJobKind.MineRock ? "Mina roccia" : "Installazione scala";
            return true;
        }

        private bool AssignCollection(TrenchJob job, List<SoldierView> available)
        {
            if (!job.CollectedItem.HasValue && !map.Data.TryGetGroundItem(job.Cell, out _))
            {
                const string unavailable = "Risorsa non disponibile";
                bool changed = job.Status != unavailable;
                job.Status = unavailable;
                return changed;
            }

            GridPath best = null;
            SoldierView bestWorker = null;
            foreach (SoldierView worker in available)
            {
                if (job.CollectedItem.HasValue && worker.Data.Inventory.Count(job.CollectedItem.Value) == 0)
                    continue;
                IEnumerable<Vector2Int> destinations = job.CollectedItem.HasValue
                    ? StorageCells() : new[] { job.Cell };
                foreach (Vector2Int destination in destinations)
                {
                    bool reserved = false;
                    foreach (SoldierView other in roster.Soldiers)
                        if (other != null && other != worker
                            && (other.Cell == destination || other.Destination == destination))
                            reserved = true;
                    if (reserved)
                        continue;
                    GridPath candidate = GridPathfinder.FindNearest(map.Data,
                        new[] { worker.Cell }, destination);
                    if (candidate == null || (best != null && candidate.Cost >= best.Cost))
                        continue;
                    best = candidate;
                    bestWorker = worker;
                }
            }
            if (best == null)
            {
                const string blocked = "Accesso alla risorsa o al deposito bloccato";
                bool changed = job.Status != blocked;
                job.Status = blocked;
                return changed;
            }

            job.Worker = bestWorker;
            bestWorker.SetWorkAssignment(true, job.Progress, job.Kind);
            if (best.Cells.Count > 1)
                bestWorker.BeginMove(mapView, best.Cells);
            job.Status = job.CollectedItem.HasValue
                ? bestWorker.IsMoving ? "In consegna al deposito" : "Consegna al deposito"
                : bestWorker.IsMoving ? "In marcia verso la risorsa" : "Raccolta risorsa";
            return true;
        }

        private IEnumerable<Vector2Int> StorageCells()
        {
            if (!map.Data.HasStorageArea)
                yield break;
            for (int y = map.Data.StorageOrigin.y; y < map.Data.StorageOrigin.y + map.Data.StorageHeight; y++)
                for (int x = map.Data.StorageOrigin.x; x < map.Data.StorageOrigin.x + map.Data.StorageWidth; x++)
                    yield return new Vector2Int(x, y);
        }

        private GridPath FindStoragePath(SoldierView worker)
        {
            GridPath best = null;
            foreach (Vector2Int cell in StorageCells())
            {
                bool reserved = false;
                foreach (SoldierView other in roster.Soldiers)
                    if (other != null && other != worker
                        && (other.Cell == cell || other.Destination == cell))
                        reserved = true;
                if (reserved)
                    continue;
                GridPath candidate = GridPathfinder.FindNearest(map.Data, new[] { worker.Cell }, cell);
                if (candidate != null && (best == null || candidate.Cost < best.Cost))
                    best = candidate;
            }
            return best;
        }

        private bool AssignToolPickup(TrenchJob job, ItemType tool)
        {
            int reservedTools = 0;
            foreach (TrenchJob other in jobs)
                if (other.PickingTool == tool)
                    reservedTools++;
            if (map.Data.StorageStockpile.Count(tool) <= reservedTools)
            {
                string noTool = tool == ItemType.Axe ? "Nessuna accetta disponibile" : "Nessuna pala disponibile";
                bool changed = job.Status != noTool;
                job.Status = noTool;
                if (changed)
                    Notify($"{noTool} nel deposito; il lavoro resta in attesa.");
                return changed;
            }

            GridPath best = null;
            SoldierView bestWorker = null;
            foreach (SoldierView worker in roster.Soldiers)
            {
                if (worker == null || !worker.CanTakeEngineeringWork
                    || worker.Data.Inventory.Count(tool) > 0)
                    continue;
                for (int y = map.Data.StorageOrigin.y; y < map.Data.StorageOrigin.y + map.Data.StorageHeight; y++)
                    for (int x = map.Data.StorageOrigin.x; x < map.Data.StorageOrigin.x + map.Data.StorageWidth; x++)
                    {
                        var pickupCell = new Vector2Int(x, y);
                        if (!map.Data.IsWalkable(pickupCell))
                            continue;
                        bool reserved = false;
                        foreach (SoldierView other in roster.Soldiers)
                            if (other != null && other != worker
                                && (other.Cell == pickupCell || other.Destination == pickupCell))
                                reserved = true;
                        if (reserved)
                            continue;
                        GridPath candidate = GridPathfinder.FindNearest(map.Data,
                            new[] { worker.Cell }, pickupCell);
                        if (candidate == null || (best != null && candidate.Cost >= best.Cost))
                            continue;
                        best = candidate;
                        bestWorker = worker;
                    }
            }
            if (best == null)
            {
                const string blocked = "Accesso al deposito bloccato o occupato";
                bool changed = job.Status != blocked;
                job.Status = blocked;
                if (changed)
                    Notify("Accesso al deposito bloccato o occupato.");
                return changed;
            }

            job.Worker = bestWorker;
            job.PickingTool = tool;
            bestWorker.SetWorkAssignment(true, job.Progress, job.Kind);
            if (best.Cells.Count > 1)
                bestWorker.BeginMove(mapView, best.Cells);
            string itemName = ItemCatalog.Get(tool).DisplayName.ToLowerInvariant();
            job.Status = bestWorker.IsMoving ? $"In marcia per {itemName}" : $"Ritiro {itemName}";
            Notify(bestWorker.IsMoving ? $"Un lavoratore sta andando al deposito per prendere {itemName}."
                : $"Ritiro {itemName} dal deposito.");
            return true;
        }

        private bool CanExcavate(Vector2Int cell)
        {
            // Completing a trench removes ground-level edges: keep existing routes valid until they clear it.
            foreach (SoldierView soldier in roster.Soldiers)
            {
                if (soldier == null)
                    continue;
                if (soldier.Cell == cell || soldier.Destination == cell)
                    return false;
                if (!soldier.IsMoving)
                    continue;
                Vector2Int from = soldier.Cell;
                for (int i = soldier.NextWaypoint; i < soldier.CurrentPath.Count; i++)
                {
                    Vector2Int to = soldier.CurrentPath[i];
                    if (to == cell || (from.x != to.x && from.y != to.y
                        && (new Vector2Int(from.x, to.y) == cell || new Vector2Int(to.x, from.y) == cell)))
                        return false;
                    from = to;
                }
            }
            return true;
        }

        private void Release(TrenchJob job)
        {
            if (job.Worker != null)
                job.Worker.SetWorkAssignment(false);
            job.Worker = null;
            job.PickingTool = null;
            if (job.Marker != null)
            {
                job.Marker.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(job.Marker.gameObject);
                else DestroyImmediate(job.Marker.gameObject);
            }
        }

        private void ResetJobs()
        {
            foreach (TrenchJob job in jobs)
                Release(job);
            jobs.Clear();
            SetMode(DiggingMode.None);
            retryTimer = 0f;
        }

        private void HandleMapGenerated(MapData data)
        {
            ResetJobs();
        }

        private SpriteRenderer CreateMarker(Vector2Int cell, Color color, string name)
        {
            var marker = new GameObject(name, typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            marker.transform.SetParent(transform, false);
            var position = new Vector3Int(cell.x, cell.y, 0);
            marker.transform.position = mapView.Tilemap.GetCellCenterWorld(position);
            marker.transform.localScale = Vector3.one * 0.85f;
            marker.sprite = mapView.Tilemap.GetSprite(position);
            marker.sortingOrder = 4;
            marker.color = color;
            return marker;
        }

        private void ShowPreview(IReadOnlyList<Vector2Int> cells)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                Color color = Mode == DiggingMode.Dig || Mode == DiggingMode.Ladder
                    ? new Color(0.3f, 0.8f, 1f, 0.6f)
                    : Mode == DiggingMode.Cut ? new Color(0.48f, 0.78f, 0.35f, 0.72f)
                    : new Color(1f, 0.2f, 0.2f, 0.6f);
                if (i == preview.Count)
                    preview.Add(CreateMarker(cells[i], color, "Dig preview"));
                SpriteRenderer marker = preview[i];
                var cell = new Vector3Int(cells[i].x, cells[i].y, 0);
                marker.transform.position = mapView.Tilemap.GetCellCenterWorld(cell);
                marker.sprite = mapView.Tilemap.GetSprite(cell);
                marker.color = color;
                marker.gameObject.SetActive(true);
            }
            for (int i = cells.Count; i < preview.Count; i++)
                preview[i].gameObject.SetActive(false);
        }

        private void ClearPreview()
        {
            foreach (SpriteRenderer marker in preview)
                if (marker != null)
                {
                    marker.gameObject.SetActive(false);
                    if (Application.isPlaying) Destroy(marker.gameObject);
                    else DestroyImmediate(marker.gameObject);
                }
            preview.Clear();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) { dragging = false; ClearPreview(); }
        }

        private void Notify(string message, bool warning = false)
        {
            Status = message;
            if (warning)
                Debug.LogWarning(message, this);
            Changed?.Invoke();
        }
    }
}
