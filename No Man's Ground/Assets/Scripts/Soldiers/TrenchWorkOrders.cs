using System;
using System.Collections.Generic;
using NoMansGround.Map;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace NoMansGround.Soldiers
{
    public enum DiggingMode { None, Dig, Cancel }

    public sealed class TrenchJob
    {
        public Vector2Int Cell { get; }
        public SoldierView Worker { get; internal set; }
        public float Progress { get; internal set; }
        public string Status { get; internal set; } = "In attesa";
        internal SpriteRenderer Marker;

        internal TrenchJob(Vector2Int cell) { Cell = cell; }
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
        [SerializeField, Min(0)] private int strengthExperience = 25;

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
        public string Status { get; private set; } = "T: scava | X: annulla | Esc: esci";
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
                || !float.IsFinite(baseDigSeconds) || baseDigSeconds <= 0f || strengthExperience < 0)
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
                : mode == DiggingMode.Cancel ? "ANNULLA: trascina sugli scavi.\nEsc o destro: esci."
                : "T: scava | X: annulla | Esc: esci");
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
                if (keyboard.xKey.wasPressedThisFrame)
                    SetMode(Mode == DiggingMode.Cancel ? DiggingMode.None : DiggingMode.Cancel);
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
                    if (Mode == DiggingMode.Dig ? Designate(target) : Cancel(target))
                        changed++;
                Notify(changed > 0 ? $"{changed} celle {(Mode == DiggingMode.Dig ? "designate" : "annullate")}."
                    : "Nessuna cella valida: scavo gia presente,\nbloccato o fuori mappa.", changed == 0);
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
                || map.Data[cell.x, cell.y] == TerrainKind.Trench || jobs.Exists(job => job.Cell == cell))
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
                    job.Status = "In attesa";
                    changed = true;
                    continue;
                }
                if (worker.IsMoving)
                    continue;
                if (Mathf.Abs(worker.Cell.x - job.Cell.x) + Mathf.Abs(worker.Cell.y - job.Cell.y) != 1)
                    throw new InvalidOperationException("A trench worker must stand next to the designated cell.");
                job.Status = "Scavo";
                float multiplier = 1f + worker.Data.GetSkill(SoldierSkill.Strength).Value / 100f;
                job.Progress = Mathf.Min(1f, job.Progress + deltaTime * multiplier / baseDigSeconds);
                worker.SetWorkAssignment(true, job.Progress);
                job.Marker.color = Color.Lerp(new Color(1f, 0.75f, 0.15f, 0.65f),
                    new Color(0.3f, 0.9f, 0.4f, 0.85f), job.Progress);
                changed = true;
                if (job.Progress < 1f)
                    continue;
                mapView.CompleteTrench(map.Data, job.Cell);
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
            var starts = new List<Vector2Int>();
            foreach (SoldierView soldier in roster.Soldiers)
                if (soldier != null && soldier.IsAvailable)
                {
                    available.Add(soldier);
                    starts.Add(soldier.Cell);
                }
            if (available.Count == 0)
            {
                bool changed = job.Status != "Nessun soldato libero";
                job.Status = "Nessun soldato libero";
                return changed;
            }
            GridPath best = null;
            foreach (Vector2Int offset in Neighbours)
            {
                Vector2Int workCell = job.Cell + offset;
                if (!map.Data.IsWalkable(workCell))
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
            job.Worker.SetWorkAssignment(true, job.Progress);
            if (best.Cells.Count > 1)
                job.Worker.BeginMove(mapView, best.Cells);
            job.Status = job.Worker.IsMoving ? "In arrivo" : "Scavo";
            return true;
        }

        private void Release(TrenchJob job)
        {
            if (job.Worker != null)
                job.Worker.SetWorkAssignment(false);
            job.Worker = null;
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
                Color color = Mode == DiggingMode.Dig
                    ? new Color(0.3f, 0.8f, 1f, 0.6f) : new Color(1f, 0.2f, 0.2f, 0.6f);
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
