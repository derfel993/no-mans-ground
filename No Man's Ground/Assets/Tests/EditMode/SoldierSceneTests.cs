using System.Collections;
using System.Collections.Generic;
using NoMansGround.Editor;
using NoMansGround.Map;
using NoMansGround.Soldiers;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace NoMansGround.Tests
{
    public sealed class SoldierSceneTests
    {
        [Test]
        public void InventoryPanelShowsFullEmptyAndRefilledCanteenWithoutReselectingSoldier()
        {
            EditorSceneManager.OpenScene(MapPrototypeSetup.ScenePath);
            var panel = Object.FindAnyObjectByType<SoldierDetailsPanel>();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            try
            {
                var map = Object.FindAnyObjectByType<MapPrototype>();
                var roster = Object.FindAnyObjectByType<SoldierRoster>();
                map.Regenerate();
                roster.Spawn(map.Data);
                typeof(SoldierDetailsPanel).GetMethod("Awake", flags).Invoke(panel, null);
                typeof(SoldierDetailsPanel).GetMethod("OnEnable", flags).Invoke(panel, null);
                SoldierView soldier = roster.Soldiers[0];
                panel.Select(soldier);
                var canvas = GameObject.Find("Soldier Interface").GetComponent<Canvas>();
                FindButton(canvas, "Inventario").onClick.Invoke();
                Text inventory = canvas.transform.Find("Soldier Details Panel/Inventario content")
                    .GetComponentInChildren<Text>(true);
                Assert.AreEqual("Borraccia piena (acqua) x1", inventory.text);
                soldier.Data.Needs.AdvanceTime(soldier.Data.Needs.ThirstDepletionSeconds * 0.8f + 1f);
                Assert.AreEqual("Borraccia vuota x1", inventory.text);
                Assert.AreEqual(100f, soldier.Data.Needs.Thirst);
                soldier.Data.ReceiveRation();
                Assert.AreEqual("Borraccia piena (acqua) x1", inventory.text);
                Assert.AreSame(soldier, panel.Selected);
            }
            finally
            {
                typeof(SoldierDetailsPanel).GetMethod("OnDisable", flags).Invoke(panel, null);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        [UnityTest]
        public IEnumerator SavedSceneSpawnsSelectsLevelsAndRegeneratesSoldiers()
        {
            EditorSceneManager.OpenScene(MapPrototypeSetup.ScenePath);
            yield return new EnterPlayMode();
            yield return null;
            var roster = Object.FindAnyObjectByType<SoldierRoster>();
            var panel = Object.FindAnyObjectByType<SoldierDetailsPanel>();
            var map = Object.FindAnyObjectByType<MapPrototype>();
            var orders = Object.FindAnyObjectByType<SoldierMovementOrders>();
            var canvas = GameObject.Find("Soldier Interface").GetComponent<Canvas>();
            Assert.IsTrue(canvas.transform.Find("Soldier Panel").gameObject.activeInHierarchy);
            Assert.IsTrue(canvas.transform.Find("Soldier Details Panel").gameObject.activeInHierarchy);
            Assert.IsNotNull(roster);
            Assert.IsNotNull(panel);
            Assert.IsNotNull(map);
            Assert.IsNotNull(map.Data);
            Assert.IsNotNull(orders);
            Assert.AreEqual(1, map.Clock.Day);
            Assert.AreEqual(9, map.Clock.Hour);
            AssertTextExists("Giorno 1 - 09:00");
            double clockBefore = map.Clock.ElapsedSeconds;
            yield return null;
            yield return null;
            Assert.Greater(map.Clock.ElapsedSeconds, clockBefore);
            FindButton(canvas, "2x").onClick.Invoke();
            clockBefore = map.Clock.ElapsedSeconds;
            yield return null;
            Assert.Greater(map.Clock.ElapsedSeconds - clockBefore, Time.unscaledDeltaTime * 1.5d);
            FindButton(canvas, "1x").onClick.Invoke();
            float previousTimeScale = Time.timeScale;
            try
            {
                Time.timeScale = 0f;
                yield return null;
                clockBefore = map.Clock.ElapsedSeconds;
                yield return null;
                yield return null;
                Assert.AreEqual(clockBefore, map.Clock.ElapsedSeconds, "The clock must stop while the game is paused.");
            }
            finally
            {
                Time.timeScale = previousTimeScale;
            }
            map.Clock.Reset();
            map.Clock.AdvanceTime(1500d);
            AssertTextExists("Giorno 2 - 00:00");
            Assert.IsNotNull(Camera.main, "The prototype scene must have an enabled MainCamera.");
            Assert.AreEqual(9, roster.Soldiers.Count);
            var cells = new HashSet<Vector2Int>();
            foreach (SoldierView soldier in roster.Soldiers)
            {
                Assert.IsNotNull(soldier);
                Assert.IsNotNull(soldier.GetComponent<SpriteRenderer>());
                Assert.IsTrue(map.Data.Contains(soldier.Cell));
                Assert.IsFalse(map.Data.IsStorageCell(soldier.Cell));
                Assert.IsFalse(map.Data.IsPantryCell(soldier.Cell));
                Assert.IsTrue(cells.Add(soldier.Cell));
                Assert.That(soldier.Cell.y, Is.InRange(7, 9));
                Assert.That(soldier.Data.Needs.Hunger, Is.InRange(99.9f, 100f));
                Assert.That(soldier.Data.Needs.Thirst, Is.InRange(99.9f, 100f));
                Assert.That(soldier.Data.Needs.PhysicalNeeds, Is.InRange(99.9f, 100f));
                Assert.IsNotNull(soldier.GetComponent<SpriteRenderer>().sprite);
                Vector3 screen = Camera.main.WorldToViewportPoint(soldier.transform.position);
                Assert.That(screen.x, Is.InRange(0f, 1f));
                Assert.That(screen.y, Is.InRange(0f, 1f));
            }
            Assert.AreSame(roster.Soldiers[0], panel.Selected);
            SoldierView selected = roster.Soldiers[1];
            Assert.IsNotNull(selected);
            Assert.IsNotNull(selected.Data);
            panel.Select(selected);
            Assert.IsTrue(selected.IsSelected);
            Assert.IsFalse(roster.Soldiers[0].IsSelected);
            Assert.AreSame(selected, panel.FindAtScreenPosition(Camera.main.WorldToScreenPoint(selected.transform.position)));
            Assert.IsNull(panel.FindAtScreenPosition(Vector2.zero));
            AssertTextExists(selected.Data.FullName);
            AssertTextExists($"Carico massimo: {selected.Data.MaxCarryWeightKg:0.0} kg");
            foreach (string tab in new[] { "Info", "Stato", "Skill", "Inventario" })
                Assert.IsNotNull(FindButton(canvas, tab));
            RectTransform soldierPanel = canvas.transform.Find("Soldier Details Panel").GetComponent<RectTransform>();
            Assert.AreEqual(Vector2.zero, soldierPanel.anchorMin);
            Assert.AreEqual(Vector2.zero, soldierPanel.anchorMax);
            Assert.IsTrue(canvas.transform.Find("Soldier Details Panel/Info content").gameObject.activeSelf);
            FindButton(canvas, "Inventario").onClick.Invoke();
            Transform inventoryPage = canvas.transform.Find("Soldier Details Panel/Inventario content");
            Assert.IsTrue(inventoryPage.gameObject.activeSelf);
            Text inventoryText = inventoryPage.GetComponentInChildren<Text>(true);
            Assert.IsNotNull(inventoryText);
            Assert.AreEqual("Borraccia piena (acqua) x1", inventoryText.text);
            selected.Data.Inventory.Add(ItemType.Pickaxe, 1);
            Assert.AreEqual("Piccone x1\nBorraccia piena (acqua) x1", inventoryText.text);
            Assert.IsTrue(selected.Data.Inventory.TryTake(ItemType.Pickaxe, 1));
            Assert.AreEqual("Borraccia piena (acqua) x1", inventoryText.text);
            FindButton(canvas, "Info").onClick.Invoke();
            SkillProgress strength = selected.Data.GetSkill(SoldierSkill.Strength);
            float initialCarryWeight = selected.Data.MaxCarryWeightKg;
            strength.AddExperience(strength.ExperienceToNextLevel);
            Assert.Greater(selected.Data.MaxCarryWeightKg, initialCarryWeight);
            AssertTextExists($"Carico massimo: {selected.Data.MaxCarryWeightKg:0.0} kg");
            SkillProgress aim = selected.Data.GetSkill(SoldierSkill.Aim);
            int previousLevel = aim.Level;
            aim.AddExperience(aim.ExperienceToNextLevel + 7);
            Assert.AreEqual(previousLevel + 1, aim.Level);
            Assert.AreEqual(7, aim.Experience);
            FindButton(canvas, "Skill").onClick.Invoke();
            Assert.IsTrue(canvas.transform.Find("Soldier Details Panel/Skill content").gameObject.activeSelf);
            AssertTextExists($"Mira - Liv. {aim.Level} ({aim.Value}/100)\nXP 7/{aim.ExperienceToNextLevel}");
            Canvas.ForceUpdateCanvases();
            AssertCompactPanel();
            yield return AssertPanelAtDifferentResolutions();
            float hunger = selected.Data.Needs.Hunger;
            yield return null;
            Assert.Less(selected.Data.Needs.Hunger, hunger);
            Vector2Int goal = selected.Cell + Vector2Int.up * 10;
            selected.RequestDuty(SoldierDuty.Guard);
            Assert.IsTrue(orders.IssueMove(goal));
            SoldierView moving = null;
            foreach (SoldierView soldier in roster.Soldiers)
                if (soldier.IsMoving && soldier.Data.Profession != SoldierProfession.Cook)
                    moving = soldier;
            Assert.IsNotNull(moving);
            panel.Select(moving);
            AssertTextExists($"In marcia: ({goal.x}, {goal.y})\nVelocita: {moving.MovementSpeed:0.00} unita/s");
            FindButton(canvas, "5x").onClick.Invoke();
            Vector3 beforeMove = moving.transform.position;
            yield return null;
            Assert.That(Vector3.Distance(beforeMove, moving.transform.position),
                Is.EqualTo(moving.MovementSpeed * Time.deltaTime).Within(0.01f));
            FindButton(canvas, "1x").onClick.Invoke();
            LineRenderer route = orders.GetComponentInChildren<LineRenderer>();
            Assert.Greater(route.positionCount, 1);
            Vector3 redirectPosition = moving.transform.position;
            goal += Vector2Int.left * 4;
            Assert.IsTrue(orders.IssueMove(goal));
            Assert.AreEqual(redirectPosition, moving.transform.position);
            AssertTextExists($"In marcia: ({goal.x}, {goal.y})\nVelocita: {moving.MovementSpeed:0.00} unita/s");
            Vector3 goalPosition = map.GetComponentInChildren<MapTilemapView>().Tilemap.GetCellCenterWorld(
                new Vector3Int(goal.x, goal.y, 0));
            Assert.AreEqual(goalPosition, route.GetPosition(route.positionCount - 1));
            bool markerFound = false;
            foreach (SpriteRenderer sprite in orders.GetComponentsInChildren<SpriteRenderer>())
                if (sprite.enabled && sprite.transform.position == goalPosition)
                    markerFound = true;
            Assert.IsTrue(markerFound, "The destination marker must follow the replacement path.");
            moving.AdvanceMovement(1000f);
            Assert.IsFalse(moving.IsMoving);
            Assert.AreEqual(goal, moving.Cell);
            Assert.Zero(route.positionCount);
            Assert.IsTrue(orders.Status.Contains("Destinazione raggiunta"));
            panel.Select(selected);
            panel.Select(null);
            Assert.IsFalse(selected.IsSelected);
            Assert.IsNull(panel.Selected);
            Assert.IsTrue(canvas.transform.Find("Soldier Details Panel").gameObject.activeInHierarchy);
            AssertTextExists(map.Clock.Display);
            panel.Select(selected);
            string firstName = roster.Soldiers[0].Data.FullName;
            Assert.IsTrue(orders.IssueMove(new Vector2Int(20, 20)));
            map.Regenerate();
            Assert.AreEqual("Giorno 1 - 09:00", map.Clock.Display);
            AssertTextExists("Giorno 1 - 09:00");
            yield return null;
            Assert.AreEqual(9, roster.Soldiers.Count);
            Assert.AreEqual(9, roster.GetComponentsInChildren<SoldierView>().Length);
            Assert.AreEqual(firstName, roster.Soldiers[0].Data.FullName);
            Assert.AreSame(roster.Soldiers[0], panel.Selected);
            Assert.IsTrue(orders.Status.Contains("Destro"));
            Assert.Zero(route.positionCount);
            foreach (SoldierView soldier in roster.Soldiers)
                if (soldier.Data.Profession != SoldierProfession.Cook)
                    Assert.IsFalse(soldier.IsMoving);
            map.Generation.width = 1;
            map.Generation.height = 16;
            map.Regenerate();
            yield return null;
            cells.Clear();
            foreach (SoldierView soldier in roster.Soldiers)
            {
                Assert.IsTrue(map.Data.Contains(soldier.Cell));
                Assert.IsTrue(cells.Add(soldier.Cell));
            }
            map.Generation.width = 1;
            map.Generation.height = 1;
            LogAssert.Expect(LogType.Warning, "Map has room for only 1 of the requested 8 soldiers.");
            map.Regenerate();
            yield return null;
            Assert.AreEqual(1, roster.Soldiers.Count);
            Assert.AreEqual(Vector2Int.zero, roster.Soldiers[0].Cell);
            yield return new ExitPlayMode();
        }

        private static void AssertCompactPanel()
        {
            var canvas = GameObject.Find("Soldier Interface").GetComponent<Canvas>();
            var panel = canvas.transform.Find("Soldier Panel").GetComponent<RectTransform>();
            var soldierPanel = canvas.transform.Find("Soldier Details Panel").GetComponent<RectTransform>();
            Assert.AreEqual(167f, panel.rect.width, 0.01f);
            Assert.IsNull(canvas.GetComponentInChildren<ScrollRect>());
            var corners = new Vector3[4];
            foreach (Text text in canvas.GetComponentsInChildren<Text>())
            {
                if (!text.gameObject.activeInHierarchy)
                    continue;
                Assert.LessOrEqual(text.preferredHeight, text.rectTransform.rect.height + 0.1f,
                    "Text is truncated: " + text.text);
                RectTransform bounds = text.transform.IsChildOf(soldierPanel) ? soldierPanel : panel;
                text.rectTransform.GetWorldCorners(corners);
                foreach (Vector3 corner in corners)
                {
                    Vector3 local = bounds.InverseTransformPoint(corner);
                    Assert.That(local.x, Is.InRange(bounds.rect.xMin - 0.1f, bounds.rect.xMax + 0.1f), text.text);
                    Assert.That(local.y, Is.InRange(bounds.rect.yMin - 0.1f, bounds.rect.yMax + 0.1f), text.text);
                }
            }
        }

        private static IEnumerator AssertPanelAtDifferentResolutions()
        {
            Canvas canvas = GameObject.Find("Soldier Interface").GetComponent<Canvas>();
            Camera camera = Camera.main;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            foreach (Vector2Int size in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080),
                new Vector2Int(800, 600), new Vector2Int(720, 1280), new Vector2Int(1280, 480) })
            {
                var target = new RenderTexture(size.x, size.y, 24);
                try
                {
                    camera.targetTexture = target;
                    camera.aspect = (float)size.x / size.y;
                    yield return null;
                    yield return null;
                    Canvas.ForceUpdateCanvases();
                    AssertCompactPanel();
                }
                finally
                {
                    camera.targetTexture = null;
                    Object.DestroyImmediate(target);
                }
            }
            camera.ResetAspect();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        private static void AssertTextExists(string value)
        {
            foreach (Text text in Object.FindObjectsByType<Text>())
            {
                if (text.text == value)
                    return;
            }
            Assert.Fail("Missing soldier panel text: " + value);
        }

        private static Button FindButton(Canvas canvas, string label)
        {
            foreach (Button button in canvas.GetComponentsInChildren<Button>())
                if (button.GetComponentInChildren<Text>().text.Contains(label))
                    return button;
            Assert.Fail("No button contains: " + label);
            return null;
        }
    }
}
