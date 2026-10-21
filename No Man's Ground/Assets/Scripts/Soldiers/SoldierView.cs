using System;
using System.Collections.Generic;
using NoMansGround.Map;
using UnityEngine;

namespace NoMansGround.Soldiers
{
    public enum SoldierDuty { Work, Guard }

    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SoldierView : MonoBehaviour
    {
        private SpriteRenderer body;
        private SpriteRenderer selection;
        private MapTilemapView movementMap;
        private Sprite fallbackSprite;
        private Sprite corpseSprite;
        private Sprite[] directionalSprites;
        private static Sprite defaultCorpseSprite;
        private int displayedDirection = -1;
        private IReadOnlyList<Vector2Int> path;
        private int waypoint;
        private double guardScanSeconds;
        public Vector2 FacingDirection { get; private set; } = Vector2.up;
        public bool IsManualLook { get; private set; }
        public Vector2 LookDirection => !IsManualLook && Duty == SoldierDuty.Guard && !IsMoving && !HasWorkAssignment
            ? (Vector2)(Quaternion.Euler(0f, 0f, 45f * Mathf.Sin((float)(guardScanSeconds / 6d * System.Math.PI * 2d)))
                * FacingDirection) : FacingDirection;
        public bool HasWorkAssignment { get; private set; }
        public bool IsAvailable => IsAlive && isActiveAndEnabled && !IsMoving && !HasWorkAssignment;
        public bool IsAlive => Data != null && Data.IsAlive;
        public SoldierDuty Duty { get; private set; } = SoldierDuty.Work;
        public bool GuardRequested { get; private set; }
        public SoldierTask? RequestedTask { get; private set; }
        public bool TaskReady { get; private set; } = true;
        public bool IsReturningTaskTool { get; internal set; }
        public bool IsChangingTaskEquipment { get; internal set; }
        public bool IsEvacuatingBuilding { get; internal set; }
        public bool AutomaticTransportEnabled { get; private set; }
        public bool CanAutoTransport => IsAlive && isActiveAndEnabled && Data.Task == SoldierTask.Hauler
            && AutomaticTransportEnabled && Duty == SoldierDuty.Work && !GuardRequested
            && !IsReturningTaskTool && !IsEvacuatingBuilding && TaskReady && !RequestedTask.HasValue;
        public string TaskStatus { get; private set; } = "Pronto";
        public bool CanTakeEngineeringWork => IsAvailable && Duty == SoldierDuty.Work
            && !GuardRequested && !IsReturningTaskTool && !IsEvacuatingBuilding && TaskReady && !RequestedTask.HasValue
            && Data.Task != SoldierTask.Cook;
        public bool CanTakeGuardMove => IsAlive && isActiveAndEnabled && !HasWorkAssignment && Duty == SoldierDuty.Guard
            && !IsReturningTaskTool && !IsEvacuatingBuilding && TaskReady && !RequestedTask.HasValue;
        public Vector2Int GuardPathStart => IsMoving && !transform.position.Equals(
            movementMap.Tilemap.GetCellCenterWorld(new Vector3Int(Cell.x, Cell.y, 0)))
                ? path[waypoint] : Cell;
        public float DiggingProgress { get; private set; }
        public TrenchJobKind WorkKind { get; private set; }

        public SoldierData Data { get; private set; }
        public Vector2Int Cell { get; private set; }
        public bool IsSelected { get; private set; }
        public bool IsMoving => path != null;
        public IReadOnlyList<Vector2Int> CurrentPath => path;
        public int NextWaypoint => waypoint;
        public Vector2Int Destination => IsMoving ? path[path.Count - 1] : Cell;
        public float MovementSpeed => Mathf.Lerp(1.5f, 4.5f, Data.GetSkill(SoldierSkill.Speed).Value / 100f);
        public event Action MovementChanged;
        public event Action DutyChanged;
        public event Action TaskChanged;
        public event Action<SoldierView> Died;

        public bool SetManualLook(Vector2 direction)
        {
            if (!CanTakeGuardMove || IsMoving || !float.IsFinite(direction.x) || !float.IsFinite(direction.y)
                || direction.sqrMagnitude < 0.0001f)
                return false;
            IsManualLook = true;
            FacingDirection = direction.normalized;
            guardScanSeconds = 0d;
            AdvanceLook(0f);
            MovementChanged?.Invoke();
            return true;
        }

        public void StopManualLook()
        {
            if (!IsManualLook)
                return;
            IsManualLook = false;
            guardScanSeconds = 0d;
            AdvanceLook(0f);
            MovementChanged?.Invoke();
        }

        public void RequestTask(SoldierTask task)
        {
            if (!Enum.IsDefined(typeof(SoldierTask), task))
                throw new ArgumentOutOfRangeException(nameof(task));
            if (!IsAlive)
                return;
            StopManualLook();
            if (task == Data.Task && !IsChangingTaskEquipment)
            {
                RequestedTask = null;
                TaskReady = true;
                SetTaskStatus("Pronto");
            }
            else
            {
                RequestedTask = task;
                TaskReady = false;
                SetTaskStatus(HasWorkAssignment && !IsChangingTaskEquipment ? "Cambio a fine lavoro" : "In attesa del deposito");
            }
            TaskChanged?.Invoke();
            MovementChanged?.Invoke();
        }

        public void SetAutomaticTransport(bool enabled)
        {
            if (!IsAlive)
                return;
            if (AutomaticTransportEnabled == enabled)
                return;
            AutomaticTransportEnabled = enabled;
            MovementChanged?.Invoke();
        }

        internal void SetTaskStatus(string status)
        {
            if (TaskStatus == status)
                return;
            TaskStatus = status;
            TaskChanged?.Invoke();
            MovementChanged?.Invoke();
        }

        internal void CompleteTaskEquipment()
        {
            if (RequestedTask.HasValue)
                Data.Task = RequestedTask.Value;
            RequestedTask = null;
            TaskReady = true;
            IsChangingTaskEquipment = false;
            if (Data.Task == SoldierTask.Cook)
            {
                Duty = SoldierDuty.Work;
                GuardRequested = false;
                DutyChanged?.Invoke();
            }
            if (body != null)
                body.color = Data.Task == SoldierTask.Cook ? new Color(1f, 0.85f, 0.65f) : Color.white;
            SetWorkAssignment(false);
            SetTaskStatus("Pronto");
            TaskChanged?.Invoke();
        }

        public bool RequestDuty(SoldierDuty duty)
        {
            if (!Enum.IsDefined(typeof(SoldierDuty), duty))
                throw new ArgumentOutOfRangeException(nameof(duty));
            if (!IsAlive)
                return false;
            StopManualLook();
            if (Data.Task == SoldierTask.Cook && duty == SoldierDuty.Guard)
            {
                Debug.LogWarning("Il cuoco resta in Lavoro.", this);
                return false;
            }
            GuardRequested = duty == SoldierDuty.Guard && HasWorkAssignment;
            if (!GuardRequested)
            {
                if (Duty != duty) guardScanSeconds = 0d;
                Duty = duty;
            }
            DutyChanged?.Invoke();
            MovementChanged?.Invoke();
            return true;
        }

        public void Initialize(SoldierData data, Vector2Int cell, Sprite sprite,
            Sprite[] spritesByDirection = null, Sprite deadSprite = null)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (sprite == null)
                throw new ArgumentNullException(nameof(sprite));

            if (Data != null)
                Data.Died -= HandleDeath;
            Data = data;
            Data.Died += HandleDeath;
            Cell = cell;
            fallbackSprite = sprite;
            directionalSprites = spritesByDirection;
            corpseSprite = deadSprite != null ? deadSprite : GetDefaultCorpseSprite();
            name = data.FullName;
            body = GetComponent<SpriteRenderer>();
            body.sprite = sprite;
            body.color = Color.white;
            if (data.Task == SoldierTask.Cook)
                body.color = new Color(1f, 0.85f, 0.65f);
            body.sortingOrder = 10;
            var highlight = new GameObject("Selection", typeof(SpriteRenderer));
            highlight.transform.SetParent(transform, false);
            highlight.transform.localScale = Vector3.one * 1.3f;
            selection = highlight.GetComponent<SpriteRenderer>();
            selection.sprite = sprite;
            selection.color = new Color(1f, 0.85f, 0.25f);
            selection.sortingOrder = 9;
            UpdateDirectionalSprite(FacingDirection);
            SetSelected(false);
            if (!Data.IsAlive)
                HandleDeath();
        }

