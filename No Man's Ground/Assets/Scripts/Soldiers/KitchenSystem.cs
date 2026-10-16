using System;
using System.Collections.Generic;
using NoMansGround.Map;
using UnityEngine;

namespace NoMansGround.Soldiers
{
    public sealed class RationStock
    {
        public const int InitialProvisions = 1000;
        public const int ProvisionsPerRation = 10;
        public const float SecondsPerRation = 30f;
        private double elapsed;
        public int Provisions { get; private set; } = InitialProvisions;
        public int ReadyRations { get; private set; }
        public float Progress => (float)(elapsed / SecondsPerRation);

        public int TakeRations(int requested)
        {
            if (requested < 0)
                throw new ArgumentOutOfRangeException(nameof(requested));
            int taken = Math.Min(requested, ReadyRations);
            ReadyRations -= taken;
            return taken;
        }

        internal void ReturnRations(int count)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            ReadyRations += count;
        }

        public void AdvanceTime(float gameSeconds)
        {
            if (!float.IsFinite(gameSeconds) || gameSeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(gameSeconds));
            if (Provisions < ProvisionsPerRation)
                return;
            elapsed += gameSeconds;
            int completed = (int)Math.Min(Math.Floor(elapsed / SecondsPerRation), Provisions / ProvisionsPerRation);
            Provisions -= completed * ProvisionsPerRation;
            ReadyRations += completed;
            elapsed = Provisions < ProvisionsPerRation ? 0d : elapsed - completed * SecondsPerRation;
        }
    }

    [DefaultExecutionOrder(-50)]
    public sealed class KitchenSystem : MonoBehaviour
    {
        private static readonly Vector2Int[] Neighbours =
            { Vector2Int.down, Vector2Int.left, Vector2Int.right, Vector2Int.up };
        [SerializeField] private MapPrototype map;
        [SerializeField] private MapTilemapView mapView;
        [SerializeField] private SoldierRoster roster;
        [SerializeField] private Sprite facilitySprite;
        private GameObject kitchenObject;
        private GameObject depotObject;
        private MapData buildingMap;
        private float retryTimer;
        private readonly List<SoldierView> mealRecipients = new List<SoldierView>();
        private int mealDay;
        private int lastMealDay;
        public int CarriedRations { get; private set; }
        public int PendingMeals => mealRecipients.Count;
        public RationStock Stock { get; private set; } = new RationStock();
        public SoldierView Cook { get; private set; }
        public Vector2Int KitchenCell { get; private set; }
        public Vector2Int DepotCell { get; private set; }
        public bool HasFacilities => kitchenObject != null && depotObject != null;
        public string Status { get; private set; } = "Cucina non pronta.";
        public event Action Changed;

        public void Configure(MapPrototype prototype, MapTilemapView view, SoldierRoster soldiers, Sprite sprite)
        {
            map = prototype;
            mapView = view;
            roster = soldiers;
            facilitySprite = sprite;
        }

        private void Awake()
        {
            if (map == null || mapView == null || roster == null || facilitySprite == null)
            {
                Debug.LogError("KitchenSystem requires map, view, roster and facility sprite.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (roster != null)
            {
                roster.Rebuilt += Rebuild;
                if (Application.isPlaying && roster.Soldiers.Count > 0
                    && (!HasFacilities || Cook == null || buildingMap != map.Data))
                    Rebuild();
            }
        }

        private void OnDisable()
        {
            if (roster != null)
                roster.Rebuilt -= Rebuild;
            if (Cook != null)
                Cook.SetWorkAssignment(false);
            SetStatus("Cucina sospesa.");
        }

        private void OnDestroy()
        {
            ClearFacilities();
        }

        public void Rebuild()
        {
            if (Cook != null)
                Cook.SetWorkAssignment(false);
            ClearFacilities();
            Stock = new RationStock();
            mealRecipients.Clear();
            CarriedRations = 0;
            mealDay = lastMealDay = 0;
            Cook = null;
            foreach (SoldierView soldier in roster.Soldiers)
                if (soldier.Data.Profession == SoldierProfession.Cook)
                {
                    Cook = soldier;
                    break;
                }
            if (Cook == null)
            {
                SetStatus("Nessun cuoco disponibile: cucina non generata.");
                return;
            }
            if (map.Data == null || !TryBuildingCell(out Vector2Int kitchen))
            {
                SetStatus("Spazio insufficiente per la cucina.");
                Debug.LogWarning(Status, this);
                return;
            }
            buildingMap = map.Data;
            KitchenCell = kitchen;
            buildingMap.SetWalkable(KitchenCell, false);
            if (!TryBuildingCell(out Vector2Int depot))
            {
                buildingMap.SetWalkable(KitchenCell, true);
                buildingMap = null;
                SetStatus("Spazio insufficiente per il deposito.");
                Debug.LogWarning(Status, this);
                return;
            }
            DepotCell = depot;
            buildingMap.SetWalkable(DepotCell, false);
            kitchenObject = CreateFacility("CUCINA", KitchenCell, new Color(0.7f, 0.45f, 0.25f));
            depotObject = CreateFacility("VIVERI", DepotCell, new Color(0.7f, 0.7f, 0.3f));
            retryTimer = 0f;
            SetStatus("Cuoco in attesa di raggiungere la cucina.");
        }

        private bool TryBuildingCell(out Vector2Int cell)
        {
            int center = map.Data.Width / 2;
            for (int y = 5; y < map.Data.Height; y++)
                for (int distance = 0; distance < map.Data.Width; distance++)
                    for (int side = -1; side <= 1; side += 2)
                    {
                        cell = new Vector2Int(center + distance * side, y);
                        if (!map.Data.IsWalkable(cell) || map.Data.IsTrench(cell)
                            || map.Data[cell.x, cell.y] == TerrainKind.Road)
                            continue;
                        if (buildingMap != null && (cell - KitchenCell).sqrMagnitude < 4)
                            continue;
                        bool occupied = false;
                        foreach (SoldierView soldier in roster.Soldiers)
                            if (soldier.Cell == cell || soldier.Destination == cell)
                                occupied = true;
                        if (!occupied)
                            return true;
                    }
            cell = default;
            return false;
        }

        private GameObject CreateFacility(string label, Vector2Int cell, Color color)
        {
            var obj = new GameObject(label, typeof(SpriteRenderer));
            obj.transform.SetParent(transform, false);
            obj.transform.position = mapView.Tilemap.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));
            var sprite = obj.GetComponent<SpriteRenderer>();
            sprite.sprite = facilitySprite;
            sprite.color = color;
            sprite.sortingOrder = 7;
            var caption = new GameObject(label + " label", typeof(TextMesh));
            caption.transform.SetParent(obj.transform, false);
            caption.transform.localPosition = new Vector3(0f, 0.65f, 0f);
            var text = caption.GetComponent<TextMesh>();
            text.text = label;
            text.anchor = TextAnchor.MiddleCenter;
            text.fontSize = 32;
            text.characterSize = 0.08f;
            caption.GetComponent<MeshRenderer>().sortingOrder = 8;
            return obj;
        }

        private void ClearFacilities()
        {
            if (buildingMap != null && HasFacilities)
            {
                buildingMap.SetWalkable(KitchenCell, true);
                buildingMap.SetWalkable(DepotCell, true);
            }
            buildingMap = null;
            RemoveFacility(kitchenObject);
            RemoveFacility(depotObject);
            kitchenObject = depotObject = null;
        }

        private static void RemoveFacility(GameObject obj)
        {
            if (obj == null)
                return;
            obj.SetActive(false);
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }

        private void Update() { Tick(Time.deltaTime); }

        public bool TrySelectAt(Camera camera, Vector2 pointer, out bool depot)
        {
            depot = false;
            if (!HasFacilities || !camera.pixelRect.Contains(pointer))
                return false;
            Vector3 world = camera.ScreenToWorldPoint(new Vector3(pointer.x, pointer.y,
                mapView.transform.position.z - camera.transform.position.z));
            Vector3Int cell = mapView.Tilemap.WorldToCell(world);
            var position = new Vector2Int(cell.x, cell.y);
            depot = position == DepotCell;
            return depot || position == KitchenCell;
        }

        public void Tick(float gameSeconds)
        {
            if (!float.IsFinite(gameSeconds) || gameSeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(gameSeconds));
            if (gameSeconds == 0f || !HasFacilities)
                return;
            UpdateMealSchedule();
            if (Cook == null || !Cook.isActiveAndEnabled || Cook.Data.Profession != SoldierProfession.Cook)
            {
                if (Cook != null) Cook.SetWorkAssignment(false);
                SetStatus("Nessun cuoco disponibile.");
                return;
            }
            if (Cook.IsMoving)
            {
                SetStatus(mealDay != 0 && CarriedRations > 0
                    ? "Cuoco in marcia per distribuire il rancio." : "Cuoco in marcia verso la cucina.");
                return;
            }
            if (mealRecipients.Count > 0 && DistributeMeals(gameSeconds))
                return;
            if (Stock.Provisions < RationStock.ProvisionsPerRation
                && !(mealRecipients.Count > 0 && Stock.ReadyRations > 0))
            {
                Cook.SetWorkAssignment(false);
                SetStatus("Viveri esauriti. Produzione ferma.");
                return;
            }
            if (Cook.HasWorkAssignment && IsAtKitchen())
            {
                Stock.AdvanceTime(gameSeconds);
                Cook.SetWorkAssignment(true, Stock.Progress);
                SetStatus(Stock.Provisions >= 10 ? "Preparazione rancio." : "Viveri esauriti. Produzione ferma.");
                if (Stock.Provisions < 10) Cook.SetWorkAssignment(false);
                Changed?.Invoke();
                return;
            }
            retryTimer -= gameSeconds;
            if (retryTimer > 0f)
                return;
            retryTimer = 0.5f;
            GridPath best = FindServingPath(KitchenCell);
            if (best == null)
            {
                Cook.SetWorkAssignment(false);
                SetStatus("Accesso cucina bloccato o occupato.");
                return;
            }
            Cook.SetWorkAssignment(true, Stock.Progress);
            if (best.Cells.Count > 1)
                Cook.BeginMove(mapView, best.Cells);
            SetStatus(Cook.IsMoving ? "Cuoco in marcia verso la cucina." : "Preparazione rancio.");
        }

        private void UpdateMealSchedule()
        {
            if (mealDay != 0 && mealDay != map.Clock.Day)
            {
                Stock.ReturnRations(CarriedRations);
                CarriedRations = 0;
                mealRecipients.Clear();
                mealDay = 0;
                retryTimer = 0f;
                Changed?.Invoke();
            }
            if (map.Clock.Day < lastMealDay)
                lastMealDay = 0;
            if (map.Clock.Hour < 13 || lastMealDay == map.Clock.Day)
                return;
            mealDay = lastMealDay = map.Clock.Day;
            foreach (SoldierView soldier in roster.Soldiers)
                if (soldier != null)
                    mealRecipients.Add(soldier);
            retryTimer = 0f;
            Changed?.Invoke();
        }

        private bool DistributeMeals(float gameSeconds)
        {
            mealRecipients.RemoveAll(soldier => soldier == null);
            if (IsAtKitchen() && Stock.ReadyRations > 0)
            {
                CarriedRations += Stock.TakeRations(Mathf.Max(0, mealRecipients.Count - CarriedRations));
                if (CarriedRations > 0 && mealRecipients.Remove(Cook))
                {
                    CarriedRations--;
                    Cook.Data.Needs.EatRation();
                }
                Changed?.Invoke();
            }
            if (mealRecipients.Count == 0)
            {
                Stock.ReturnRations(CarriedRations);
                CarriedRations = 0;
                return false;
            }
            if (CarriedRations == 0)
            {
                if (Stock.ReadyRations == 0)
                    return false;
                // Ready meals must be collected at the kitchen, not remotely.
                Cook.SetWorkAssignment(false);
                return false;
            }
            Cook.SetWorkAssignment(true);
            for (int i = mealRecipients.Count - 1; i >= 0 && CarriedRations > 0; i--)
            {
                SoldierView soldier = mealRecipients[i];
                if (!soldier.isActiveAndEnabled || soldier == Cook
                    || Mathf.Abs(Cook.Cell.x - soldier.Cell.x) + Mathf.Abs(Cook.Cell.y - soldier.Cell.y) != 1
                    || !map.Data.CanStep(Cook.Cell, soldier.Cell)
                    || Vector3.Distance(Cook.transform.position, soldier.transform.position) > 1.05f)
                    continue;
                soldier.Data.Needs.EatRation();
                CarriedRations--;
                mealRecipients.RemoveAt(i);
                Changed?.Invoke();
            }
            if (mealRecipients.Count == 0)
            {
                retryTimer = 0f;
                return false;
            }
            if (CarriedRations == 0)
            {
                Cook.SetWorkAssignment(false);
                retryTimer = 0f;
                return false;
            }
            retryTimer -= gameSeconds;
            if (retryTimer > 0f)
                return true;
            retryTimer = 0.5f;
            GridPath best = null;
            foreach (SoldierView soldier in mealRecipients)
            {
                if (soldier == Cook || !soldier.isActiveAndEnabled)
                    continue;
                GridPath candidate = FindServingPath(soldier.Cell);
                if (candidate != null && candidate.Cells.Count > 1 && (best == null || candidate.Cost < best.Cost))
                    best = candidate;
            }
            if (best == null)
            {
                SetStatus("Rancio in attesa: soldati non raggiungibili.");
                return true;
            }
            if (best.Cells.Count > 1)
                Cook.BeginMove(mapView, best.Cells);
            SetStatus("Distribuzione rancio.");
            return true;
        }

        private GridPath FindServingPath(Vector2Int target)
        {
            GridPath best = null;
            foreach (Vector2Int offset in Neighbours)
            {
                Vector2Int destination = target + offset;
                if (target != KitchenCell && !map.Data.CanStep(destination, target))
                    continue;
                if (target == KitchenCell && map.Data.IsTrench(destination))
                    continue;
                bool reserved = false;
                foreach (SoldierView other in roster.Soldiers)
                    if (other != Cook && other != null && other.Destination == destination)
                        reserved = true;
                var trenches = roster.GetComponent<TrenchWorkOrders>();
                if (trenches != null)
                    foreach (TrenchJob job in trenches.Jobs)
                        if (job.Kind == TrenchJobKind.Dig && job.Cell == destination)
                            reserved = true;
                if (reserved)
                    continue;
                GridPath path = GridPathfinder.FindNearest(map.Data, new[] { Cook.Cell }, destination);
                if (path != null && (best == null || path.Cost < best.Cost))
                    best = path;
            }
            return best;
        }

        private bool IsAtKitchen()
        {
            return Mathf.Abs(Cook.Cell.x - KitchenCell.x) + Mathf.Abs(Cook.Cell.y - KitchenCell.y) == 1
                && !map.Data.IsTrench(Cook.Cell);
        }

        private void SetStatus(string status)
        {
            if (Status == status)
                return;
            Status = status;
            Changed?.Invoke();
        }
    }
}
