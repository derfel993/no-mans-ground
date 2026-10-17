using NoMansGround.Map;
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

        private Text clockText;
        private Text identity;
        private Text instructions;
        private Text orderStatus;
        private Text activity;
        private Text diggingStatus;
        private Text diggingButton;
        private Text cancelButton;
        private Text ladderButton;
        private Text mealStatus;
        private Text facilityStatus;
        private Text dutyCounts;
        private Text dutyStatus;
        private Button guardButton;
        private Button workButton;
        private Button[] speedButtons;
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
        }

        private void Awake()
        {
            if (roster == null || mapCamera == null)
            {
                Debug.LogError("SoldierDetailsPanel requires a roster and a map camera.", this);
                enabled = false;
                return;
            }
            BuildInterface();
        }

        private void OnEnable()
        {
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
            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
                return;
            Vector2 pointer = mouse.position.ReadValue();
            if (!mapCamera.pixelRect.Contains(pointer)
                || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
                return;
            if (kitchen != null && kitchen.TrySelectAt(mapCamera, pointer, out bool depot))
                SelectFacility(depot);
            else
                Select(FindAtScreenPosition(pointer));
        }

        public SoldierView FindAtScreenPosition(Vector2 pointer)
        {
            SoldierView closest = null;
            float distance = 14f * 14f;
            foreach (SoldierView soldier in roster.Soldiers)
            {
                if (soldier == null || !soldier.gameObject.activeInHierarchy)
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

        private void RefreshFacility()
        {
            if (!facilitySelected || facilityStatus == null || kitchen == null)
                return;
            identity.text = depotSelected ? "DEPOSITO VIVERI" : "CUCINA";
            instructions.text = "Sinistro: ispeziona.\nRancio: ogni giorno alle 13.";
            facilityStatus.gameObject.SetActive(true);
            facilityStatus.text = depotSelected
                ? $"Viveri: {kitchen.Stock.Provisions}/1000\nConsumo: 10 per razione."
                : $"Razioni pronte: {kitchen.Stock.ReadyRations}\nViveri: {kitchen.Stock.Provisions}/1000\n"
                    + $"Razione: {kitchen.Stock.Progress:P0}\nIn consegna: {kitchen.CarriedRations}"
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
            if (facilityStatus != null)
                facilityStatus.gameObject.SetActive(false);
            identity.text = selected ? Selected.Data.FullName : "Nessun soldato selezionato";
            instructions.text = selected
                ? "Sinistro: ispeziona.\nDestro: muovi solo in Guardia."
                : "Sinistro: scegli un soldato.\nDestro: muovi solo in Guardia.";
            RefreshOrderStatus();
            details.SetActive(selected);
            if (!selected)
                return;
            dutyStatus.text = Selected.GuardRequested ? "Lavoro -> Guardia a fine job"
                : Selected.Duty == SoldierDuty.Guard ? "Stato: Guardia" : "Stato: Lavoro";
            guardButton.interactable = Selected.Data.Profession != SoldierProfession.Cook
                && Selected.Duty != SoldierDuty.Guard && !Selected.GuardRequested;
            workButton.interactable = Selected.Duty != SoldierDuty.Work || Selected.GuardRequested;

            Vector2Int target = Selected.Destination;
            activity.text = (Selected.IsMoving ? $"In marcia: ({target.x}, {target.y})"
                : Selected.HasWorkAssignment
                    ? $"{(Selected.Data.Profession == SoldierProfession.Cook ? "Rancio" : Selected.WorkKind == TrenchJobKind.Ladder ? "Scala" : "Scavo")}: {Selected.DiggingProgress:P0}"
                    : Selected.Duty == SoldierDuty.Guard ? "In guardia" : "Disponibile")
                + $"\nVelocita: {Selected.MovementSpeed:0.00} unita/s";
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
            string profession = Selected.Data.Profession == SoldierProfession.Cook ? "Cuoco"
                : Selected.Data.Profession == SoldierProfession.Engineer ? "Genio" : "Soldato";
            mealStatus.text = profession + (state.WaitingForRations
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

        private void RefreshOrderStatus()
        {
            if (orderStatus != null)
                orderStatus.text = movementOrders != null ? movementOrders.Status : "Movimento non configurato.";
        }

        private void RefreshDigging()
        {
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
            cancelButton.text = trenchOrders.Mode == DiggingMode.Cancel ? "ANNULLA [X] - attivo" : "Annulla scavi [X]";
            ladderButton.text = trenchOrders.Mode == DiggingMode.Ladder ? "SCALA [L] - attivo" : "Installa scala [L]";
            diggingButton.GetComponentInParent<Button>().interactable = trenchOrders.isActiveAndEnabled;
            cancelButton.GetComponentInParent<Button>().interactable = trenchOrders.isActiveAndEnabled;
            ladderButton.GetComponentInParent<Button>().interactable = trenchOrders.isActiveAndEnabled;
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
                speedButtons = new Button[3];
                speedButtons[0] = CreateSpeedButton(speedRow, 1);
                speedButtons[1] = CreateSpeedButton(speedRow, 2);
                speedButtons[2] = CreateSpeedButton(speedRow, 5);
                RefreshSpeedButtons();
            }
            CreateText(content, "SOLDATI", 13, 20f);
            dutyCounts = CreateText(content, "", 10, 20f);
            RefreshDutyCounts();
            identity = CreateText(content, "", 12, 32f);
            instructions = CreateText(content, "", 10, 28f);
            orderStatus = CreateText(content, "", 10, 48f);
            facilityStatus = CreateText(content, "", 10, 156f);
            facilityStatus.gameObject.SetActive(false);
            if (trenchOrders != null)
            {
                diggingButton = CreateButton(content, "Scava [T]", DiggingMode.Dig);
                cancelButton = CreateButton(content, "Annulla scavi [X]", DiggingMode.Cancel);
                ladderButton = CreateButton(content, "Installa scala [L]", DiggingMode.Ladder);
                diggingStatus = CreateText(content, "", 10, 66f);
                RefreshDigging();
            }
            var detailRect = CreateRect("Details", content);
            var detailLayout = detailRect.gameObject.AddComponent<VerticalLayoutGroup>();
            detailLayout.spacing = 4f;
            detailLayout.childControlHeight = true;
            detailLayout.childControlWidth = true;
            detailLayout.childForceExpandHeight = false;
            details = detailRect.gameObject;
            dutyStatus = CreateText(detailRect, "", 10, 28f);
            var dutyRow = CreateRect("Duty controls", detailRect);
            dutyRow.gameObject.AddComponent<LayoutElement>().preferredHeight = 22f;
            var dutyLayout = dutyRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            dutyLayout.spacing = 4f;
            dutyLayout.childForceExpandWidth = true;
            guardButton = CreateDutyButton(dutyRow, "Guardia", SoldierDuty.Guard);
            workButton = CreateDutyButton(dutyRow, "Lavoro", SoldierDuty.Work);
            activity = CreateText(detailRect, "", 10, 28f);
            mealStatus = CreateText(detailRect, "", 10, 28f);
            CreateText(detailRect, "Riserve: 100 pieno, 0 esaurito\nStress/paura/fatica: 100 critico", 10, 28f);
            needs = new Meter[7];
            for (int i = 0; i < needs.Length; i++)
                needs[i] = CreateMeter(detailRect, 24f);
            CreateText(detailRect, "SKILL - livelli 1-20", 11, 20f);
            skills = new Meter[5];
            for (int i = 0; i < skills.Length; i++)
                skills[i] = CreateMeter(detailRect, 34f);
            CreateText(detailRect, "T: scava, L: scala, X: annulla.\nEsc/destro: esci dalla modalita.", 9, 28f);
            Refresh();
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
            int[] speeds = { 1, 2, 5 };
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