        private void HandleDeath()
        {
            path = null;
            HasWorkAssignment = false;
            DiggingProgress = 0f;
            GuardRequested = false;
            RequestedTask = null;
            IsManualLook = false;
            IsReturningTaskTool = false;
            IsChangingTaskEquipment = false;
            IsEvacuatingBuilding = false;
            AutomaticTransportEnabled = false;
            TaskReady = false;
            TaskStatus = "Morto";
            SetSelected(false);
            body.sprite = corpseSprite;
            body.color = Color.white;
            MovementChanged?.Invoke();
            Died?.Invoke(this);
        }

        private static Sprite GetDefaultCorpseSprite()
        {
            if (defaultCorpseSprite != null)
                return defaultCorpseSprite;

            const int size = 32;
            var pixels = new Color32[size * size];
            Color32 shadow = new Color32(24, 22, 18, 90);
            Color32 outline = new Color32(31, 35, 29, 255);
            Color32 uniform = new Color32(82, 99, 64, 255);
            Color32 uniformLight = new Color32(116, 132, 82, 255);
            Color32 helmet = new Color32(75, 78, 66, 255);
            Color32 skin = new Color32(199, 153, 112, 255);
            Color32 leather = new Color32(91, 61, 37, 255);

            FillPixels(pixels, size, 3, 7, 29, 12, shadow);
            FillPixels(pixels, size, 3, 14, 12, 25, outline);
            FillPixels(pixels, size, 4, 19, 11, 24, helmet);
            FillPixels(pixels, size, 5, 15, 10, 19, skin);
            FillPixels(pixels, size, 9, 10, 24, 23, outline);
            FillPixels(pixels, size, 10, 12, 22, 21, uniform);
            FillPixels(pixels, size, 11, 18, 20, 21, uniformLight);
            FillPixels(pixels, size, 16, 12, 18, 21, leather);
            FillPixels(pixels, size, 21, 11, 29, 21, outline);
            FillPixels(pixels, size, 22, 13, 27, 19, uniform);
            FillPixels(pixels, size, 27, 10, 31, 14, outline);
            FillPixels(pixels, size, 28, 11, 30, 13, helmet);
            FillPixels(pixels, size, 7, 10, 11, 15, outline);
            FillPixels(pixels, size, 8, 11, 10, 14, uniformLight);

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply();
            defaultCorpseSprite = Sprite.Create(texture, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), 32f);
            defaultCorpseSprite.hideFlags = HideFlags.HideAndDontSave;
            return defaultCorpseSprite;
        }

        private static void FillPixels(Color32[] pixels, int size, int left, int bottom,
            int right, int top, Color32 color)
        {
            for (int y = bottom; y < top; y++)
                for (int x = left; x < right; x++)
                    pixels[y * size + x] = color;
        }

        private void OnDestroy()
        {
            if (Data != null)
                Data.Died -= HandleDeath;
        }

        private void UpdateDirectionalSprite(Vector2 direction)
        {
            if (body == null || direction.sqrMagnitude < 0.0001f)
                return;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            int directionIndex = Mathf.RoundToInt((90f - angle) / 45f) % 8;
            if (directionIndex < 0)
                directionIndex += 8;
            if (directionIndex == displayedDirection)
                return;
            displayedDirection = directionIndex;
            Sprite sprite = directionalSprites != null && directionalSprites.Length == 8
                ? directionalSprites[directionIndex] : null;
            if (sprite == null)
                sprite = fallbackSprite;
            body.sprite = sprite;
            if (selection != null)
                selection.sprite = sprite;
        }

        public void SetSelected(bool selected)
        {
            if (selection == null)
                throw new InvalidOperationException("Initialize the soldier view before selecting it.");
            IsSelected = selected;
            selection.enabled = selected;
        }

        public void BeginMove(MapTilemapView mapView, IReadOnlyList<Vector2Int> cells)
        {
            if (mapView == null)
                throw new ArgumentNullException(nameof(mapView));
            if (!IsAlive || IsMoving)
                throw new InvalidOperationException("Only initialized, available soldiers can begin moving.");
            if (cells == null || cells.Count < 2 || cells[0] != Cell)
                throw new ArgumentException("A movement path must start at the soldier cell and include a destination.", nameof(cells));
            movementMap = mapView;
            StopManualLook();
            path = new List<Vector2Int>(cells).AsReadOnly();
            waypoint = 1;
            MovementChanged?.Invoke();
        }

        internal void SetWorkAssignment(bool assigned, float progress = 0f, TrenchJobKind kind = TrenchJobKind.Dig)
        {
            HasWorkAssignment = assigned;
            DiggingProgress = progress;
            WorkKind = kind;
            if (!assigned && GuardRequested)
            {
                GuardRequested = false;
                Duty = SoldierDuty.Guard;
                guardScanSeconds = 0d;
                DutyChanged?.Invoke();
            }
            MovementChanged?.Invoke();
        }

        internal void SetGuardPath(MapTilemapView mapView, IReadOnlyList<Vector2Int> cells)
        {
            if (mapView == null)
                throw new ArgumentNullException(nameof(mapView));
            if (Data == null)
                throw new InvalidOperationException("Initialize the soldier before assigning a guard path.");
            if (!CanTakeGuardMove)
                throw new InvalidOperationException("Only guards without a work assignment can receive guard paths.");
            StopManualLook();
            Vector2Int start = GuardPathStart;
            if (cells == null || cells.Count == 0 || cells[0] != start)
                throw new ArgumentException("The guard path must start at the next reachable cell centre.", nameof(cells));
            var replacement = new List<Vector2Int>();
            if (start != Cell)
                replacement.Add(Cell);
            replacement.AddRange(cells);
            movementMap = mapView;
            path = replacement.Count > 1 ? replacement.AsReadOnly() : null;
            waypoint = 1;
            MovementChanged?.Invoke();
        }

        private void Update()
        {
            if (!IsAlive)
                return;
            Data.AdvanceTime(Time.deltaTime);
            AdvanceMovement(Time.deltaTime);
            AdvanceLook(Time.deltaTime);
        }

        public void AdvanceLook(float deltaTime)
        {
            if (!float.IsFinite(deltaTime) || deltaTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            if (!IsAlive)
                return;
            if (!IsManualLook && Duty == SoldierDuty.Guard && !IsMoving && !HasWorkAssignment)
                guardScanSeconds = (guardScanSeconds + deltaTime) % 6d;
            else
                guardScanSeconds = 0d;
            Vector2 direction = LookDirection;
            UpdateDirectionalSprite(direction);
        }

        public void AdvanceMovement(float deltaTime)
        {
            if (!float.IsFinite(deltaTime) || deltaTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            if (!IsAlive || !IsMoving)
                return;
            float remainingDistance = MovementSpeed * deltaTime;
            while (IsMoving && remainingDistance > 0f)
            {
                Vector2Int cell = path[waypoint];
                Vector3 target = movementMap.Tilemap.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));
                float distance = Vector3.Distance(transform.position, target);
                Vector2 direction = target - transform.position;
                if (direction.sqrMagnitude > 0.000001f)
                {
                    FacingDirection = direction.normalized;
                    guardScanSeconds = 0d;
                    UpdateDirectionalSprite(FacingDirection);
                }
                if (distance > remainingDistance)
                {
                    transform.position = Vector3.MoveTowards(transform.position, target, remainingDistance);
                    break;
                }
                transform.position = target;
                remainingDistance -= distance;
                Vector2Int previousCell = Cell;
                Cell = cell;
                if (previousCell != cell)
                    Data.Needs.AddFatigue(1f);
                waypoint++;
                if (waypoint == path.Count)
                {
                    path = null;
                    MovementChanged?.Invoke();
                }
            }
        }
    }
}
