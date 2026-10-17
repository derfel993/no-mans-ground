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
        public IEnumerator ScheduledLunchUpdatesKitchenCountersAndSoldierHungerBar()
        {
            EditorSceneManager.OpenScene(MapPrototypeSetup.ScenePath);
            yield return new EnterPlayMode();
            yield return null;
            var kitchen = Object.FindAnyObjectByType<KitchenSystem>();
            var roster = Object.FindAnyObjectByType<SoldierRoster>();
            var panel = Object.FindAnyObjectByType<SoldierDetailsPanel>();
            var map = Object.FindAnyObjectByType<MapPrototype>();
            float scale = Time.timeScale;
            try
            {
                Time.timeScale = 0f;
                yield return null;
                kitchen.Cook.AdvanceMovement(1000f);
                kitchen.Stock.StoreKitchenProvisions(kitchen.Stock.TakeProvisions(kitchen.Stock.Provisions));
                kitchen.Stock.AdvanceTime(270f);
                kitchen.Tick(1f);
                kitchen.Cook.AdvanceMovement(1000f);
                kitchen.Tick(0.1f);
                SoldierView soldier = roster.Soldiers[0];
                soldier.Data.Needs.AdvanceTime(1000f);
                panel.Select(soldier);
                AssertText("Attende rancio");
                map.Clock.Reset();
                map.Clock.AdvanceTime(400d);
                kitchen.Tick(0.1f);
                panel.SelectFacility();
                AssertText("Razioni pronte: 0");
                AssertText("In consegna: ");
                Assert.Greater(kitchen.CarriedRations, 0);
                yield return null;
                Canvas.ForceUpdateCanvases();
                foreach (Text text in GameObject.Find("Soldier Interface").GetComponentsInChildren<Text>())
                    Assert.LessOrEqual(text.preferredHeight, text.rectTransform.rect.height + 0.1f,
                        "Kitchen UI text is truncated: " + text.text);
                for (int i = 0; i < 100 && kitchen.PendingMeals > 0; i++)
                {
                    kitchen.Cook.AdvanceMovement(1000f);
                    kitchen.Tick(0.6f);
                }
                Assert.Zero(kitchen.PendingMeals, kitchen.Status);
                Assert.Zero(kitchen.CarriedRations);
                AssertText("In attesa: 0");
                panel.Select(soldier);
                AssertText("Fame: 100/100");
                bool greenBar = false;
                foreach (Text text in GameObject.Find("Soldier Interface").GetComponentsInChildren<Text>())
                    if (text.text == "Fame: 100/100")
                    {
                        var fill = text.transform.parent.Find("Track/Fill").GetComponent<Image>();
                        Assert.AreEqual(1f, fill.rectTransform.anchorMax.x);
                        Assert.AreEqual(0.70f, fill.color.g, 0.00001f);
                        greenBar = true;
                    }
                Assert.IsTrue(greenBar);
                map.Regenerate();
                Assert.Zero(kitchen.PendingMeals);
                Assert.Zero(kitchen.CarriedRations);
                Assert.Zero(kitchen.Stock.ReadyRations);
            }
            finally { Time.timeScale = scale; }
            yield return new ExitPlayMode();
        }

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
                Assert.AreEqual(990, stock.Provisions);
                Assert.AreEqual(10, kitchen.CarriedProvisions);
                Assert.Zero(stock.Progress);
                kitchen.Tick(100f);
                Assert.Zero(stock.Progress);
                kitchen.Cook.AdvanceMovement(100f);
                kitchen.Tick(1f);
                Assert.AreEqual(10, stock.KitchenProvisions);
                Assert.Zero(stock.Progress);
                kitchen.Tick(30f);
                Assert.AreEqual(1, stock.ReadyRations);
                Assert.AreEqual(990, stock.Provisions);
                Assert.Zero(stock.KitchenProvisions);
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
                kitchen.Tick(1f);
                kitchen.Cook.AdvanceMovement(100f);
                kitchen.Tick(1f);
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
