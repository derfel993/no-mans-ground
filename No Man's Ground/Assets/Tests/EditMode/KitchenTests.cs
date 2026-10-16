using NoMansGround.Editor;
using NoMansGround.Map;
using NoMansGround.Soldiers;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NoMansGround.Tests
{
    public sealed class KitchenTests
    {
        private Scene scene;
        private MapPrototype map;
        private SoldierRoster roster;
        private KitchenSystem kitchen;

        [SetUp]
        public void SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene = EditorSceneManager.OpenScene(MapPrototypeSetup.ScenePath, OpenSceneMode.Additive);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.TryGetComponent(out MapPrototype prototype)) map = prototype;
                if (root.TryGetComponent(out SoldierRoster soldiers)) roster = soldiers;
            }
            map.Regenerate();
            roster.Spawn(map.Data);
            kitchen = roster.GetComponent<KitchenSystem>();
            Assert.IsNotNull(kitchen);
            kitchen.Rebuild();
        }

        [TearDown]
        public void TearDown() { EditorSceneManager.CloseScene(scene, true); }

        [Test]
        public void StockConsumesExactlyTenPerThirtySecondsAndStopsAtOneHundredRations()
        {
            var stock = new RationStock();
            Assert.AreEqual(1000, stock.Provisions);
            Assert.Zero(stock.ReadyRations);
            stock.AdvanceTime(29f);
            Assert.Zero(stock.ReadyRations);
            Assert.AreEqual(1000, stock.Provisions);
            stock.AdvanceTime(1f);
            Assert.AreEqual(1, stock.ReadyRations);
            Assert.AreEqual(990, stock.Provisions);
            stock.AdvanceTime(75f);
            Assert.AreEqual(3, stock.ReadyRations);
            Assert.AreEqual(970, stock.Provisions);
            Assert.AreEqual(0.5f, stock.Progress);
            stock.AdvanceTime(float.MaxValue);
            Assert.AreEqual(100, stock.ReadyRations);
            Assert.Zero(stock.Provisions);
            Assert.Zero(stock.Progress);
            stock.AdvanceTime(300f);
            Assert.AreEqual(100, stock.ReadyRations);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => stock.AdvanceTime(float.NaN));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => stock.AdvanceTime(-1f));
        }

        [Test]
        public void CookIsAdditionalAndExcludedFromEngineeringAndSharedMoves()
        {
            Assert.AreEqual(9, roster.Soldiers.Count);
            Assert.AreSame(roster.Soldiers[8], kitchen.Cook);
            Assert.AreEqual(SoldierProfession.Cook, kitchen.Cook.Data.Profession);
            Assert.IsFalse(kitchen.Cook.CanTakeEngineeringWork);
            Assert.IsFalse(kitchen.Cook.CanTakeSharedMove);
            for (int i = 0; i < 8; i++)
                Assert.AreEqual(SoldierProfession.Engineer, roster.Soldiers[i].Data.Profession);
            Assert.IsTrue(kitchen.HasFacilities);
            Assert.IsFalse(map.Data.IsWalkable(kitchen.KitchenCell));
            Assert.IsFalse(map.Data.IsWalkable(kitchen.DepotCell));
        }

        [Test]
        public void SmallStepsAndSingleStepProduceTheSameRationsAndPartialProgress()
        {
            var single = new RationStock();
            var stepped = new RationStock();
            single.AdvanceTime(105f);
            for (int i = 0; i < 6300; i++)
                stepped.AdvanceTime(1f / 60f);
            Assert.AreEqual(single.Provisions, stepped.Provisions);
            Assert.AreEqual(single.ReadyRations, stepped.ReadyRations);
            Assert.AreEqual(single.Progress, stepped.Progress, 0.00001f);
            float progress = stepped.Progress;
            stepped.AdvanceTime(0f);
            Assert.AreEqual(progress, stepped.Progress);
        }

        [Test]
        public void TravelIsNotCookingAndStockOnlyChangesAfterThirtySecondsAtKitchen()
        {
            kitchen.Tick(1f);
            Assert.IsTrue(kitchen.Cook.IsMoving);
            kitchen.Tick(100f);
            Assert.Zero(kitchen.Stock.Progress);
            Assert.AreEqual(1000, kitchen.Stock.Provisions);
            kitchen.Cook.AdvanceMovement(100f);
            Assert.AreEqual(1, Mathf.Abs(kitchen.Cook.Cell.x - kitchen.KitchenCell.x)
                + Mathf.Abs(kitchen.Cook.Cell.y - kitchen.KitchenCell.y));
            kitchen.Tick(29f);
            Assert.Zero(kitchen.Stock.ReadyRations);
            kitchen.Tick(1f);
            Assert.AreEqual(1, kitchen.Stock.ReadyRations);
            Assert.AreEqual(990, kitchen.Stock.Provisions);
            kitchen.Tick(2970f);
            Assert.AreEqual(100, kitchen.Stock.ReadyRations);
            Assert.Zero(kitchen.Stock.Provisions);
            Assert.IsFalse(kitchen.Cook.HasWorkAssignment);
        }

        [Test]
        public void InaccessibleKitchenWaitsAndDisabledCookCannotProduce()
        {
            foreach (Vector2Int delta in new[] { Vector2Int.down, Vector2Int.up, Vector2Int.left, Vector2Int.right })
                map.Data.SetWalkable(kitchen.KitchenCell + delta, false);
            kitchen.Tick(1f);
            Assert.IsFalse(kitchen.Cook.IsMoving);
            Assert.AreEqual("Accesso cucina bloccato o occupato.", kitchen.Status);
            map.Data.SetWalkable(kitchen.KitchenCell + Vector2Int.down, true);
            kitchen.Tick(1f);
            Assert.IsTrue(kitchen.Cook.IsMoving);
            kitchen.Cook.AdvanceMovement(100f);
            kitchen.Cook.enabled = false;
            kitchen.Tick(300f);
            Assert.Zero(kitchen.Stock.ReadyRations);
            Assert.IsFalse(kitchen.Cook.HasWorkAssignment);
            kitchen.Cook.enabled = true;
            kitchen.Tick(1f);
            kitchen.Tick(30f);
            Assert.AreEqual(1, kitchen.Stock.ReadyRations);
        }
    }
}
