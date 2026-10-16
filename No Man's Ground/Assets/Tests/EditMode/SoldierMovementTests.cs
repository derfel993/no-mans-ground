using NoMansGround.Editor;
using NoMansGround.Map;
using NoMansGround.Soldiers;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;

namespace NoMansGround.Tests
{
    public sealed class SoldierMovementTests
    {
        private Scene scene;
        private MapPrototype map;
        private SoldierRoster roster;
        private SoldierMovementOrders orders;
        private MapTilemapView view;

        [SetUp]
        public void SetUp()
        {
            scene = EditorSceneManager.OpenScene(MapPrototypeSetup.ScenePath, OpenSceneMode.Additive);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.TryGetComponent(out MapPrototype prototype))
                {
                    map = prototype;
                    view = root.GetComponentInChildren<MapTilemapView>();
                }
                if (root.TryGetComponent(out SoldierRoster foundRoster))
                {
                    roster = foundRoster;
                    orders = root.GetComponent<SoldierMovementOrders>();
                }
            }
            Assert.IsNotNull(orders);
            map.Regenerate();
            roster.Spawn(map.Data);
        }

        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.CloseScene(scene, true);
        }

        [Test]
        public void SharedOrderChoosesNearestFreeSoldierAndReservesDestination()
        {
            SoldierView nearest = roster.Soldiers[0];
            Vector2Int start = nearest.Cell;
            Vector2Int goal = start + Vector2Int.left;
            Assert.IsTrue(orders.IssueMove(goal));
            Assert.IsTrue(nearest.IsMoving);
            Assert.AreEqual(goal, nearest.Destination);
            LogAssert.Expect(LogType.Warning, "Destinazione gia occupata o prenotata.");
            Assert.IsFalse(orders.IssueMove(goal));
            Assert.IsTrue(orders.IssueMove(start));
            Assert.IsTrue(roster.Soldiers[3].IsMoving);
            Assert.AreEqual(start, roster.Soldiers[3].Destination);
            nearest.AdvanceMovement(100f);
            Assert.IsFalse(nearest.IsMoving);
            Assert.AreEqual(goal, nearest.Cell);
            Assert.AreEqual(view.Tilemap.GetCellCenterWorld(new Vector3Int(goal.x, goal.y, 0)), nearest.transform.position);
            LogAssert.Expect(LogType.Warning, "Destinazione gia occupata o prenotata.");
            Assert.IsFalse(orders.IssueMove(goal));
            Assert.IsTrue(orders.IssueMove(goal + Vector2Int.left));
            Assert.IsTrue(nearest.IsMoving);
        }

        [Test]
        public void MovementConsumesDistanceAcrossWaypointsAndUsesSpeedSkill()
        {
            SoldierView soldier = roster.Soldiers[0];
            Vector2Int start = soldier.Cell;
            var cells = new[] { start, start + Vector2Int.up, start + Vector2Int.up * 2 };
            soldier.BeginMove(view, cells);
            float speed = soldier.MovementSpeed;
            Vector3 position = soldier.transform.position;
            soldier.AdvanceMovement(0.5f / speed);
            Assert.AreEqual(start, soldier.Cell);
            Assert.Less(Vector3.Distance(position + Vector3.up * 0.5f, soldier.transform.position), 0.0001f);
            soldier.AdvanceMovement(1f / speed);
            Assert.AreEqual(start + Vector2Int.up, soldier.Cell);
            Assert.Less(Vector3.Distance(position + Vector3.up * 1.5f, soldier.transform.position), 0.0001f);
            soldier.AdvanceMovement(100f);
            Assert.AreEqual(cells[2], soldier.Cell);
            Assert.IsFalse(soldier.IsMoving);
            Assert.IsTrue(orders.Status.Contains("Destro"));
            int value = soldier.Data.GetSkill(SoldierSkill.Speed).Value;
            Assert.AreEqual(Mathf.Lerp(1.5f, 4.5f, value / 100f), speed);
            soldier.Data.GetSkill(SoldierSkill.Speed).AddExperience(int.MaxValue);
            Assert.AreEqual(4.5f, soldier.MovementSpeed);
        }

        [Test]
        public void DiagonalSpeedIsNotFasterAndFrameSubdivisionPreservesArrival()
        {
            SoldierView first = roster.Soldiers[0];
            Vector2Int start = first.Cell;
            first.BeginMove(view, new[] { start, start + Vector2Int.one, start + new Vector2Int(2, 1) });
            Vector3 before = first.transform.position;
            first.AdvanceMovement(0.5f / first.MovementSpeed);
            Assert.AreEqual(0.5f, Vector3.Distance(before, first.transform.position), 0.0001f);
            for (int i = 0; i < 100; i++)
                first.AdvanceMovement(0.1f);
            Assert.AreEqual(start + new Vector2Int(2, 1), first.Cell);
            Assert.IsFalse(first.IsMoving);
        }

        [Test]
        public void DebugObstaclesAreVisibleReversibleAndCannotInvalidateActivePaths()
        {
            var cell = new Vector2Int(50, 10);
            var tileCell = new Vector3Int(cell.x, cell.y, 0);
            Color original = view.Tilemap.GetColor(tileCell);
            Assert.IsTrue(orders.ToggleObstacle(cell));
            Assert.IsFalse(map.Data.IsWalkable(cell));
            Assert.AreEqual(new Color(0.20f, 0.13f, 0.13f), view.Tilemap.GetColor(tileCell));
            LogAssert.Expect(LogType.Warning, "Destinazione fuori mappa o bloccata.");
            Assert.IsFalse(orders.IssueMove(cell));
            Assert.IsTrue(orders.ToggleObstacle(cell));
            Assert.IsTrue(map.Data.IsWalkable(cell));
            Assert.AreEqual(original, view.Tilemap.GetColor(tileCell));
            Assert.IsTrue(orders.IssueMove(cell));
            LogAssert.Expect(LogType.Warning, "Ostacoli debug: attendi che tutti i soldati siano fermi.");
            Assert.IsFalse(orders.ToggleObstacle(cell + Vector2Int.up));
            foreach (SoldierView soldier in roster.Soldiers)
                soldier.AdvanceMovement(1000f);
            LogAssert.Expect(LogType.Warning, "Non puoi bloccare la cella di un soldato.");
            Assert.IsFalse(orders.ToggleObstacle(cell));
        }

        [Test]
        public void DisabledSoldiersKeepTheirEndpointsReservedAndAreNotAssigned()
        {
            SoldierView first = roster.Soldiers[0];
            first.enabled = false;
            LogAssert.Expect(LogType.Warning, "Destinazione gia occupata o prenotata.");
            Assert.IsFalse(orders.IssueMove(first.Cell));
            Assert.IsTrue(orders.IssueMove(first.Cell + Vector2Int.left));
            Assert.IsFalse(first.IsMoving);
        }

        [Test]
        public void BusyAndUnreachableOrdersReportFailureWithoutChangingExistingOrders()
        {
            for (int i = 0; i < roster.Soldiers.Count; i++)
                Assert.IsTrue(orders.IssueMove(new Vector2Int(20 + i, 20)));
            LogAssert.Expect(LogType.Warning, "Nessun soldato disponibile. Attendi un arrivo.");
            Assert.IsFalse(orders.IssueMove(new Vector2Int(50, 20)));
            foreach (SoldierView soldier in roster.Soldiers)
                soldier.AdvanceMovement(1000f);
            var goal = new Vector2Int(10, 10);
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    if (dx != 0 || dy != 0)
                        map.Data.SetWalkable(goal + new Vector2Int(dx, dy), false);
            LogAssert.Expect(LogType.Warning, "Nessun percorso raggiungibile verso la destinazione.");
            Assert.IsFalse(orders.IssueMove(goal));
        }
    }
}
