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
        public void DepotStockIsWithdrawnOnceAndKitchenStockIsConsumedByCooking()
        {
            var stock = new RationStock();
            Assert.AreEqual(1000, stock.Provisions);
            Assert.Zero(stock.KitchenProvisions);
            Assert.Zero(stock.ReadyRations);
            int initialLoad = stock.TakeProvisions(RationStock.ProvisionsPerRation);
            Assert.AreEqual(990, stock.Provisions);
            stock.StoreKitchenProvisions(initialLoad);
            stock.AdvanceTime(29f);
            Assert.Zero(stock.ReadyRations);
            Assert.AreEqual(990, stock.Provisions);
            Assert.AreEqual(10, stock.KitchenProvisions);
            stock.AdvanceTime(1f);
            Assert.AreEqual(1, stock.ReadyRations);
            Assert.AreEqual(990, stock.Provisions);
            Assert.Zero(stock.KitchenProvisions);
            stock.StoreKitchenProvisions(stock.TakeProvisions(990));
            stock.AdvanceTime(75f);
            Assert.AreEqual(3, stock.ReadyRations);
            Assert.Zero(stock.Provisions);
            Assert.AreEqual(970, stock.KitchenProvisions);
            Assert.AreEqual(0.5f, stock.Progress);
            stock.AdvanceTime(float.MaxValue);
            Assert.AreEqual(100, stock.ReadyRations);
            Assert.Zero(stock.Provisions);
            Assert.Zero(stock.KitchenProvisions);
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
            Assert.IsFalse(kitchen.Cook.CanTakeGuardMove);
            for (int i = 0; i < 8; i++)
                Assert.AreEqual(SoldierProfession.Engineer, roster.Soldiers[i].Data.Profession);
            Assert.IsTrue(kitchen.HasFacilities);
            Assert.IsFalse(map.Data.IsWalkable(kitchen.KitchenCell));
            Assert.IsFalse(map.Data.IsWalkable(kitchen.DepotCell));
            Assert.AreNotEqual(TerrainKind.Road, map.Data[kitchen.KitchenCell.x, kitchen.KitchenCell.y]);
            Assert.AreNotEqual(TerrainKind.Road, map.Data[kitchen.DepotCell.x, kitchen.DepotCell.y]);
            for (int y = 0; y < 6; y++)
                for (int x = 62; x < 66; x++)
                    Assert.IsTrue(map.Data.IsWalkable(new Vector2Int(x, y)),
                        "Facilities must leave the arrival road walkable.");
        }

        [Test]
        public void SmallStepsAndSingleStepProduceTheSameRationsAndPartialProgress()
        {
            var single = new RationStock();
            var stepped = new RationStock();
            single.StoreKitchenProvisions(single.TakeProvisions(1000));
            stepped.StoreKitchenProvisions(stepped.TakeProvisions(1000));
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
        public void CookWithdrawsFoodBeforeCookingAndDoesNotChargeTheDepotAgainOnCompletion()
        {
            kitchen.Tick(1f);
            Assert.IsTrue(kitchen.Cook.IsMoving);
            kitchen.Tick(100f);
            Assert.Zero(kitchen.Stock.Progress);
            Assert.AreEqual(1000, kitchen.Stock.Provisions);
            Assert.Zero(kitchen.CarriedProvisions);
            kitchen.Cook.AdvanceMovement(100f);
            kitchen.Tick(1f);
            Assert.AreEqual(990, kitchen.Stock.Provisions);
            Assert.AreEqual(10, kitchen.CarriedProvisions);
            Assert.IsTrue(kitchen.Cook.IsMoving);
            Assert.Zero(kitchen.Stock.Progress);
            kitchen.Tick(100f);
            Assert.Zero(kitchen.Stock.Progress);
            kitchen.Cook.AdvanceMovement(100f);
            kitchen.Tick(1f);
            Assert.AreEqual(990, kitchen.Stock.Provisions);
            Assert.Zero(kitchen.CarriedProvisions);
            Assert.AreEqual(10, kitchen.Stock.KitchenProvisions);
            Assert.Zero(kitchen.Stock.Progress);
            kitchen.Tick(29f);
            Assert.Zero(kitchen.Stock.ReadyRations);
            Assert.AreEqual(990, kitchen.Stock.Provisions);
            kitchen.Tick(1f);
            Assert.AreEqual(1, kitchen.Stock.ReadyRations);
            Assert.AreEqual(990, kitchen.Stock.Provisions);
            Assert.Zero(kitchen.Stock.KitchenProvisions);
            kitchen.Tick(1f);
            Assert.IsTrue(kitchen.Cook.IsMoving);
            Assert.AreEqual(990, kitchen.Stock.Provisions);
            kitchen.Cook.AdvanceMovement(100f);
            kitchen.Tick(1f);
            Assert.AreEqual(980, kitchen.Stock.Provisions);
            Assert.AreEqual(10, kitchen.CarriedProvisions);
            Assert.Zero(kitchen.Stock.Progress);
            kitchen.Tick(100f);
            Assert.Zero(kitchen.Stock.Progress);
            kitchen.Cook.AdvanceMovement(100f);
            kitchen.Tick(1f);
            Assert.AreEqual(10, kitchen.Stock.KitchenProvisions);
            Assert.Zero(kitchen.Stock.Progress);
            kitchen.Tick(29f);
            Assert.AreEqual(1, kitchen.Stock.ReadyRations);
            kitchen.Tick(1f);
            Assert.AreEqual(2, kitchen.Stock.ReadyRations);
            Assert.AreEqual(980, kitchen.Stock.Provisions);
        }

        [Test]
        public void InaccessibleKitchenWaitsAndDisabledCookCannotProduce()
        {
            foreach (Vector2Int delta in new[] { Vector2Int.down, Vector2Int.up, Vector2Int.left, Vector2Int.right })
                map.Data.SetWalkable(kitchen.DepotCell + delta, false);
            kitchen.Tick(1f);
            Assert.IsFalse(kitchen.Cook.IsMoving);
            Assert.AreEqual("Accesso deposito bloccato o occupato.", kitchen.Status);
            foreach (Vector2Int delta in new[] { Vector2Int.down, Vector2Int.up, Vector2Int.left, Vector2Int.right })
                map.Data.SetWalkable(kitchen.DepotCell + delta, true);
            kitchen.Tick(1f);
            Assert.IsTrue(kitchen.Cook.IsMoving);
            kitchen.Cook.AdvanceMovement(100f);
            kitchen.Tick(1f);
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

        private void ReachKitchen()
        {
            kitchen.Tick(1f);
            kitchen.Cook.AdvanceMovement(1000f);
            kitchen.Tick(1f);
            kitchen.Cook.AdvanceMovement(1000f);
            kitchen.Tick(1f);
        }

        private void StageAllProvisionsInKitchen()
        {
            kitchen.Stock.StoreKitchenProvisions(kitchen.Stock.TakeProvisions(kitchen.Stock.Provisions));
        }

        private void CompleteMealRound()
        {
            for (int i = 0; i < 300 && kitchen.PendingMeals > 0; i++)
            {
                kitchen.Tick(1f);
                kitchen.Cook.AdvanceMovement(1000f);
            }
            Assert.Zero(kitchen.PendingMeals, kitchen.Status);
        }

        [Test]
        public void LunchStartsAtThirteenFeedsEveryoneOnceAndRepeatsNextDay()
        {
            foreach (SoldierView soldier in roster.Soldiers)
                soldier.Data.Needs.AdvanceTime(1000f);
            ReachKitchen();
            StageAllProvisionsInKitchen();
            kitchen.Stock.AdvanceTime(270f);
            map.Clock.AdvanceTime(399d);
            kitchen.Tick(0.1f);
            Assert.Zero(kitchen.PendingMeals);
            Assert.Less(kitchen.Cook.Data.Needs.Hunger, 35f);
            map.Clock.AdvanceTime(1d);
            kitchen.Tick(0.1f);
            Assert.AreEqual(100f, kitchen.Cook.Data.Needs.Hunger);
            Assert.Zero(kitchen.Stock.ReadyRations);
            CompleteMealRound();
            foreach (SoldierView soldier in roster.Soldiers)
                Assert.AreEqual(100f, soldier.Data.Needs.Hunger);
            Assert.Zero(kitchen.CarriedRations);
            foreach (SoldierView soldier in roster.Soldiers)
                soldier.Data.Needs.AdvanceTime(10f);
            kitchen.Tick(1f);
            Assert.Zero(kitchen.PendingMeals, "A completed lunch must not restart on the same day.");
            Assert.Less(roster.Soldiers[0].Data.Needs.Hunger, 100f);
            kitchen.Stock.AdvanceTime(270f);
            map.Clock.AdvanceTime(2400d);
            kitchen.Tick(1f);
            CompleteMealRound();
            foreach (SoldierView soldier in roster.Soldiers)
                Assert.AreEqual(100f, soldier.Data.Needs.Hunger);
        }

        [Test]
        public void ShortageReturnsCookToProductionUntilEveryoneHasEaten()
        {
            ReachKitchen();
            StageAllProvisionsInKitchen();
            kitchen.Stock.AdvanceTime(60f);
            map.Clock.AdvanceTime(400d);
            kitchen.Tick(0.1f);
            Assert.AreEqual(1, kitchen.CarriedRations);
            Assert.Zero(kitchen.Stock.ReadyRations);
            for (int i = 0; i < 300 && kitchen.PendingMeals > 0; i++)
            {
                kitchen.Cook.AdvanceMovement(1000f);
                kitchen.Tick(30f);
            }
            Assert.Zero(kitchen.PendingMeals, kitchen.Status);
            Assert.Zero(kitchen.CarriedRations);
            Assert.LessOrEqual(kitchen.Stock.Provisions, 910);
        }

        [Test]
        public void PreparedMealsAreDistributedEvenWhenProvisionsAreExhausted()
        {
            ReachKitchen();
            StageAllProvisionsInKitchen();
            kitchen.Stock.AdvanceTime(3000f);
            Assert.Zero(kitchen.Stock.Provisions);
            var view = map.GetComponentInChildren<MapTilemapView>();
            var away = new Vector2Int(20, 20);
            kitchen.Cook.BeginMove(view,
                GridPathfinder.FindNearest(map.Data, new[] { kitchen.Cook.Cell }, away).Cells);
            kitchen.Cook.AdvanceMovement(1000f);
            map.Clock.AdvanceTime(400d);
            kitchen.Tick(0.1f);
            CompleteMealRound();
            Assert.AreEqual(91, kitchen.Stock.ReadyRations);
            Assert.Zero(kitchen.CarriedRations);
        }

        [Test]
        public void MovingRecipientIsReachedAtNewLocationWithoutReplacingTheirGuardOrder()
        {
            ReachKitchen();
            StageAllProvisionsInKitchen();
            kitchen.Stock.AdvanceTime(270f);
            foreach (SoldierView soldier in roster.Soldiers)
                soldier.Data.Needs.AdvanceTime(1000f);
            map.Clock.AdvanceTime(400d);
            kitchen.Tick(0.1f);
            var orders = roster.GetComponent<SoldierMovementOrders>();
            SoldierView guard = roster.Soldiers[0];
            orders.Selected = guard;
            guard.RequestDuty(SoldierDuty.Guard);
            var destination = new Vector2Int(30, 30);
            Assert.IsTrue(orders.IssueMove(destination));
            guard.AdvanceMovement(1000f);
            CompleteMealRound();
            Assert.AreEqual(destination, guard.Cell);
            Assert.AreEqual(SoldierDuty.Guard, guard.Duty);
            Assert.AreEqual(100f, guard.Data.Needs.Hunger);
            Assert.AreEqual(0, kitchen.Stock.ReadyRations);
        }

        [Test]
        public void DisabledCookPausesDeliveryAndRegenerationClearsTheRound()
        {
            ReachKitchen();
            StageAllProvisionsInKitchen();
            kitchen.Stock.AdvanceTime(270f);
            map.Clock.AdvanceTime(400d);
            kitchen.Tick(0.1f);
            int pending = kitchen.PendingMeals;
            int carried = kitchen.CarriedRations;
            kitchen.Cook.enabled = false;
            kitchen.Tick(10f);
            Assert.AreEqual(pending, kitchen.PendingMeals);
            Assert.AreEqual(carried, kitchen.CarriedRations);
            kitchen.Cook.enabled = true;
            CompleteMealRound();
            kitchen.Rebuild();
            Assert.Zero(kitchen.PendingMeals);
            Assert.Zero(kitchen.CarriedRations);
            Assert.Zero(kitchen.Stock.ReadyRations);
            Assert.AreEqual(1000, kitchen.Stock.Provisions);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void UnreachableTrenchRecipientIsRetriedUntilLadderOrMidnight(bool addLadder)
        {
            SoldierView isolated = roster.Soldiers[0];
            var cell = new Vector2Int(10, 10);
            var view = map.GetComponentInChildren<MapTilemapView>();
            isolated.BeginMove(view, GridPathfinder.FindNearest(map.Data, new[] { isolated.Cell }, cell).Cells);
            isolated.AdvanceMovement(1000f);
            map.Data[cell.x, cell.y] = TerrainKind.Trench;
            isolated.Data.Needs.AdvanceTime(1000f);
            ReachKitchen();
            StageAllProvisionsInKitchen();
            kitchen.Stock.AdvanceTime(270f);
            map.Clock.AdvanceTime(400d);
            kitchen.Tick(0.1f);
            for (int i = 0; i < 40; i++)
            {
                kitchen.Cook.AdvanceMovement(1000f);
                kitchen.Tick(1f);
            }
            Assert.AreEqual(1, kitchen.PendingMeals);
            Assert.AreEqual(1, kitchen.CarriedRations);
            Assert.Less(isolated.Data.Needs.Hunger, 35f);
            if (addLadder)
            {
                map.Data.AddLadder(cell);
                CompleteMealRound();
                Assert.AreEqual(100f, isolated.Data.Needs.Hunger);
            }
            else
            {
                map.Clock.AdvanceTime(1100d);
                kitchen.Tick(0.1f);
                Assert.Zero(kitchen.PendingMeals);
                Assert.Zero(kitchen.CarriedRations);
                Assert.AreEqual(1, kitchen.Stock.ReadyRations);
                Assert.Less(isolated.Data.Needs.Hunger, 35f);
            }
        }

        [Test]
        public void EatingOnlyRestoresHungerAndNotifiesObservers()
        {
            SoldierNeeds needs = roster.Soldiers[0].Data.Needs;
            needs.AdvanceTime(1000f);
            float thirst = needs.Thirst;
            float physical = needs.PhysicalNeeds;
            float stress = needs.Stress;
            int changes = 0;
            needs.Changed += () => changes++;
            needs.EatRation();
            Assert.AreEqual(100f, needs.Hunger);
            Assert.IsFalse(needs.WaitingForRations);
            Assert.AreEqual(thirst, needs.Thirst);
            Assert.AreEqual(physical, needs.PhysicalNeeds);
            Assert.AreEqual(stress, needs.Stress);
            Assert.AreEqual(1, changes);
            needs.AdvanceTime(1f);
            Assert.Less(needs.Hunger, 100f);
        }
    }
}
