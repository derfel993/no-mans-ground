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
    public sealed class KitchenSceneTests
    {
        [UnityTest]
        public IEnumerator SceneCookProducesKitchenSelectionShowsStockAndRegenerationResets()
        {
            EditorSceneManager.OpenScene(MapPrototypeSetup.ScenePath);
            yield return new EnterPlayMode();
            yield return null;
            var kitchen = Object.FindAnyObjectByType<KitchenSystem>();
            var roster = Object.FindAnyObjectByType<SoldierRoster>();
            var panel = Object.FindAnyObjectByType<SoldierDetailsPanel>();
            var map = Object.FindAnyObjectByType<MapPrototype>();
            Assert.IsTrue(kitchen.HasFacilities);
            Assert.IsNotNull(kitchen.Cook);
            Assert.IsTrue(kitchen.Cook.IsMoving || kitchen.Cook.HasWorkAssignment);
            kitchen.Cook.AdvanceMovement(100f);
            float scale = Time.timeScale;
            try
            {
                Time.timeScale = 0f;
                yield return null;
                var stock = kitchen.Stock;
                float before = stock.Progress;
                yield return null;
                Assert.AreEqual(before, stock.Progress);
                kitchen.Tick(30f);
                Assert.AreEqual(1, stock.ReadyRations);
                Assert.AreEqual(990, stock.Provisions);
                panel.SelectFacility();
                AssertText("Razioni pronte: 1");
                panel.SelectFacility(true);
                AssertText("Viveri: 990/1000");
                var camera = Camera.main;
                var view = map.GetComponentInChildren<MapTilemapView>();
                Vector2 pointer = camera.WorldToScreenPoint(view.Tilemap.GetCellCenterWorld(
                    new Vector3Int(kitchen.KitchenCell.x, kitchen.KitchenCell.y, 0)));
                Assert.IsTrue(kitchen.TrySelectAt(camera, pointer, out bool depot));
                Assert.IsFalse(depot);
                var soldier = roster.Soldiers[0];
                panel.Select(soldier);
                soldier.Data.Needs.AdvanceTime(soldier.Data.Needs.HungerDepletionSeconds * 0.66f);
                Assert.IsTrue(soldier.Data.Needs.WaitingForRations);
                AssertText("Attende rancio");
                Assert.IsTrue(soldier.CanTakeEngineeringWork);
                var orders = roster.GetComponent<TrenchWorkOrders>();
                orders.Designate(soldier.Cell + Vector2Int.down);
                orders.Tick(1f);
                Assert.IsNotNull(orders.Jobs[0].Worker);
                map.Regenerate();
                Assert.AreEqual(1000, kitchen.Stock.Provisions);
                Assert.Zero(kitchen.Stock.ReadyRations);
                Assert.AreEqual(9, roster.Soldiers.Count);
                Assert.IsFalse(roster.Soldiers[0].Data.Needs.WaitingForRations);
                kitchen.Tick(1f);
                kitchen.Cook.AdvanceMovement(100f);
                kitchen.Tick(15f);
                float progress = kitchen.Stock.Progress;
                kitchen.enabled = false;
                Assert.IsFalse(kitchen.Cook.HasWorkAssignment);
                kitchen.enabled = true;
                Assert.AreEqual(progress, kitchen.Stock.Progress);
                kitchen.Tick(1f);
                kitchen.Tick(15f);
                Assert.AreEqual(1, kitchen.Stock.ReadyRations);
                kitchen.enabled = false;
                map.Regenerate();
                kitchen.enabled = true;
                Assert.IsNotNull(kitchen.Cook);
                Assert.IsTrue(kitchen.HasFacilities);
                Assert.IsFalse(map.Data.IsWalkable(kitchen.KitchenCell));
                Assert.Zero(kitchen.Stock.ReadyRations);
                Assert.AreEqual(1000, kitchen.Stock.Provisions);
            }
            finally { Time.timeScale = scale; }
            yield return new ExitPlayMode();
        }

        private static void AssertText(string fragment)
        {
            foreach (Text text in GameObject.Find("Soldier Interface").GetComponentsInChildren<Text>())
                if (text.text.Contains(fragment)) return;
            Assert.Fail("UI text missing: " + fragment);
        }
    }
}
