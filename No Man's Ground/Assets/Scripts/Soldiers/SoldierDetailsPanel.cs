using NoMansGround.Map;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace NoMansGround.Soldiers
{
    public sealed class SoldierDetailsPanel : MonoBehaviour
    {
        private static readonly string[] SkillNames = { "Mira", "Coraggio", "Forza", "Resistenza", "Velocita" };
        private static readonly string[] NeedNames =
            { "Fame", "Sete", "Bisogni fisiologici", "Freddo / caldo", "Stress", "Paura", "Stanchezza" };

        [SerializeField] private SoldierRoster roster;
        [SerializeField] private Camera mapCamera;
        [SerializeField] private SoldierMovementOrders movementOrders;
        [SerializeField] private TrenchWorkOrders trenchOrders;
        [SerializeField] private KitchenSystem kitchen;
        [SerializeField] private MapPrototype gameMap;
        [SerializeField] private MapTilemapView mapView;

        private Text clockText;
        private Text identity;
        private Text instructions;
        private Text orderStatus;
        private Text activity;
        private Text carryCapacity;
        private Text inventoryStatus;
        private Text diggingStatus;
        private Text diggingButton;
        private Text cutButton;
        private Text mineButton;
        private Text collectButton;
        private Text cancelButton;
        private Text ladderButton;
        private Text buildingStatus;
        private Text[] buildingButtons;
        private Text mealStatus;
        private Text facilityStatus;
        private Text dutyCounts;
        private Text dutyStatus;
        private Text taskStatus;
        private Dropdown taskDropdown;
        private GameObject automaticTransportRow;
        private Toggle automaticTransportToggle;
        private Text zoneStatus;
        private Text roomDesignationButton;
        private Text roomStatus;
        private Button guardButton;
        private Button workButton;
        private Button[] zoneButtons;
        private Button[] speedButtons;
        private Button[] soldierTabButtons;
        private Dropdown roomSpecializationDropdown;
        private GameObject[] soldierTabPages;
        private bool facilitySelected;
        private bool depotSelected;
        private SoldierView subscribedMovement;
        private GameObject details;
        private Meter[] needs;
        private Meter[] skills;
        private Font font;
        private GameObject canvasObject;
        private GameObject ownedEventSystem;
        private SoldierData selectedData;
        private RectTransform panelRect;
        private RectTransform contentRect;
        private int activeSoldierTab;
        private readonly List<SpriteRenderer> zonePreview = new List<SpriteRenderer>();
        private MapZoneKind? activeZone;
        private bool zoneDragging;
        private Vector2Int zoneDragStart;
        private bool roomDesignationActive;
        private bool roomDragging;
        private Vector2Int roomDragStart;
        private RoomSpecialization selectedRoomSpecialization;

        public SoldierView Selected { get; private set; }

        public void Configure(SoldierRoster soldierRoster, Camera camera, SoldierMovementOrders orders = null,
            TrenchWorkOrders trenches = null, KitchenSystem kitchenSystem = null, MapPrototype prototype = null)
        {
            roster = soldierRoster;
            mapCamera = camera;
            movementOrders = orders;
            trenchOrders = trenches;
            kitchen = kitchenSystem;
            gameMap = prototype;
            mapView = prototype != null ? prototype.GetComponentInChildren<MapTilemapView>() : null;
        }

        private void Awake()
        {
            if (mapView == null && gameMap != null)
                mapView = gameMap.GetComponentInChildren<MapTilemapView>();
            if (roster == null || mapCamera == null)
            {
                Debug.LogError("SoldierDetailsPanel requires a roster and a map camera.", this);
                enabled = false;
                return;
            }
            EnsureInterface();
        }

        private void OnEnable()
        {
            EnsureInterface();
            if (canvasObject != null)
                canvasObject.SetActive(true);
            if (roster != null)
            {
                roster.Rebuilt += HandleRosterRebuilt;
                roster.DutiesChanged += RefreshDutyCounts;
            }
            RefreshDutyCounts();
            if (gameMap != null)
            {
                gameMap.Clock.Changed += RefreshClock;
                gameMap.SimulationSpeedChanged += RefreshSpeedButtons;
            }
            RefreshClock();
            RefreshSpeedButtons();
            if (movementOrders != null)
                movementOrders.StatusChanged += RefreshOrderStatus;
            if (trenchOrders != null)
                trenchOrders.Changed += RefreshDigging;
            if (kitchen != null)
                kitchen.Changed += RefreshFacility;
            RefreshDigging();
            if (canvasObject != null && roster != null)
                HandleRosterRebuilt();
        }

        private void EnsureInterface()
        {
            if (canvasObject != null || roster == null || mapCamera == null)
                return;
            GameObject staleCanvas = GameObject.Find("Soldier Interface");
            if (staleCanvas != null)
                Destroy(staleCanvas);
            BuildInterface();
        }

        private void Start()
        {
            if (Selected == null)
                HandleRosterRebuilt();
        }

        private void OnDisable()
        {
            if (gameMap != null)
            {
                gameMap.Clock.Changed -= RefreshClock;
                gameMap.SimulationSpeedChanged -= RefreshSpeedButtons;
            }
            if (roster != null)
            {
                roster.Rebuilt -= HandleRosterRebuilt;
                roster.DutiesChanged -= RefreshDutyCounts;
            }
            if (movementOrders != null)
                movementOrders.StatusChanged -= RefreshOrderStatus;
            if (trenchOrders != null)
                trenchOrders.Changed -= RefreshDigging;
            if (kitchen != null)
                kitchen.Changed -= RefreshFacility;
            Select(null);
            if (canvasObject != null)
                canvasObject.SetActive(false);
            activeZone = null;
            zoneDragging = false;
            roomDesignationActive = false;
            roomDragging = false;
            ClearZonePreview();
        }

        private void RefreshClock()
        {
            if (clockText != null && gameMap != null)
                clockText.text = gameMap.Clock.Display;
        }

        private void OnDestroy()
        {
            if (canvasObject != null)
                Destroy(canvasObject);
            if (ownedEventSystem != null)
                Destroy(ownedEventSystem);
        }

        private void LateUpdate()
        {
            FitContent();
            if (trenchOrders != null && trenchOrders.BlocksWorldCommands)
                return;
            if (roomDesignationActive)
            {
                HandleRoomDesignationInput();
                return;
            }
            if (activeZone.HasValue)
            {
                HandleZoneInput();
                return;
            }
            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
                return;
            Vector2 pointer = mouse.position.ReadValue();
            if (!mapCamera.pixelRect.Contains(pointer)
                || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
                return;
            SoldierView soldierOnCell = FindAtMapCell(pointer);
            if (soldierOnCell != null)
            {
                Select(soldierOnCell);
                return;
            }
            if (kitchen != null && kitchen.TrySelectAt(mapCamera, pointer, out bool depot))
                SelectFacility(depot);
            else
                Select(FindAtScreenPosition(pointer));
        }

        private SoldierView FindAtMapCell(Vector2 pointer)
        {
            if (gameMap == null || gameMap.Data == null || mapView == null)
                return null;
            Vector3 world = mapCamera.ScreenToWorldPoint(new Vector3(pointer.x, pointer.y,
                mapView.transform.position.z - mapCamera.transform.position.z));
            Vector3Int mapCell = mapView.Tilemap.WorldToCell(world);
            var cell = new Vector2Int(mapCell.x, mapCell.y);
            if (!gameMap.Data.Contains(cell) || !gameMap.Data.Fog.IsVisible(cell))
                return null;
            foreach (SoldierView soldier in roster.Soldiers)
                if (soldier != null && soldier.gameObject.activeInHierarchy && soldier.Cell == cell)
                    return soldier;
            return null;
        }

        public SoldierView FindAtScreenPosition(Vector2 pointer)
        {
            SoldierView closest = null;
            float distance = 14f * 14f;
            foreach (SoldierView soldier in roster.Soldiers)
            {
                if (soldier == null || !soldier.gameObject.activeInHierarchy)
                    continue;
                if (gameMap != null && gameMap.Data != null && !gameMap.Data.Fog.IsVisible(soldier.Cell))
                    continue;
                Vector3 screen = mapCamera.WorldToScreenPoint(soldier.transform.position);
                float candidate = ((Vector2)screen - pointer).sqrMagnitude;
                if (screen.z > 0f && candidate < distance)
                {
                    closest = soldier;
                    distance = candidate;
                }
            }
            return closest;
        }

        public void Select(SoldierView soldier)
        {
            facilitySelected = false;
            if (subscribedMovement != null)
                subscribedMovement.MovementChanged -= Refresh;
            if (Selected != null)
                Selected.SetSelected(false);
            if (selectedData != null)
            {
                selectedData.Needs.Changed -= RefreshNeeds;
                selectedData.Inventory.Changed -= RefreshInventory;
                selectedData.CarryWeightChanged -= RefreshCarryWeight;
                foreach (SkillProgress skill in selectedData.Skills)
                    skill.Changed -= Refresh;
            }
            Selected = soldier;
            if (movementOrders != null)
                movementOrders.Selected = soldier;
            subscribedMovement = soldier;
            selectedData = soldier != null ? soldier.Data : null;
            if (Selected != null)
            {
                selectedData.Needs.Changed += RefreshNeeds;
                selectedData.Inventory.Changed += RefreshInventory;
                selectedData.CarryWeightChanged += RefreshCarryWeight;
                Selected.MovementChanged += Refresh;
                Selected.SetSelected(true);
                foreach (SkillProgress skill in Selected.Data.Skills)
                    skill.Changed += Refresh;
            }
            if (identity != null)
                Refresh();
        }

        public void SelectFacility(bool depot = false)
        {
            Select(null);
            facilitySelected = true;
            depotSelected = depot;
            RefreshFacility();
        }

        public void SetZoneMode(MapZoneKind? kind)
        {
            if (kind.HasValue && roomDesignationActive)
                SetRoomDesignationMode(false);
            activeZone = activeZone == kind ? null : kind;
            zoneDragging = false;
            ClearZonePreview();
            if (activeZone.HasValue && trenchOrders != null)
                trenchOrders.SetMode(DiggingMode.None);
            UpdateZoneControls();
        }

        private void SetRoomDesignationMode(bool active)
        {
            roomDesignationActive = active;
            roomDragging = false;
            ClearZonePreview();
            if (active)
            {
                activeZone = null;
                zoneDragging = false;
                if (trenchOrders != null)
                    trenchOrders.SetMode(DiggingMode.None);
            }
            UpdateZoneControls();
            UpdateRoomDesignationControls();
        }

        private void HandleZoneInput()
        {
            Keyboard keyboard = Keyboard.current;
            if ((keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                || (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame))
            {
                SetZoneMode(null);
                return;
            }
            Mouse mouse = Mouse.current;
            if (mouse == null)
                return;
            bool valid = TryGetMapPointerCell(mouse.position.ReadValue(), out Vector2Int cell);
            if (mouse.leftButton.wasPressedThisFrame && valid)
            {
                zoneDragging = true;
                zoneDragStart = cell;
            }
            if (zoneDragging && valid)
                ShowZonePreview(zoneDragStart, cell);
            if (!zoneDragging || !mouse.leftButton.wasReleasedThisFrame)
                return;
            zoneDragging = false;
            ClearZonePreview();
            if (!valid || !activeZone.HasValue || gameMap == null || gameMap.Data == null)
                return;
            if (gameMap.Data.TryDesignateZone(activeZone.Value, zoneDragStart, cell, out int changed))
            {
                mapView.RenderZones(gameMap.Data);
                if (kitchen != null)
                    kitchen.HandleZonesChanged();
                if (zoneStatus != null)
                    zoneStatus.text = changed > 0
                        ? $"{ZoneName(activeZone.Value)}: {changed} celle aggiunte."
                        : $"{ZoneName(activeZone.Value)}: area gia presente.";
            }
            else if (zoneStatus != null)
                zoneStatus.text = "Area non valida: fuori mappa, bloccata o sovrapposta.";
        }

        private bool TryGetMapPointerCell(Vector2 pointer, out Vector2Int cell)
        {
            cell = default;
            if (gameMap == null || gameMap.Data == null || mapView == null
                || !mapCamera.pixelRect.Contains(pointer)
                || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
                return false;
            Vector3 world = mapCamera.ScreenToWorldPoint(new Vector3(pointer.x, pointer.y,
                mapView.transform.position.z - mapCamera.transform.position.z));
            Vector3Int position = mapView.Tilemap.WorldToCell(world);
            cell = new Vector2Int(position.x, position.y);
            return gameMap.Data.Contains(cell);
        }

        private void ShowZonePreview(Vector2Int from, Vector2Int to)
        {
            ShowRectanglePreview(from, to, MapTilemapView.ZoneColor(activeZone.Value));
        }

        private void ShowRoomPreview(Vector2Int from, Vector2Int to)
        {
            ShowRectanglePreview(from, to, MapTilemapView.SpecializationColor(selectedRoomSpecialization));
        }

        private void ShowRectanglePreview(Vector2Int from, Vector2Int to, Color color)
        {
            int minX = Mathf.Min(from.x, to.x);
            int maxX = Mathf.Max(from.x, to.x);
            int minY = Mathf.Min(from.y, to.y);
            int maxY = Mathf.Max(from.y, to.y);
            int index = 0;
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    if (index == zonePreview.Count)
                    {
                        var previewObject = new GameObject("Area preview", typeof(SpriteRenderer));
                        previewObject.transform.SetParent(transform, false);
                        zonePreview.Add(previewObject.GetComponent<SpriteRenderer>());
                    }
                    SpriteRenderer renderer = zonePreview[index++];
                    var position = new Vector3Int(x, y, 0);
                    renderer.sprite = mapView.Tilemap.GetSprite(position);
                    renderer.color = new Color(color.r, color.g, color.b, 0.48f);
                    renderer.sortingOrder = 3;
                    renderer.transform.position = mapView.Tilemap.GetCellCenterWorld(position);
                    renderer.enabled = renderer.sprite != null;
                }
            for (int i = index; i < zonePreview.Count; i++)
                zonePreview[i].enabled = false;
        }

        private void ClearZonePreview()
        {
            foreach (SpriteRenderer renderer in zonePreview)
                if (renderer != null)
                    renderer.enabled = false;
        }

        private void UpdateZoneControls()
        {
            if (zoneButtons == null)
                return;
            MapZoneKind[] kinds =
                { MapZoneKind.Dump, MapZoneKind.MaterialsDepot, MapZoneKind.Cemetery };
            for (int i = 0; i < zoneButtons.Length; i++)
                zoneButtons[i].GetComponent<Image>().color = activeZone == kinds[i]
                    ? MapTilemapView.ZoneColor(kinds[i]) : new Color(0.26f, 0.32f, 0.22f);
            if (zoneStatus != null && activeZone.HasValue)
                zoneStatus.text = $"{ZoneName(activeZone.Value)}: trascina un rettangolo. Esc/destro: esci.";
            else if (zoneStatus != null)
                zoneStatus.text = DescribeZones();
        }

        private void HandleRoomDesignationInput()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if ((keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                || (mouse != null && mouse.rightButton.wasPressedThisFrame))
            {
                SetRoomDesignationMode(false);
                return;
            }
            if (mouse == null)
                return;
            Vector2 pointer = mouse.position.ReadValue();
            bool valid = TryGetMapPointerCell(pointer, out Vector2Int cell);
            if (mouse.leftButton.wasPressedThisFrame && valid)
            {
                SoldierView clickedSoldier = FindAtScreenPosition(pointer);
                if (clickedSoldier != null)
                {
                    SetRoomDesignationMode(false);
                    Select(clickedSoldier);
                    return;
                }
                roomDragging = true;
                roomDragStart = cell;
            }
            if (roomDragging && valid)
                ShowRoomPreview(roomDragStart, cell);
            if (!roomDragging || !mouse.leftButton.wasReleasedThisFrame)
                return;

            roomDragging = false;
            ClearZonePreview();
            if (!valid || gameMap == null || gameMap.Data == null)
            {
                roomStatus.text = "Disegno annullato: rilascio fuori dalla mappa.";
                return;
            }
            if (gameMap.Data.TryDesignateSpecializedRoom(selectedRoomSpecialization,
                roomDragStart, cell, out SpecializedRoom room))
            {
                mapView.RenderSpecializedRooms(gameMap.Data);
                RefreshDigging();
                roomStatus.text = $"{RoomSpecializationName(room.Specialization)}: area {room.Bounds.width} x {room.Bounds.height}.";
            }
            else
                roomStatus.text = "Area non valida: serve un rettangolo interno a un edificio completato, senza sovrapposizioni.";
        }

        private void UpdateRoomDesignationControls()
        {
            if (roomDesignationButton == null)
                return;
            roomDesignationButton.text = roomDesignationActive ? "DISEGNA STANZA - attivo" : "Disegna stanza";
            roomDesignationButton.GetComponentInParent<Button>().GetComponent<Image>().color = roomDesignationActive
                ? new Color(0.48f, 0.58f, 0.34f) : new Color(0.26f, 0.32f, 0.22f);
            if (roomStatus == null)
                return;
            roomStatus.text = roomDesignationActive
                ? $"{RoomSpecializationName(selectedRoomSpecialization)}: trascina nell'interno. Esc/destro: esci."
                : DescribeSpecializedRooms();
        }

        private string DescribeSpecializedRooms()
        {
            if (gameMap == null || gameMap.Data == null || gameMap.Data.SpecializedRooms.Count == 0)
                return "Nessuna stanza specializzata.";
            var counts = new int[System.Enum.GetValues(typeof(RoomSpecialization)).Length];
            foreach (SpecializedRoom room in gameMap.Data.SpecializedRooms)
                counts[(int)room.Specialization]++;
            return $"Comando {counts[0]} | Telegrafo {counts[1]} | Cucina {counts[2]}\n"
                + $"Armeria {counts[3]} | Ricovero {counts[4]} | Ospedale {counts[5]}";
        }

        private static string RoomSpecializationName(RoomSpecialization specialization)
        {
            switch (specialization)
            {
                case RoomSpecialization.Command: return "Comando";
                case RoomSpecialization.TelegraphStation: return "Stazione Telegrafo";
                case RoomSpecialization.Kitchen: return "Cucina";
                case RoomSpecialization.Armory: return "Armeria";
                case RoomSpecialization.Shelter: return "Ricovero";
                case RoomSpecialization.Hospital: return "Ospedale";
                default: return "Stanza";
            }
        }

        private static string ZoneName(MapZoneKind kind)
        {
            switch (kind)
            {
                case MapZoneKind.Dump: return "DISCARICA";
                case MapZoneKind.MaterialsDepot: return "DEPOSITO MATERIALI";
                case MapZoneKind.Cemetery: return "CIMITERO";
                default: return "ZONA";
            }
        }

        private void RefreshFacility()
        {
            if (!facilitySelected || facilityStatus == null || kitchen == null)
                return;
            identity.text = depotSelected ? "DISPENSA" : "CUCINA";
            instructions.text = "Sinistro: ispeziona.\nRancio: ogni giorno alle 13.";
            facilityStatus.gameObject.SetActive(true);
            facilityStatus.text = depotSelected
                ? $"Viveri: {kitchen.Stock.Provisions}/1000\nIn trasporto: {kitchen.CarriedProvisions}"
                : $"Razioni pronte: {kitchen.Stock.ReadyRations}\nViveri in cucina: {kitchen.Stock.KitchenProvisions}"
                    + $"\nViveri in deposito: {kitchen.Stock.Provisions}/1000\nRazione: {kitchen.Stock.Progress:P0}"
                    + $"\nIn trasporto: {kitchen.CarriedProvisions}\nIn consegna: {kitchen.CarriedRations}"
                    + $"\nIn attesa: {kitchen.PendingMeals}\n{kitchen.Status}";
        }

        [ContextMenu("Debug/Add 100 Aim XP to Selected Soldier")]
        public void AddDebugAimExperience()
        {
            if (Selected == null)
            {
                Debug.LogWarning("Select a soldier before awarding debug experience.", this);
                return;
            }
            Selected.Data.GetSkill(SoldierSkill.Aim).AddExperience(100);
        }

        private void HandleRosterRebuilt()
        {
            RefreshDutyCounts();
            RefreshDigging();
            Select(roster.Soldiers.Count > 0 ? roster.Soldiers[0] : null);
            if (Selected != null && mapCamera.TryGetComponent(out MapCameraController controller))
                controller.FocusOn(Selected.transform.position);
        }

        private void RefreshDutyCounts()
        {
            if (dutyCounts != null && roster != null)
                dutyCounts.text = $"Lavoro: {roster.WorkCount} | Guardia: {roster.GuardCount}";
        }

        private void Refresh()
        {
            bool selected = Selected != null;
            if (taskDropdown != null)
            {
                taskDropdown.interactable = selected;
                if (selected)
                    taskDropdown.SetValueWithoutNotify((int)(Selected.RequestedTask ?? Selected.Data.Task));
                taskStatus.text = selected ? (Selected.RequestedTask.HasValue
                    ? $"{SoldierTasks.DisplayName(Selected.Data.Task)} -> {SoldierTasks.DisplayName(Selected.RequestedTask.Value)}\n{Selected.TaskStatus}"
                    : Selected.TaskStatus) : string.Empty;
            }
            if (automaticTransportRow != null)
            {
                bool haulerSelected = selected
                    && (Selected.RequestedTask ?? Selected.Data.Task) == SoldierTask.Hauler;
                automaticTransportRow.SetActive(haulerSelected);
                automaticTransportToggle.SetIsOnWithoutNotify(selected && Selected.AutomaticTransportEnabled);
                automaticTransportToggle.interactable = selected;
            }
            if (facilityStatus != null)
                facilityStatus.gameObject.SetActive(false);
            identity.text = selected ? Selected.Data.FullName : "Nessun soldato selezionato";
            instructions.text = selected
                ? "Sinistro: ispeziona.\nDestro: muovi solo in Guardia."
                : "Sinistro: scegli un soldato.\nDestro: muovi solo in Guardia.";
            RefreshOrderStatus();
            details.SetActive(true);
            if (!selected)
            {
                dutyStatus.text = string.Empty;
                guardButton.interactable = false;
                workButton.interactable = false;
                activity.text = string.Empty;
                carryCapacity.text = string.Empty;
                inventoryStatus.text = string.Empty;
                mealStatus.text = string.Empty;
                foreach (Meter need in needs)
                    need.Set(string.Empty, 0f, Color.clear);
                foreach (Meter skill in skills)
                    skill.Set(string.Empty, 0f, Color.clear);
                return;
            }
            dutyStatus.text = Selected.GuardRequested ? "Lavoro -> Guardia a fine job"
                : Selected.Duty == SoldierDuty.Guard ? "Stato: Guardia" : "Stato: Lavoro";
            guardButton.interactable = Selected.Data.Task != SoldierTask.Cook
                && Selected.Duty != SoldierDuty.Guard && !Selected.GuardRequested;
            workButton.interactable = Selected.Duty != SoldierDuty.Work || Selected.GuardRequested;

            Vector2Int target = Selected.Destination;
            activity.text = (Selected.IsChangingTaskEquipment || Selected.IsReturningTaskTool
                || Selected.IsEvacuatingBuilding ? Selected.TaskStatus
                : Selected.IsMoving ? $"In marcia: ({target.x}, {target.y})"
                : Selected.HasWorkAssignment
                    ? $"{(Selected.Data.Task == SoldierTask.Cook ? "Rancio" : Selected.WorkKind == TrenchJobKind.BuildWall || Selected.WorkKind == TrenchJobKind.BuildDoor ? "Costruzione" : Selected.WorkKind == TrenchJobKind.Ladder ? "Scala" : Selected.WorkKind == TrenchJobKind.CutTree ? "Taglio albero" : Selected.WorkKind == TrenchJobKind.MineRock ? "Mina roccia" : Selected.WorkKind == TrenchJobKind.CollectItem ? "Raccolta" : "Scavo")}: {Selected.DiggingProgress:P0}"
                    : Selected.Duty == SoldierDuty.Guard ? "In guardia" : "Disponibile")
                + $"\nVelocita: {Selected.MovementSpeed:0.00} unita/s";
            RefreshCarryWeight();
            RefreshInventory();
            RefreshNeeds();
            for (int i = 0; i < skills.Length; i++)
            {
                SkillProgress skill = Selected.Data.GetSkill((SoldierSkill)i);
                string progress = skill.IsMaximumLevel ? "Livello massimo"
                    : $"XP {skill.Experience}/{skill.ExperienceToNextLevel}";
                skills[i].Set($"{SkillNames[i]} - Liv. {skill.Level} ({skill.Value}/100)\n{progress}",
                    skill.IsMaximumLevel ? 1f : (float)skill.Experience / skill.ExperienceToNextLevel,
                    new Color(0.75f, 0.70f, 0.44f));
            }
        }

        private void RefreshNeeds()
        {
            if (Selected == null || needs == null)
                return;
            SoldierNeeds state = Selected.Data.Needs;
            mealStatus.text = SoldierTasks.DisplayName(Selected.Data.Task) + (state.WaitingForRations
                ? " | Attende rancio\nStress fame: +5/min" : " | Rancio non richiesto");
            float[] values = { state.Hunger, state.Thirst, state.PhysicalNeeds, state.ThermalState,
                state.Stress, state.Fear, state.Fatigue };
            for (int i = 0; i < needs.Length; i++)
            {
                float fill = Mathf.Abs(values[i]) / 100f;
                float severity = i < 3 ? 1f - fill : fill;
                Color color = Color.Lerp(new Color(0.35f, 0.70f, 0.43f), new Color(0.85f, 0.32f, 0.24f), severity);
                string label = NeedNames[i] + ": " + values[i].ToString("0") + "/100";
                if (i == 3)
                {
                    label = "Termico: " + (values[i] > 0 ? "+" : "") + values[i].ToString("0")
                        + (values[i] < 0 ? " (freddo)" : values[i] > 0 ? " (caldo)" : " (neutro)");
                    color = values[i] < 0f ? new Color(0.40f, 0.67f, 0.90f) : new Color(0.91f, 0.58f, 0.29f);
                }
                needs[i].Set(label, fill, color);
            }
        }

        private void RefreshInventory()
        {
            RefreshCarryWeight();
            if (inventoryStatus == null)
                return;
            inventoryStatus.text = string.Empty;
            if (Selected == null)
                return;
            foreach (ItemType type in new[] { ItemType.Shovel, ItemType.Axe, ItemType.Pickaxe, ItemType.Rations,
                ItemType.Wood, ItemType.BuildingStone, ItemType.FullCanteen, ItemType.EmptyCanteen,
                ItemType.ConstructionHammer })
            {
                int count = Selected.Data.Inventory.Count(type);
                if (count > 0)
                    inventoryStatus.text += $"{ItemCatalog.Get(type).DisplayName} x{count}\n";
            }
            inventoryStatus.text = inventoryStatus.text.TrimEnd();
        }

        private void RefreshCarryWeight()
        {
            if (carryCapacity != null && Selected != null)
                carryCapacity.text = $"Carico: {Selected.Data.CarryWeightKg:0.0} / {Selected.Data.MaxCarryWeightKg:0.0} kg";
        }

        private void RefreshOrderStatus()
        {
            if (orderStatus != null)
                orderStatus.text = movementOrders != null ? movementOrders.Status : "Movimento non configurato.";
        }

        private void RefreshDigging()
        {
            if ((activeZone.HasValue || roomDesignationActive)
                && trenchOrders != null && trenchOrders.Mode != DiggingMode.None)
            {
                activeZone = null;
                roomDesignationActive = false;
                zoneDragging = false;
                roomDragging = false;
                ClearZonePreview();
                UpdateZoneControls();
                UpdateRoomDesignationControls();
            }
            if (buildingStatus != null && gameMap != null && gameMap.Data != null)
            {
                int complete = 0;
                foreach (BuildingRoom room in gameMap.Data.Buildings)
                    if (room.IsComplete) complete++;
                buildingStatus.text = $"Costruzioni: {complete}/{gameMap.Data.Buildings.Count} | Aree: {gameMap.Data.SpecializedRooms.Count}\n"
                    + $"Rifugi galleria: {gameMap.Data.ClosedTunnelShelterCount}\nGallerie aperte: {gameMap.Data.OpenTunnelCount}\n"
                    + $"Legna: {gameMap.Data.StorageStockpile.Count(ItemType.Wood)} | Pietra: {gameMap.Data.StorageStockpile.Count(ItemType.BuildingStone)}";
            }
            if (diggingStatus == null || trenchOrders == null)
                return;
            int blocked = 0;
            int active = 0;
            foreach (TrenchJob job in trenchOrders.Jobs)
            {
                if (job.Worker != null) active++;
                if (job.Status == "Accesso bloccato o occupato"
                    || job.Status == "Attesa: cella o percorso occupato") blocked++;
            }
            diggingStatus.text = $"Lavori: {trenchOrders.Jobs.Count} | Attivi: {active}\nBloccati: {blocked}\n"
                + trenchOrders.Status;
                diggingButton.text = trenchOrders.Mode == DiggingMode.Dig ? "SCAVA [T] - attivo" : "Scava [T]";
                cutButton.text = trenchOrders.Mode == DiggingMode.Cut ? "TAGLIA [A] - attivo" : "Taglia [A]";
                mineButton.text = trenchOrders.Mode == DiggingMode.Mine ? "MINA [M] - attivo" : "Mina [M]";
                collectButton.text = trenchOrders.Mode == DiggingMode.Collect ? "RACCOGLI [C] - attivo" : "Raccogli [C]";
                cancelButton.text = trenchOrders.Mode == DiggingMode.Cancel ? "ANNULLA [X] - attivo" : "Annulla scavi [X]";
                ladderButton.text = trenchOrders.Mode == DiggingMode.Ladder ? "SCALA [L] - attivo" : "Installa scala [L]";
            if (buildingButtons != null)
            {
                buildingButtons[0].text = trenchOrders.Mode == DiggingMode.BuildWallWood ? "MURO LEGNO - attivo" : "Muro Legno";
                buildingButtons[1].text = trenchOrders.Mode == DiggingMode.BuildWallStone ? "MURO PIETRA - attivo" : "Muro Pietra";
                buildingButtons[2].text = trenchOrders.Mode == DiggingMode.Door ? "PORTA - attivo" : "Porta";
            }
            diggingButton.GetComponentInParent<Button>().interactable = trenchOrders.isActiveAndEnabled;
            cutButton.GetComponentInParent<Button>().interactable = trenchOrders.isActiveAndEnabled;
            mineButton.GetComponentInParent<Button>().interactable = trenchOrders.isActiveAndEnabled;
            collectButton.GetComponentInParent<Button>().interactable = trenchOrders.isActiveAndEnabled;
            cancelButton.GetComponentInParent<Button>().interactable = trenchOrders.isActiveAndEnabled;
            ladderButton.GetComponentInParent<Button>().interactable = trenchOrders.isActiveAndEnabled;
            if (buildingButtons != null)
                foreach (Text button in buildingButtons)
                    button.GetComponentInParent<Button>().interactable = trenchOrders.isActiveAndEnabled;
        }

        private void BuildInterface()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (EventSystem.current == null && FindAnyObjectByType<EventSystem>() == null)
                ownedEventSystem = new GameObject("Soldier UI Input", typeof(EventSystem), typeof(InputSystemUIInputModule));

            canvasObject = new GameObject("Soldier Interface", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            var panel = CreateRect("Soldier Panel", canvasObject.transform);
            panelRect = panel;
            panel.anchorMin = new Vector2(1f, 0f);
            panel.anchorMax = Vector2.one;
            panel.pivot = new Vector2(1f, 0.5f);
            panel.offsetMin = new Vector2(-179f, 12f);
            panel.offsetMax = new Vector2(-12f, -12f);
            panel.gameObject.AddComponent<Image>().color = new Color(0.09f, 0.11f, 0.09f, 0.96f);
            var content = CreateRect("Content", panel);
            contentRect = content;
            content.anchorMin = Vector2.up;
            content.anchorMax = Vector2.up;
            content.pivot = Vector2.up;
            content.anchoredPosition = new Vector2(8f, -8f);
            content.sizeDelta = new Vector2(151f, 0f);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 4f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            if (gameMap != null)
            {
                clockText = CreateText(content, gameMap.Clock.Display, 12, 22f);
                var speedRow = CreateRect("Time speed controls", content);
                speedRow.gameObject.AddComponent<LayoutElement>().preferredHeight = 22f;
                var speedLayout = speedRow.gameObject.AddComponent<HorizontalLayoutGroup>();
                speedLayout.spacing = 3f;
                speedLayout.childForceExpandWidth = true;
                speedButtons = new Button[5];
                speedButtons[0] = CreateSpeedButton(speedRow, 0);
                speedButtons[1] = CreateSpeedButton(speedRow, 1);
                speedButtons[2] = CreateSpeedButton(speedRow, 2);
                speedButtons[3] = CreateSpeedButton(speedRow, 5);
                speedButtons[4] = CreateSpeedButton(speedRow, 10);
                RefreshSpeedButtons();
            }
            CreateText(content, "SOLDATI", 13, 20f);
            dutyCounts = CreateText(content, "", 10, 20f);
            RefreshDutyCounts();
            facilityStatus = CreateText(content, "", 10, 156f);
            facilityStatus.gameObject.SetActive(false);
            if (trenchOrders != null)
            {
                diggingButton = CreateButton(content, "Scava [T]", DiggingMode.Dig);
                cutButton = CreateButton(content, "Taglia [A]", DiggingMode.Cut);
                mineButton = CreateButton(content, "Mina [M]", DiggingMode.Mine);
                collectButton = CreateButton(content, "Raccogli [C]", DiggingMode.Collect);
                cancelButton = CreateButton(content, "Annulla scavi [X]", DiggingMode.Cancel);
                ladderButton = CreateButton(content, "Installa scala [L]", DiggingMode.Ladder);
                CreateText(content, "COSTRUZIONI", 12, 20f);
                buildingButtons = new[]
                {
                    CreateButton(content, "Muro Legno", DiggingMode.BuildWallWood),
                    CreateButton(content, "Muro Pietra", DiggingMode.BuildWallStone),
                    CreateButton(content, "Porta", DiggingMode.Door)
                };
                buildingStatus = CreateText(content, "", 9, 56f);
                diggingStatus = CreateText(content, "", 10, 66f);
                RefreshDigging();
            }
            if (gameMap != null)
            {
                CreateText(content, "STANZE", 12, 20f);
                roomSpecializationDropdown = CreateRoomSpecializationDropdown(content);
                roomDesignationButton = CreateRoomDesignationButton(content);
                roomStatus = CreateText(content, "", 9, 38f);
                UpdateRoomDesignationControls();
            }
            if (gameMap != null)
            {
                CreateText(content, "ZONE", 12, 20f);
                var zoneRow = CreateRect("Zone controls", content);
                zoneRow.gameObject.AddComponent<LayoutElement>().preferredHeight = 24f;
                var zoneLayout = zoneRow.gameObject.AddComponent<HorizontalLayoutGroup>();
                zoneLayout.spacing = 2f;
                zoneLayout.childForceExpandWidth = true;
                zoneButtons = new[]
                {
                    CreateZoneButton(zoneRow, "Discarica", MapZoneKind.Dump),
                    CreateZoneButton(zoneRow, "Materiali", MapZoneKind.MaterialsDepot),
                    CreateZoneButton(zoneRow, "Cimitero", MapZoneKind.Cemetery)
                };
                zoneStatus = CreateText(content, DescribeZones(), 9, 32f);
                UpdateZoneControls();
            }
            BuildSoldierPanel(canvasObject.transform);
            Refresh();
        }

        private void BuildSoldierPanel(Transform parent)
        {
            RectTransform panel = CreateRect("Soldier Details Panel", parent);
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.zero;
            panel.pivot = Vector2.zero;
            panel.anchoredPosition = new Vector2(12f, 12f);
            panel.sizeDelta = new Vector2(380f, 300f);
            panel.gameObject.AddComponent<Image>().color = new Color(0.09f, 0.11f, 0.09f, 0.96f);
            details = panel.gameObject;

            RectTransform tabs = CreateRect("Soldier Tabs", panel);
            tabs.anchorMin = new Vector2(0f, 1f);
            tabs.anchorMax = new Vector2(1f, 1f);
            tabs.pivot = new Vector2(0.5f, 1f);
            tabs.anchoredPosition = new Vector2(0f, -8f);
            tabs.sizeDelta = new Vector2(-16f, 30f);
            var tabLayout = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
            tabLayout.spacing = 4f;
            tabLayout.childForceExpandWidth = true;
            tabLayout.childForceExpandHeight = true;
            soldierTabButtons = new[]
            {
                CreateSoldierTabButton(tabs, "Info", 0),
                CreateSoldierTabButton(tabs, "Stato", 1),
                CreateSoldierTabButton(tabs, "Skill", 2),
                CreateSoldierTabButton(tabs, "Inventario", 3)
            };

            soldierTabPages = new[]
            {
                CreateSoldierTabPage(panel, "Info content"),
                CreateSoldierTabPage(panel, "Stato content"),
                CreateSoldierTabPage(panel, "Skill content"),
                CreateSoldierTabPage(panel, "Inventario content")
            };

            Transform info = soldierTabPages[0].transform;
            identity = CreateText(info, "", 12, 24f);
            instructions = CreateText(info, "", 10, 22f);
            dutyStatus = CreateText(info, "", 10, 18f);
            var dutyRow = CreateRect("Duty controls", info);
            dutyRow.gameObject.AddComponent<LayoutElement>().preferredHeight = 22f;
            var dutyLayout = dutyRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            dutyLayout.spacing = 4f;
            dutyLayout.childForceExpandWidth = true;
            guardButton = CreateDutyButton(dutyRow, "Guardia", SoldierDuty.Guard);
            workButton = CreateDutyButton(dutyRow, "Lavoro", SoldierDuty.Work);
            taskDropdown = CreateTaskDropdown(info);
            automaticTransportRow = CreateAutomaticTransportToggle(info);
            taskStatus = CreateText(info, "", 9, 26f);
            activity = CreateText(info, "", 10, 24f);
            carryCapacity = CreateText(info, "", 10, 20f);
            orderStatus = CreateText(info, "", 10, 28f);

            Transform state = soldierTabPages[1].transform;
            needs = new Meter[7];
            for (int i = 0; i < needs.Length; i++)
                needs[i] = CreateMeter(state, 24f);
            mealStatus = CreateText(state, "", 10, 28f);

            Transform skill = soldierTabPages[2].transform;
            skills = new Meter[5];
            for (int i = 0; i < skills.Length; i++)
                skills[i] = CreateMeter(skill, 34f);

            inventoryStatus = CreateText(soldierTabPages[3].transform, "", 10, 24f);

            SetSoldierTab(0);
        }

        private GameObject CreateSoldierTabPage(Transform parent, string name)
        {
            RectTransform page = CreateRect(name, parent);
            page.anchorMin = Vector2.zero;
            page.anchorMax = Vector2.one;
            page.offsetMin = new Vector2(10f, 10f);
            page.offsetMax = new Vector2(-10f, -46f);
            var layout = page.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 3f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return page.gameObject;
        }

        private Button CreateSoldierTabButton(Transform parent, string label, int tabIndex)
        {
            var rect = CreateRect(label, parent);
            var image = rect.gameObject.AddComponent<Image>();
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => SetSoldierTab(tabIndex));
            Text text = CreateText(rect, label, 10, 0f);
            Stretch(text.rectTransform, 1f);
            return button;
        }

        private void SetSoldierTab(int tabIndex)
        {
            activeSoldierTab = Mathf.Clamp(tabIndex, 0, 3);
            if (soldierTabPages == null || soldierTabButtons == null)
                return;
            for (int i = 0; i < soldierTabPages.Length; i++)
            {
                soldierTabPages[i].SetActive(i == activeSoldierTab);
                soldierTabButtons[i].GetComponent<Image>().color = i == activeSoldierTab
                    ? new Color(0.48f, 0.58f, 0.34f) : new Color(0.26f, 0.32f, 0.22f);
            }
        }

        private Text CreateButton(Transform parent, string label, DiggingMode mode)
        {
            var rect = CreateRect(label, parent);
            rect.gameObject.AddComponent<LayoutElement>().preferredHeight = 22f;
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.26f, 0.32f, 0.22f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => trenchOrders.SetMode(
                trenchOrders.Mode == mode ? DiggingMode.None : mode));
            Text text = CreateText(rect, label, 10, 0f);
            Stretch(text.rectTransform, 2f);
            return text;
        }

        private Dropdown CreateTaskDropdown(Transform parent)
        {
            Dropdown dropdown = CreateOptionDropdown(parent, "Compito", "Scelta compito");
            foreach (SoldierTask task in System.Enum.GetValues(typeof(SoldierTask)))
                dropdown.options.Add(new Dropdown.OptionData(SoldierTasks.DisplayName(task)));
            dropdown.onValueChanged.AddListener(value =>
            {
                if (Selected != null) Selected.RequestTask((SoldierTask)value);
            });
            dropdown.RefreshShownValue();
            return dropdown;
        }

        private GameObject CreateAutomaticTransportToggle(Transform parent)
        {
            var row = CreateRect("Trasporto Automatico", parent);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 20f;
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            var box = CreateRect("Automatic transport checkbox", row);
            box.gameObject.AddComponent<LayoutElement>().preferredWidth = 16f;
            var background = box.gameObject.AddComponent<Image>();
            background.color = new Color(0.26f, 0.32f, 0.22f);
            automaticTransportToggle = box.gameObject.AddComponent<Toggle>();
            automaticTransportToggle.targetGraphic = background;
            var check = CreateRect("Checked", box);
            check.anchorMin = new Vector2(0.5f, 0.5f);
            check.anchorMax = new Vector2(0.5f, 0.5f);
            check.sizeDelta = new Vector2(8f, 8f);
            automaticTransportToggle.graphic = check.gameObject.AddComponent<Image>();
            automaticTransportToggle.onValueChanged.AddListener(enabled =>
            {
                if (Selected != null)
                    Selected.SetAutomaticTransport(enabled);
            });
            CreateText(row, "Trasporto Automatico", 10, 0f);
            return row.gameObject;
        }

        private Dropdown CreateRoomSpecializationDropdown(Transform parent)
        {
            Dropdown dropdown = CreateOptionDropdown(parent, "Tipo", "Tipo stanza");
            foreach (RoomSpecialization specialization in System.Enum.GetValues(typeof(RoomSpecialization)))
                dropdown.options.Add(new Dropdown.OptionData(RoomSpecializationName(specialization)));
            dropdown.onValueChanged.AddListener(value =>
            {
                selectedRoomSpecialization = (RoomSpecialization)value;
                UpdateRoomDesignationControls();
            });
            dropdown.RefreshShownValue();
            return dropdown;
        }

        private Dropdown CreateOptionDropdown(Transform parent, string titleText, string dropdownName)
        {
            var row = CreateRect(dropdownName, parent);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 24f;
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childForceExpandWidth = false;
            Text title = CreateText(row, titleText, 10, 24f);
            title.GetComponent<LayoutElement>().preferredWidth = 48f;
            var rect = CreateRect(dropdownName, row);
            rect.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var background = rect.gameObject.AddComponent<Image>();
            background.color = new Color(0.26f, 0.32f, 0.22f);
            var dropdown = rect.gameObject.AddComponent<Dropdown>();
            dropdown.targetGraphic = background;
            Text caption = CreateText(rect, "", 10, 0f);
            Stretch(caption.rectTransform, 4f);
            caption.rectTransform.offsetMax = new Vector2(-20f, -4f);
            dropdown.captionText = caption;
            Text arrow = CreateText(rect, "v", 10, 0f);
            arrow.alignment = TextAnchor.MiddleRight;
            Stretch(arrow.rectTransform, 5f);
            var template = CreateRect("Template", rect);
            template.anchorMin = Vector2.zero;
            template.anchorMax = Vector2.right;
            template.pivot = Vector2.up;
            template.anchoredPosition = new Vector2(0f, -2f);
            template.sizeDelta = new Vector2(0f, 144f);
            template.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.16f, 0.12f);
            var scroll = template.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            var viewport = CreateRect("Viewport", template);
            Stretch(viewport, 0f);
            viewport.gameObject.AddComponent<Image>();
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var content = CreateRect("Content", viewport);
            content.anchorMin = Vector2.up;
            content.anchorMax = Vector2.one;
            content.pivot = Vector2.up;
            content.sizeDelta = new Vector2(0f, 24f);
            var item = CreateRect("Item", content);
            Stretch(item, 0f);
            var itemBackground = item.gameObject.AddComponent<Image>();
            itemBackground.color = new Color(0.26f, 0.32f, 0.22f);
            var toggle = item.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = itemBackground;
            var check = CreateRect("Selected", item);
            check.anchorMin = new Vector2(0f, 0.5f);
            check.anchorMax = new Vector2(0f, 0.5f);
            check.anchoredPosition = new Vector2(8f, 0f);
            check.sizeDelta = new Vector2(5f, 5f);
            toggle.graphic = check.gameObject.AddComponent<Image>();
            Text label = CreateText(item, "", 10, 0f);
            Stretch(label.rectTransform, 4f);
            label.rectTransform.offsetMin = new Vector2(18f, 4f);
            scroll.viewport = viewport;
            scroll.content = content;
            dropdown.template = template;
            dropdown.itemText = label;
            template.gameObject.SetActive(false);
            return dropdown;
        }

        private Text CreateRoomDesignationButton(Transform parent)
        {
            var rect = CreateRect("Disegna stanza", parent);
            rect.gameObject.AddComponent<LayoutElement>().preferredHeight = 22f;
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.26f, 0.32f, 0.22f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => SetRoomDesignationMode(!roomDesignationActive));
            Text text = CreateText(rect, "Disegna stanza", 10, 0f);
            Stretch(text.rectTransform, 2f);
            return text;
        }

        private Button CreateDutyButton(Transform parent, string label, SoldierDuty duty)
        {
            var rect = CreateRect(label, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.26f, 0.32f, 0.22f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => { if (Selected != null) Selected.RequestDuty(duty); });
            Text text = CreateText(rect, label, 10, 0f);
            Stretch(text.rectTransform, 2f);
            return button;
        }

        private Button CreateZoneButton(Transform parent, string label, MapZoneKind kind)
        {
            var rect = CreateRect(label, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.26f, 0.32f, 0.22f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => SetZoneMode(kind));
            Text text = CreateText(rect, label, 8, 0f);
            Stretch(text.rectTransform, 1f);
            return button;
        }

        private string DescribeZones()
        {
            if (gameMap == null || gameMap.Data == null)
                return "Nessuna zona designata.";
            return $"Discarica: {(gameMap.Data.HasZone(MapZoneKind.Dump) ? "attiva" : "assente")}\n"
                + $"Deposito: {(gameMap.Data.HasZone(MapZoneKind.MaterialsDepot) ? "attivo" : "assente")} | "
                + $"Dispensa: {(gameMap.Data.HasPantryArea ? "attiva" : "assente")} | "
                + $"Cimitero: {(gameMap.Data.HasZone(MapZoneKind.Cemetery) ? "attivo" : "assente")}";
        }

        private Button CreateSpeedButton(Transform parent, int multiplier)
        {
            string label = multiplier + "x";
            var rect = CreateRect(label, parent);
            var image = rect.gameObject.AddComponent<Image>();
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => gameMap.SetSimulationSpeed(multiplier));
            Text text = CreateText(rect, label, 10, 0f);
            Stretch(text.rectTransform, 2f);
            return button;
        }

        private void RefreshSpeedButtons(int speed = -1)
        {
            if (speedButtons == null || gameMap == null)
                return;
            int selectedSpeed = speed < 0 ? gameMap.SimulationSpeed : speed;
            int[] speeds = { 0, 1, 2, 5, 10 };
            for (int i = 0; i < speedButtons.Length; i++)
                speedButtons[i].GetComponent<Image>().color = speeds[i] == selectedSpeed
                    ? new Color(0.48f, 0.58f, 0.34f)
                    : new Color(0.26f, 0.32f, 0.22f);
        }

        private void FitContent()
        {
            if (contentRect == null)
                return;
            float height = LayoutUtility.GetPreferredHeight(contentRect);
            if (height > 0f)
                contentRect.localScale = Vector3.one * Mathf.Min(1f, (panelRect.rect.height - 16f) / height);
        }

        private Meter CreateMeter(Transform parent, float height)
        {
            var row = CreateRect("Meter", parent);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            Text label = CreateText(row, "", 10, 0f);
            Stretch(label.rectTransform, 0f);
            label.rectTransform.offsetMin = new Vector2(0f, 6f);
            var track = CreateRect("Track", row);
            track.anchorMin = Vector2.zero;
            track.anchorMax = new Vector2(1f, 0f);
            track.sizeDelta = new Vector2(0f, 3f);
            track.anchoredPosition = new Vector2(0f, 1.5f);
            track.gameObject.AddComponent<Image>().color = new Color(0.22f, 0.25f, 0.21f);
            var fill = CreateRect("Fill", track);
            Stretch(fill, 0f);
            var image = fill.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            return new Meter(label, fill, image);
        }

        private Text CreateText(Transform parent, string value, int size, float height)
        {
            var rect = CreateRect("Label", parent);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.color = new Color(0.91f, 0.92f, 0.85f);
            text.raycastTarget = false;
            text.alignment = TextAnchor.MiddleLeft;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            rect.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            return text;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect, float margin)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.one * margin;
            rect.offsetMax = -Vector2.one * margin;
        }

        private sealed class Meter
        {
            private readonly Text label;
            private readonly RectTransform fill;
            private readonly Image image;

            public Meter(Text text, RectTransform rect, Image fillImage)
            {
                label = text;
                fill = rect;
                image = fillImage;
            }

            public void Set(string value, float fraction, Color color)
            {
                label.text = value;
                fill.anchorMax = new Vector2(fraction, 1f);
                image.color = color;
            }
        }
    }
}
