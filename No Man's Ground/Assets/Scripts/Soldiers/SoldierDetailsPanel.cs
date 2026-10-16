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

        private Text identity;
        private Text instructions;
        private Text orderStatus;
        private Text activity;
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

        public void Configure(SoldierRoster soldierRoster, Camera camera, SoldierMovementOrders orders = null)
        {
            roster = soldierRoster;
            mapCamera = camera;
            movementOrders = orders;
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
                roster.Rebuilt += HandleRosterRebuilt;
            if (movementOrders != null)
                movementOrders.StatusChanged += RefreshOrderStatus;
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
            if (roster != null)
                roster.Rebuilt -= HandleRosterRebuilt;
            if (movementOrders != null)
                movementOrders.StatusChanged -= RefreshOrderStatus;
            Select(null);
            if (canvasObject != null)
                canvasObject.SetActive(false);
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
            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
                return;
            Vector2 pointer = mouse.position.ReadValue();
            if (!mapCamera.pixelRect.Contains(pointer)
                || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
                return;
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
            if (subscribedMovement != null)
                subscribedMovement.MovementChanged -= Refresh;
            if (Selected != null)
                Selected.SetSelected(false);
            if (selectedData != null)
            {
                foreach (SkillProgress skill in selectedData.Skills)
                    skill.Changed -= Refresh;
            }
            Selected = soldier;
            subscribedMovement = soldier;
            selectedData = soldier != null ? soldier.Data : null;
            if (Selected != null)
            {
                Selected.MovementChanged += Refresh;
                Selected.SetSelected(true);
                foreach (SkillProgress skill in Selected.Data.Skills)
                    skill.Changed += Refresh;
            }
            if (identity != null)
                Refresh();
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
            Select(roster.Soldiers.Count > 0 ? roster.Soldiers[0] : null);
            if (Selected != null && mapCamera.TryGetComponent(out MapCameraController controller))
                controller.FocusOn(Selected.transform.position);
        }

        private void Refresh()
        {
            bool selected = Selected != null;
            identity.text = selected ? Selected.Data.FullName : "Nessun soldato selezionato";
            instructions.text = selected
                ? "Sinistro: ispeziona.\nDestro: ordine condiviso."
                : "Sinistro: scegli un soldato.\nDestro: ordine condiviso.";
            RefreshOrderStatus();
            details.SetActive(selected);
            if (!selected)
                return;

            Vector2Int target = Selected.Destination;
            activity.text = (Selected.IsMoving ? $"In marcia: ({target.x}, {target.y})" : "Disponibile")
                + $"\nVelocita: {Selected.MovementSpeed:0.00} unita/s";
            SoldierNeeds state = Selected.Data.Needs;
            float[] values = { state.Hunger, state.Thirst, state.PhysicalNeeds, state.ThermalState,
                state.Stress, state.Fear, state.Fatigue };
            for (int i = 0; i < needs.Length; i++)
            {
                float severity = Mathf.Abs(values[i]) / 100f;
                Color color = Color.Lerp(new Color(0.35f, 0.70f, 0.43f), new Color(0.85f, 0.32f, 0.24f), severity);
                string label = NeedNames[i] + ": " + values[i].ToString("0") + "/100";
                if (i == 3)
                {
                    label = "Termico: " + (values[i] > 0 ? "+" : "") + values[i].ToString("0")
                        + (values[i] < 0 ? " (freddo)" : values[i] > 0 ? " (caldo)" : " (neutro)");
                    color = values[i] < 0f ? new Color(0.40f, 0.67f, 0.90f) : new Color(0.91f, 0.58f, 0.29f);
                }
                needs[i].Set(label, severity, color);
            }
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

        private void RefreshOrderStatus()
        {
            if (orderStatus != null)
                orderStatus.text = movementOrders != null ? movementOrders.Status : "Movimento non configurato.";
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
            CreateText(content, "SOLDATI", 13, 20f);
            identity = CreateText(content, "", 12, 32f);
            instructions = CreateText(content, "", 10, 28f);
            orderStatus = CreateText(content, "", 10, 48f);
            var detailRect = CreateRect("Details", content);
            var detailLayout = detailRect.gameObject.AddComponent<VerticalLayoutGroup>();
            detailLayout.spacing = 4f;
            detailLayout.childControlHeight = true;
            detailLayout.childControlWidth = true;
            detailLayout.childForceExpandHeight = false;
            details = detailRect.gameObject;
            activity = CreateText(detailRect, "", 10, 28f);
            CreateText(detailRect, "STATO: 0 soddisfatto, 100 critico", 10, 28f);
            needs = new Meter[7];
            for (int i = 0; i < needs.Length; i++)
                needs[i] = CreateMeter(detailRect, 24f);
            CreateText(detailRect, "SKILL - livelli 1-20", 11, 20f);
            skills = new Meter[5];
            for (int i = 0; i < skills.Length; i++)
                skills[i] = CreateMeter(detailRect, 34f);
            CreateText(detailRect, "Stati statici. Nessun job.\nMaiusc+destro: ostacolo debug.", 9, 28f);
            Refresh();
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
