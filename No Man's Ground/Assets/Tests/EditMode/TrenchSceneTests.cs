using System.Collections;
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
    public sealed class TrenchSceneTests
    {
        [UnityTest]
        public IEnumerator ConstructionButtonsToggleModesAndFollowWorkSystemAvailability()
        {
            EditorSceneManager.OpenScene(MapPrototypeSetup.ScenePath);
            yield return new EnterPlayMode();
            yield return null;
            var trenches = Object.FindAnyObjectByType<TrenchWorkOrders>();
            var canvas = GameObject.Find("Soldier Interface").GetComponent<Canvas>();
            var panel = Object.FindAnyObjectByType<SoldierDetailsPanel>();
            panel.SetZoneMode(MapZoneKind.Dump);
            FindButton(canvas, "Legno").onClick.Invoke();
            Assert.AreEqual(DiggingMode.BuildWood, trenches.Mode);
            FindButton(canvas, "Pietra").onClick.Invoke();
            Assert.AreEqual(DiggingMode.BuildStone, trenches.Mode);
            FindButton(canvas, "Porta").onClick.Invoke();
            Assert.AreEqual(DiggingMode.Door, trenches.Mode);
            FindButton(canvas, "Porta").onClick.Invoke();
            Assert.AreEqual(DiggingMode.None, trenches.Mode);
            trenches.enabled = false;
            foreach (string label in new[] { "Legno", "Pietra", "Porta" })
                Assert.IsFalse(FindButton(canvas, label).interactable);
            trenches.enabled = true;
            foreach (string label in new[] { "Legno", "Pietra", "Porta" })
                Assert.IsTrue(FindButton(canvas, label).interactable);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator SceneButtonsProgressCancellationAndRegenerationStayConsistent()
        {
            EditorSceneManager.OpenScene(MapPrototypeSetup.ScenePath);
            yield return new EnterPlayMode();
            yield return null;
            var map = Object.FindAnyObjectByType<MapPrototype>();
            var roster = Object.FindAnyObjectByType<SoldierRoster>();
            var trenches = roster.GetComponent<TrenchWorkOrders>();
            var panel = Object.FindAnyObjectByType<SoldierDetailsPanel>();
            var canvas = GameObject.Find("Soldier Interface").GetComponent<Canvas>();
            var buttons = canvas.GetComponentsInChildren<Button>();
            Assert.AreEqual(22, buttons.Length);
            FindButton(canvas, "Legno").onClick.Invoke();
            Assert.AreEqual(DiggingMode.BuildWood, trenches.Mode);
            FindButton(canvas, "Pietra").onClick.Invoke();
            Assert.AreEqual(DiggingMode.BuildStone, trenches.Mode);
            FindButton(canvas, "Porta").onClick.Invoke();
            Assert.AreEqual(DiggingMode.Door, trenches.Mode);
            FindButton(canvas, "Porta").onClick.Invoke();
            Assert.AreEqual(DiggingMode.None, trenches.Mode);
            FindButton(canvas, "2x").onClick.Invoke();
            Assert.AreEqual(2, Time.timeScale);
            Assert.AreEqual(2, map.SimulationSpeed);
            FindButton(canvas, "10x").onClick.Invoke();
            Assert.AreEqual(10, Time.timeScale);
            Assert.AreEqual(10, map.SimulationSpeed);
            FindButton(canvas, "1x").onClick.Invoke();
            Assert.AreEqual(1, Time.timeScale);
            FindButton(canvas, "Scava").onClick.Invoke();
            Assert.AreEqual(DiggingMode.Dig, trenches.Mode);
            FindButton(canvas, "Annulla scavi").onClick.Invoke();
            Assert.AreEqual(DiggingMode.Cancel, trenches.Mode);
            FindButton(canvas, "Annulla scavi").onClick.Invoke();
            Assert.AreEqual(DiggingMode.None, trenches.Mode);
            FindButton(canvas, "Taglia").onClick.Invoke();
            Assert.AreEqual(DiggingMode.Cut, trenches.Mode);
            FindButton(canvas, "Taglia").onClick.Invoke();
            Assert.AreEqual(DiggingMode.None, trenches.Mode);
            FindButton(canvas, "Mina").onClick.Invoke();
            Assert.AreEqual(DiggingMode.Mine, trenches.Mode);
            FindButton(canvas, "Mina").onClick.Invoke();
            Assert.AreEqual(DiggingMode.None, trenches.Mode);
            FindButton(canvas, "Raccogli").onClick.Invoke();
            Assert.AreEqual(DiggingMode.Collect, trenches.Mode);
            FindButton(canvas, "Raccogli").onClick.Invoke();
            Assert.AreEqual(DiggingMode.None, trenches.Mode);
            SoldierView worker = roster.Soldiers[0];
            FindButton(canvas, "Guardia").onClick.Invoke();
            Assert.AreEqual(SoldierDuty.Guard, worker.Duty);
            AssertTextContains(canvas, "Lavoro: 8 | Guardia: 1");
            FindButton(canvas, "Lavoro").onClick.Invoke();
            Assert.AreEqual(SoldierDuty.Work, worker.Duty);
            AssertTextContains(canvas, "Lavoro: 9 | Guardia: 0");
            Assert.IsTrue(map.Data.StorageStockpile.TryTake(ItemType.Shovel, 1));
            worker.Data.Inventory.Add(ItemType.Shovel, 1);
            Vector2Int target = worker.Cell + Vector2Int.down;
            panel.Select(worker);
            Assert.IsTrue(trenches.Designate(target));
            yield return null;
            Assert.AreSame(worker, trenches.Jobs[0].Worker, "Runtime updates must assign queued work automatically.");
            trenches.Tick(1f);
            Assert.AreSame(worker, trenches.Jobs[0].Worker);
            Assert.IsTrue(worker.HasWorkAssignment);
            AssertTextContains(canvas, "Scavo:");
            AssertTextContains(canvas, "Attivi: 1");
            yield return null;
            Assert.Greater(trenches.Jobs[0].Progress, 0f);
            Assert.IsTrue(trenches.Cancel(target));
            Assert.IsTrue(worker.IsAvailable);
            AssertTextContains(canvas, "Disponibile");
            trenches.Designate(target);
            trenches.Tick(0.01f);
            trenches.Tick(10f);
            Assert.AreEqual(TerrainKind.Trench, map.Data[target.x, target.y]);
            Assert.Zero(trenches.Jobs.Count);
            FindButton(canvas, "Installa scala").onClick.Invoke();
            Assert.AreEqual(DiggingMode.Ladder, trenches.Mode);
            Assert.IsTrue(trenches.DesignateLadder(target));
            yield return null;
            Assert.AreSame(worker, trenches.Jobs[0].Worker);
            Assert.IsFalse(map.Data.HasLadder(target));
            trenches.Tick(1f);
            AssertTextContains(canvas, "Scala:");
            trenches.Tick(10f);
            Assert.IsTrue(map.Data.HasLadder(target));
            Assert.IsTrue(worker.IsAvailable);
            var view = map.GetComponentInChildren<MapTilemapView>();
            Vector2Int inside = target + Vector2Int.left;
            view.CompleteTrench(map.Data, inside);
            var orders = roster.GetComponent<SoldierMovementOrders>();
            worker.RequestDuty(SoldierDuty.Guard);
            Assert.IsTrue(orders.IssueMove(inside));
            Assert.IsTrue(worker.IsMoving);
            CollectionAssert.Contains(worker.CurrentPath, target);
            worker.AdvanceMovement(100f);
            Assert.AreEqual(inside, worker.Cell);
            GridPath exit = GridPathfinder.FindNearest(map.Data, new[] { inside }, target + Vector2Int.up);
            CollectionAssert.Contains(exit.Cells, target);
            worker.BeginMove(view, exit.Cells);
            worker.AdvanceMovement(100f);
            Assert.AreEqual(target + Vector2Int.up, worker.Cell);
            worker.RequestDuty(SoldierDuty.Work);
            trenches.Designate(new Vector2Int(50, 10));
            trenches.Tick(0.01f);
            trenches.SetMode(DiggingMode.Dig);
            map.Regenerate();
            Assert.Zero(trenches.Jobs.Count);
            Assert.AreEqual(DiggingMode.None, trenches.Mode);
            Assert.AreEqual(9, roster.WorkCount);
            Assert.Zero(roster.GuardCount);
            Assert.AreNotEqual(TerrainKind.Trench, map.Data[target.x, target.y]);
            Assert.IsFalse(map.Data.HasLadder(target));
            yield return null;
            foreach (SpriteRenderer sprite in roster.GetComponentsInChildren<SpriteRenderer>())
                Assert.AreNotEqual("Dig designation", sprite.name, "Work markers must be removed on regeneration.");
            trenches.Designate(roster.Soldiers[0].Cell + Vector2Int.down);
            trenches.Tick(0.01f);
            worker = trenches.Jobs[0].Worker;
            trenches.enabled = false;
            Assert.IsFalse(worker.HasWorkAssignment);
            Assert.Zero(trenches.Jobs.Count);
            Assert.IsFalse(FindButton(canvas, "Scava").interactable);
            trenches.SetMode(DiggingMode.Dig);
            Assert.AreEqual(DiggingMode.None, trenches.Mode);
            Assert.IsFalse(trenches.BlocksWorldCommands);
            trenches.enabled = true;
            Assert.IsTrue(FindButton(canvas, "Scava").interactable);
            trenches.Designate(new Vector2Int(50, 10));
            trenches.Tick(0.01f);
            roster.enabled = false;
            map.Regenerate();
            Assert.Zero(trenches.Jobs.Count, "Regeneration clears work even when the roster is disabled.");
            yield return new ExitPlayMode();
        }

        private static void AssertTextContains(Canvas canvas, string fragment)
        {
            foreach (Text text in canvas.GetComponentsInChildren<Text>())
                if (text.text.Contains(fragment))
                    return;
            Assert.Fail("No UI text contains: " + fragment);
        }

        private static Button FindButton(Canvas canvas, string label)
        {
            foreach (Button button in canvas.GetComponentsInChildren<Button>())
                if (button.GetComponentInChildren<Text>().text.IndexOf(label, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return button;
            Assert.Fail("No button contains: " + label);
            return null;
        }
    }
}
