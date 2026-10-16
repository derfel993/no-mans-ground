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
        private IReadOnlyList<Vector2Int> path;
        private int waypoint;
        public bool HasWorkAssignment { get; private set; }
        public bool IsAvailable => isActiveAndEnabled && !IsMoving && !HasWorkAssignment;
        public SoldierDuty Duty { get; private set; } = SoldierDuty.Work;
        public bool GuardRequested { get; private set; }
        public bool CanTakeEngineeringWork => IsAvailable && Duty == SoldierDuty.Work
            && !GuardRequested && Data.Profession == SoldierProfession.Engineer;
        public bool CanTakeGuardMove => IsAvailable && Duty == SoldierDuty.Guard;
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

        public bool RequestDuty(SoldierDuty duty)
        {
            if (!Enum.IsDefined(typeof(SoldierDuty), duty))
                throw new ArgumentOutOfRangeException(nameof(duty));
            if (Data.Profession == SoldierProfession.Cook && duty == SoldierDuty.Guard)
            {
                Debug.LogWarning("Il cuoco resta in Lavoro.", this);
                return false;
            }
            GuardRequested = duty == SoldierDuty.Guard && HasWorkAssignment;
            if (!GuardRequested)
                Duty = duty;
            DutyChanged?.Invoke();
            MovementChanged?.Invoke();
            return true;
        }

        public void Initialize(SoldierData data, Vector2Int cell, Sprite sprite)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (sprite == null)
                throw new ArgumentNullException(nameof(sprite));

            Data = data;
            Cell = cell;
            name = data.FullName;
            body = GetComponent<SpriteRenderer>();
            body.sprite = sprite;
            body.color = Color.white;
            if (data.Profession == SoldierProfession.Cook)
                body.color = new Color(1f, 0.85f, 0.65f);
            body.sortingOrder = 10;
            var highlight = new GameObject("Selection", typeof(SpriteRenderer));
            highlight.transform.SetParent(transform, false);
            highlight.transform.localScale = Vector3.one * 1.3f;
            selection = highlight.GetComponent<SpriteRenderer>();
            selection.sprite = sprite;
            selection.color = new Color(1f, 0.85f, 0.25f);
            selection.sortingOrder = 9;
            SetSelected(false);
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
            if (Data == null || IsMoving)
                throw new InvalidOperationException("Only initialized, available soldiers can begin moving.");
            if (cells == null || cells.Count < 2 || cells[0] != Cell)
                throw new ArgumentException("A movement path must start at the soldier cell and include a destination.", nameof(cells));
            movementMap = mapView;
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
                DutyChanged?.Invoke();
            }
            MovementChanged?.Invoke();
        }

        private void Update()
        {
            Data.Needs.AdvanceTime(Time.deltaTime);
            AdvanceMovement(Time.deltaTime);
        }

        public void AdvanceMovement(float deltaTime)
        {
            if (!float.IsFinite(deltaTime) || deltaTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            if (!IsMoving)
                return;
            float remainingDistance = MovementSpeed * deltaTime;
            while (IsMoving && remainingDistance > 0f)
            {
                Vector2Int cell = path[waypoint];
                Vector3 target = movementMap.Tilemap.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));
                float distance = Vector3.Distance(transform.position, target);
                if (distance > remainingDistance)
                {
                    transform.position = Vector3.MoveTowards(transform.position, target, remainingDistance);
                    break;
                }
                transform.position = target;
                remainingDistance -= distance;
                Cell = cell;
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
