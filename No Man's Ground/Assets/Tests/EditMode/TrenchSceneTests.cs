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
            Assert.AreEqual(2, buttons.Length);
            buttons[0].onClick.Invoke();
            Assert.AreEqual(DiggingMode.Dig, trenches.Mode);
            buttons[1].onClick.Invoke();
            Assert.AreEqual(DiggingMode.Cancel, trenches.Mode);
            buttons[1].onClick.Invoke();
            Assert.AreEqual(DiggingMode.None, trenches.Mode);
            SoldierView worker = roster.Soldiers[0];
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
            trenches.Designate(new Vector2Int(50, 10));
            trenches.Tick(0.01f);
            trenches.SetMode(DiggingMode.Dig);
            map.Regenerate();
            Assert.Zero(trenches.Jobs.Count);
            Assert.AreEqual(DiggingMode.None, trenches.Mode);
            Assert.AreNotEqual(TerrainKind.Trench, map.Data[target.x, target.y]);
            yield return null;
            foreach (SpriteRenderer sprite in roster.GetComponentsInChildren<SpriteRenderer>())
                Assert.AreNotEqual("Dig designation", sprite.name, "Work markers must be removed on regeneration.");
            trenches.Designate(roster.Soldiers[0].Cell + Vector2Int.down);
            trenches.Tick(0.01f);
            worker = trenches.Jobs[0].Worker;
            trenches.enabled = false;
            Assert.IsFalse(worker.HasWorkAssignment);
            Assert.Zero(trenches.Jobs.Count);
            Assert.IsFalse(buttons[0].interactable);
            trenches.SetMode(DiggingMode.Dig);
            Assert.AreEqual(DiggingMode.None, trenches.Mode);
            Assert.IsFalse(trenches.BlocksWorldCommands);
            trenches.enabled = true;
            Assert.IsTrue(buttons[0].interactable);
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
    }
}
