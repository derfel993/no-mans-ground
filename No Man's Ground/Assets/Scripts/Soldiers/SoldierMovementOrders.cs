using System;
using System.Collections.Generic;
using System.Linq;
using NoMansGround.Map;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace NoMansGround.Soldiers
{
    public sealed class SoldierMovementOrders : MonoBehaviour
    {
        [SerializeField] private MapPrototype map;
        [SerializeField] private MapTilemapView mapView;
        [SerializeField] private SoldierRoster roster;
        [SerializeField] private Camera mapCamera;
        [SerializeField] private TrenchWorkOrders trenchOrders;

        private LineRenderer route;
        private SpriteRenderer destinationMarker;
        private SoldierView routeSoldier;
        private Material routeMaterial;
        private SoldierView lookSoldier;
        private Vector2 lookDragOrigin;
        public SoldierView Selected { get; set; }

        public string Status { get; private set; } = "Destro: muovi la guardia scelta.\nMaiusc+destro: ostacolo debug.";
        public event Action StatusChanged;

        public void ConfigureTrenches(TrenchWorkOrders orders)
        {
            trenchOrders = orders;
        }

        public void Configure(MapPrototype prototype, MapTilemapView view, SoldierRoster soldierRoster, Camera camera)
        {
            map = prototype;
            mapView = view;
            roster = soldierRoster;
            mapCamera = camera;
        }

        private void Awake()
        {
            if (map == null || mapView == null || roster == null || mapCamera == null)
            {
                Debug.LogError("SoldierMovementOrders requires a map, tilemap view, roster and camera.", this);
                enabled = false;
                return;
            }
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
                throw new InvalidOperationException("The movement route requires the Sprites/Default shader.");
            routeMaterial = new Material(shader);
            var routeObject = new GameObject("Movement Route", typeof(LineRenderer));
            routeObject.transform.SetParent(transform, false);
            route = routeObject.GetComponent<LineRenderer>();
            route.sharedMaterial = routeMaterial;
            route.startColor = route.endColor = new Color(0.95f, 0.80f, 0.25f);
            route.startWidth = route.endWidth = 0.08f;
            route.sortingOrder = 5;
            route.positionCount = 0;
            route.useWorldSpace = true;
            var markerObject = new GameObject("Movement Destination", typeof(SpriteRenderer));
            markerObject.transform.SetParent(transform, false);
            destinationMarker = markerObject.GetComponent<SpriteRenderer>();
            destinationMarker.sortingOrder = 6;
            destinationMarker.color = new Color(1f, 0.85f, 0.2f, 0.7f);
            destinationMarker.enabled = false;
        }

        private void OnEnable()
        {
            if (roster != null)
            {
                roster.Rebuilt += ResetOrders;
                roster.SoldierDied += HandleSoldierDied;
            }
        }

        private void OnDisable()
        {
            EndLookDrag();
            if (roster != null)
            {
                roster.Rebuilt -= ResetOrders;
                roster.SoldierDied -= HandleSoldierDied;
            }
            ClearRoute();
        }

        private void OnDestroy()
        {
            if (routeMaterial != null)
                Destroy(routeMaterial);
        }

        private void LateUpdate()
        {
            UpdateRoute();
            if (trenchOrders != null && trenchOrders.BlocksWorldCommands)
            {
                EndLookDrag();
                return;
            }
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                EndLookDrag();
                return;
            }
            Vector2 pointer = mouse.position.ReadValue();
            if (lookSoldier != null)
            {
                if (!mouse.rightButton.isPressed || !lookSoldier.CanTakeGuardMove || lookSoldier.IsMoving
                    || !mapCamera.pixelRect.Contains(pointer)
                    || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
                {
                    EndLookDrag();
                    return;
                }
                if ((pointer - lookDragOrigin).sqrMagnitude >= 16f)
                {
                    Vector3 lookPoint = mapCamera.ScreenToWorldPoint(new Vector3(pointer.x, pointer.y,
                        mapView.transform.position.z - mapCamera.transform.position.z));
                    lookSoldier.SetManualLook(lookPoint - lookSoldier.transform.position);
                }
                return;
            }
            if (!mouse.rightButton.wasPressedThisFrame)
                return;
            if (!mapCamera.pixelRect.Contains(pointer)
                || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
                return;
            Vector3 world = mapCamera.ScreenToWorldPoint(new Vector3(pointer.x, pointer.y,
                mapView.transform.position.z - mapCamera.transform.position.z));
            Vector3Int position = mapView.Tilemap.WorldToCell(world);
            var cell = new Vector2Int(position.x, position.y);
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed))
                ToggleObstacle(cell);
            else
            {
                SoldierView guard = FindGuardAtPointer(pointer);
                if (guard != null)
                {
                    lookSoldier = guard;
                    lookDragOrigin = pointer;
                    SetStatus($"Orientamento di {guard.Data.FullName}.");
                }
                else
                    IssueMove(cell);
            }
        }

        private SoldierView FindGuardAtPointer(Vector2 pointer)
        {
            SoldierView closest = null;
            float best = 14f * 14f;
            foreach (SoldierView soldier in roster.Soldiers)
            {
                if (soldier == null || !soldier.CanTakeGuardMove || soldier.IsMoving
                    || map.Data == null || !map.Data.Fog.IsVisible(soldier.Cell))
                    continue;
                Vector3 screen = mapCamera.WorldToScreenPoint(soldier.transform.position);
                float distance = ((Vector2)screen - pointer).sqrMagnitude;
                if (screen.z > 0f && distance < best)
                {
                    best = distance;
                    closest = soldier;
                }
            }
            return closest;
        }

        private void EndLookDrag()
        {
            if (lookSoldier != null)
                lookSoldier.StopManualLook();
            lookSoldier = null;
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) EndLookDrag();
        }

        public bool IssueMove(Vector2Int destination)
        {
            if (map.Data == null)
                return Reject("La mappa non e ancora pronta.");
            if (!map.Data.Fog.IsExplored(destination))
                return Reject("Destinazione non esplorata: avanza verso il bordo visibile.");
            if (!map.Data.IsWalkable(destination))
                return Reject("Destinazione fuori mappa o bloccata.");

            SoldierView assigned = Selected;
            if (assigned == null || !roster.Soldiers.Contains(assigned))
                return Reject("Seleziona un soldato in Guardia.");
            if (!assigned.CanTakeGuardMove)
                return Reject("Il soldato deve essere in Guardia e libero.");
            if (!assigned.IsMoving && assigned.Cell == destination)
            {
                SetStatus("Il soldato e gia in guardia sulla cella.");
                return true;
            }
            foreach (SoldierView soldier in roster.Soldiers)
            {
                if (soldier == null)
                    continue;
                if (soldier != assigned && soldier.Destination == destination)
                    return Reject("Destinazione gia occupata o prenotata.");
            }
            GridPath path = GridPathfinder.FindNearest(map.Data, new[] { assigned.GuardPathStart }, destination);
            if (path == null)
                return Reject("Nessun percorso raggiungibile verso la destinazione.");

            assigned.SetGuardPath(mapView, path.Cells);
            if (!assigned.IsMoving)
            {
                ClearRoute();
                SetStatus("Il soldato e gia in guardia sulla cella.");
                return true;
            }
            ShowRoute(assigned);
            SetStatus($"{assigned.Data.FullName}\nIn marcia verso ({destination.x}, {destination.y}).");
            return true;
        }

        public bool ToggleObstacle(Vector2Int cell)
        {
            if (map.Data == null || !map.Data.Contains(cell))
                return Reject("Ostacolo fuori dalla mappa.");
            if (!map.Data.Fog.IsVisible(cell))
                return Reject("Ostacolo debug: la cella non e visibile.");
            var kitchen = roster.GetComponent<KitchenSystem>();
            if (kitchen != null && kitchen.HasFacilities && (cell == kitchen.KitchenCell || cell == kitchen.DepotCell))
                return Reject("Non puoi modificare gli edifici con gli ostacoli debug.");
            if (map.Data.TryGetZone(cell, out _))
                return Reject("Non puoi bloccare una cella assegnata a una zona.");
            if (map.Data.IsStorageCell(cell) || map.Data.IsPantryCell(cell))
                return Reject("Non puoi modificare le aree di deposito.");
            if (trenchOrders != null && trenchOrders.Jobs.Count > 0)
                return Reject("Ostacoli debug: annulla o completa prima gli scavi.");
            foreach (SoldierView soldier in roster.Soldiers)
            {
                if (soldier == null)
                    continue;
                if (soldier.IsMoving)
                    return Reject("Ostacoli debug: attendi che tutti i soldati siano fermi.");
                if (soldier.Cell == cell)
                    return Reject("Non puoi bloccare la cella di un soldato.");
            }
            bool walkable = !map.Data.IsWalkable(cell);
            map.Data.SetWalkable(cell, walkable);
            mapView.RefreshWalkability(map.Data, cell);
            SetStatus(walkable ? "Cella nuovamente percorribile." : "Ostacolo debug aggiunto.");
            return true;
        }

        private void ShowRoute(SoldierView soldier)
        {
            ClearRoute();
            routeSoldier = soldier;
            routeSoldier.MovementChanged += HandleArrival;
            if (route == null)
                return;
            UpdateRoute();
            destinationMarker.sprite = mapView.Tilemap.GetSprite(new Vector3Int(soldier.Destination.x, soldier.Destination.y, 0));
            destinationMarker.transform.position = mapView.Tilemap.GetCellCenterWorld(
                new Vector3Int(soldier.Destination.x, soldier.Destination.y, 0));
            destinationMarker.transform.localScale = Vector3.one * 0.55f;
            destinationMarker.enabled = true;
        }

        private void UpdateRoute()
        {
            if (routeSoldier == null || !routeSoldier.IsMoving || route == null)
                return;
            IReadOnlyList<Vector2Int> path = routeSoldier.CurrentPath;
            route.positionCount = path.Count - routeSoldier.NextWaypoint + 1;
            route.SetPosition(0, routeSoldier.transform.position);
            for (int i = routeSoldier.NextWaypoint; i < path.Count; i++)
                route.SetPosition(i - routeSoldier.NextWaypoint + 1,
                    mapView.Tilemap.GetCellCenterWorld(new Vector3Int(path[i].x, path[i].y, 0)));
        }

        private void HandleArrival()
        {
            if (routeSoldier == null || routeSoldier.IsMoving)
                return;
            string name = routeSoldier.Data.FullName;
            bool alive = routeSoldier.IsAlive;
            ClearRoute();
            SetStatus(name + (alive ? "\nDestinazione raggiunta." : "\nMorto."));
        }

        private void HandleSoldierDied(SoldierView soldier)
        {
            if (lookSoldier == soldier)
                EndLookDrag();
            if (routeSoldier == soldier)
                ClearRoute();
            if (Selected == soldier)
            {
                Selected = null;
                SetStatus(soldier.Data.FullName + "\nMorto.");
            }
        }

        private void ClearRoute()
        {
            if (routeSoldier != null)
                routeSoldier.MovementChanged -= HandleArrival;
            routeSoldier = null;
            if (route != null)
                route.positionCount = 0;
            if (destinationMarker != null)
                destinationMarker.enabled = false;
        }

        private void ResetOrders()
        {
            ClearRoute();
            Selected = null;
            SetStatus("Destro: muovi la guardia scelta.\nMaiusc+destro: ostacolo debug.");
        }

        private bool Reject(string message)
        {
            SetStatus(message);
            Debug.LogWarning(message, this);
            return false;
        }

        private void SetStatus(string message)
        {
            Status = message;
            StatusChanged?.Invoke();
        }
    }
}
