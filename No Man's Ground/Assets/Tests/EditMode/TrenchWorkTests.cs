using System.Collections.Generic;
using NoMansGround.Editor;
using NoMansGround.Map;
using NoMansGround.Soldiers;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace NoMansGround.Tests
{
    public sealed class TrenchWorkTests
    {
        private Scene scene;
        private MapPrototype map;
        private MapTilemapView view;
        private SoldierRoster roster;
        private SoldierMovementOrders movement;
        private TrenchWorkOrders trenches;

        [SetUp]
        public void SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene = EditorSceneManager.OpenScene(MapPrototypeSetup.ScenePath, OpenSceneMode.Additive);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.TryGetComponent(out MapPrototype prototype))
                {
                    map = prototype;
                    view = root.GetComponentInChildren<MapTilemapView>();
                }
                if (root.TryGetComponent(out SoldierRoster soldiers))
                {
                    roster = soldiers;
                    movement = root.GetComponent<SoldierMovementOrders>();
                    trenches = root.GetComponent<TrenchWorkOrders>();
                }
            }
            Assert.IsNotNull(trenches);
            map.Regenerate();
            roster.Spawn(map.Data);
        }

        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.CloseScene(scene, true);
        }

        [TestCase(0, 0, 7, 0)]
        [TestCase(7, 7, 0, 0)]
        [TestCase(2, 8, 5, 1)]
        [TestCase(3, 3, 3, 3)]
        public void DragRasterIncludesEndpointsWithoutDuplicates(int x0, int y0, int x1, int y1)
        {
            var from = new Vector2Int(x0, y0);
            var to = new Vector2Int(x1, y1);
            var line = TrenchWorkOrders.RasterLine(from, to);
            Assert.AreEqual(from, line[0]);
            Assert.AreEqual(to, line[line.Count - 1]);
            Assert.AreEqual(Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0)) + 1, line.Count);
            Assert.AreEqual(line.Count, new HashSet<Vector2Int>(line).Count);
        }

        [Test]
        public void AdjacentWorkerCompletesTrenchWithExactStrengthDurationAndExperience()
        {
            SoldierView worker = roster.Soldiers[0];
            Vector2Int cell = worker.Cell + Vector2Int.down;
            Assert.IsTrue(trenches.Designate(cell));
            Assert.IsFalse(trenches.Designate(cell));
            trenches.Tick(0.01f);
            TrenchJob job = trenches.Jobs[0];
            Assert.AreSame(worker, job.Worker);
            Assert.IsFalse(worker.IsMoving);
            Assert.IsTrue(worker.HasWorkAssignment);
            Assert.IsFalse(worker.IsAvailable);
            SkillProgress strength = worker.Data.GetSkill(SoldierSkill.Strength);
            int before = strength.Experience;
            float duration = 6f / (1f + strength.Value / 100f);
            trenches.Tick(duration * 0.5f);
            Assert.AreEqual(0.5f, job.Progress, 0.0001f);
            Assert.AreEqual(0.5f, worker.DiggingProgress, 0.0001f);
            trenches.Tick(duration * 0.5f + 0.001f);
            Assert.Zero(trenches.Jobs.Count);
            Assert.AreEqual(TerrainKind.Trench, map.Data[cell.x, cell.y]);
            Assert.IsTrue(map.Data.IsWalkable(cell));
            Assert.IsTrue(worker.IsAvailable);
            Assert.AreEqual(before + 25, strength.Experience);
            Assert.IsFalse(trenches.Designate(cell));
            Assert.AreEqual("Trench", view.Tilemap.GetTile(new Vector3Int(cell.x, cell.y, 0)).name);
        }

        [Test]
        public void QueuedJobsReserveDistinctWorkersAndAdjacentEndpointsAndContinueAutomatically()
        {
            for (int x = 50; x < 62; x++)
                Assert.IsTrue(trenches.Designate(new Vector2Int(x, 10)));
            trenches.Tick(0.01f);
            var workers = new HashSet<SoldierView>();
            var endpoints = new HashSet<Vector2Int>();
            foreach (TrenchJob job in trenches.Jobs)
            {
                if (job.Worker == null)
                    continue;
                Assert.IsTrue(workers.Add(job.Worker));
                Assert.IsTrue(endpoints.Add(job.Worker.Destination));
                Assert.AreEqual(1, Mathf.Abs(job.Cell.x - job.Worker.Destination.x)
                    + Mathf.Abs(job.Cell.y - job.Worker.Destination.y));
            }
            Assert.AreEqual(8, workers.Count);
            for (int step = 0; step < 60 && trenches.Jobs.Count > 0; step++)
            {
                foreach (SoldierView soldier in roster.Soldiers)
                    soldier.AdvanceMovement(100f);
                trenches.Tick(1f);
            }
            Assert.Zero(trenches.Jobs.Count);
            for (int x = 50; x < 62; x++)
                Assert.AreEqual(TerrainKind.Trench, map.Data[x, 10]);
        }

        [Test]
        public void CancellationReleasesWorkerKeepsTerrainAndDoesNotAwardExperience()
        {
            var cell = roster.Soldiers[0].Cell + Vector2Int.down;
            TerrainKind original = map.Data[cell.x, cell.y];
            trenches.Designate(cell);
            trenches.Tick(0.01f);
            SoldierView worker = trenches.Jobs[0].Worker;
            int xp = worker.Data.GetSkill(SoldierSkill.Strength).Experience;
            trenches.Tick(1f);
            Assert.IsTrue(trenches.Cancel(cell));
            Assert.IsFalse(trenches.Cancel(cell));
            Assert.IsTrue(worker.IsAvailable);
            Assert.AreEqual(original, map.Data[cell.x, cell.y]);
            Assert.AreEqual(xp, worker.Data.GetSkill(SoldierSkill.Strength).Experience);
            Assert.Zero(trenches.Jobs.Count);
        }

        [Test]
        public void CancelInTransitDoesNotTeleportOrInterruptOtherMovement()
        {
            var cell = new Vector2Int(50, 10);
            trenches.Designate(cell);
            trenches.Tick(0.01f);
            SoldierView worker = trenches.Jobs[0].Worker;
            Assert.IsTrue(worker.IsMoving);
            Vector3 before = worker.transform.position;
            trenches.Cancel(cell);
            Assert.IsFalse(worker.HasWorkAssignment);
            Assert.IsTrue(worker.IsMoving);
            Assert.AreEqual(before, worker.transform.position);
            worker.AdvanceMovement(100f);
            Assert.IsTrue(worker.IsAvailable);
            Assert.AreNotEqual(TerrainKind.Trench, map.Data[cell.x, cell.y]);
        }

        [Test]
        public void UnreachableWorkRemainsPendingAndRetriesAfterAccessOpens()
        {
            var cell = new Vector2Int(50, 10);
            foreach (Vector2Int delta in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                map.Data.SetWalkable(cell + delta, false);
            Assert.IsTrue(trenches.Designate(cell));
            trenches.Tick(1f);
            Assert.IsNull(trenches.Jobs[0].Worker);
            Assert.AreEqual("Accesso bloccato o occupato", trenches.Jobs[0].Status);
            map.Data.SetWalkable(cell + Vector2Int.down, true);
            trenches.Tick(1f);
            Assert.IsNotNull(trenches.Jobs[0].Worker);
            LogAssert.Expect(LogType.Warning, "Ostacoli debug: annulla o completa prima gli scavi.");
            Assert.IsFalse(movement.ToggleObstacle(new Vector2Int(10, 10)));
        }

        [Test]
        public void AssignedWorkersAreExcludedFromSharedMovementAndDisabledWorkersReleaseJob()
        {
            var cell = roster.Soldiers[0].Cell + Vector2Int.down;
            trenches.Designate(cell);
            trenches.Tick(0.01f);
            SoldierView worker = trenches.Jobs[0].Worker;
            movement.Selected = roster.Soldiers[1];
            movement.Selected.RequestDuty(SoldierDuty.Guard);
            Assert.IsTrue(movement.IssueMove(worker.Cell + Vector2Int.left));
            Assert.IsFalse(worker.IsMoving);
            worker.enabled = false;
            trenches.Tick(0.1f);
            Assert.IsFalse(worker.HasWorkAssignment);
            Assert.IsNull(trenches.Jobs[0].Worker);
        }

        [Test]
        public void InvalidDesignationsAndPauseDoNotProduceWork()
        {
            Assert.IsFalse(trenches.Designate(new Vector2Int(-1, 0)));
            var cell = new Vector2Int(50, 10);
            map.Data.SetWalkable(cell, false);
            Assert.IsFalse(trenches.Designate(cell));
            map.Data.SetWalkable(cell, true);
            trenches.Designate(cell);
            trenches.Tick(0f);
            Assert.IsNull(trenches.Jobs[0].Worker);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => trenches.Tick(float.NaN));
        }

        [Test]
        public void BlockedEarlierJobDoesNotStarveLaterReachableJob()
        {
            var blocked = new Vector2Int(50, 10);
            foreach (Vector2Int delta in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                map.Data.SetWalkable(blocked + delta, false);
            trenches.Designate(blocked);
            trenches.Designate(roster.Soldiers[0].Cell + Vector2Int.down);
            trenches.Tick(1f);
            Assert.IsNull(trenches.Jobs[0].Worker);
            Assert.IsNotNull(trenches.Jobs[1].Worker);
        }

        [Test]
        public void ManualMovementCannotClaimReservedWorkEndpoint()
        {
            trenches.Designate(new Vector2Int(50, 10));
            trenches.Tick(0.01f);
            Vector2Int reserved = trenches.Jobs[0].Worker.Destination;
            movement.Selected = roster.Soldiers[7];
            movement.Selected.RequestDuty(SoldierDuty.Guard);
            movement.Selected.AdvanceMovement(100f);
            LogAssert.Expect(LogType.Warning, "Destinazione gia occupata o prenotata.");
            Assert.IsFalse(movement.IssueMove(reserved));
        }

        [Test]
        public void GuardRequestWaitsForCurrentJobThenExcludesWorkerFromNextJob()
        {
            SoldierView worker = roster.Soldiers[0];
            Assert.AreEqual(9, roster.WorkCount);
            Assert.Zero(roster.GuardCount);
            trenches.Designate(worker.Cell + Vector2Int.down);
            trenches.Tick(0.01f);
            Assert.AreSame(worker, trenches.Jobs[0].Worker);
            worker.RequestDuty(SoldierDuty.Guard);
            Assert.IsTrue(worker.GuardRequested);
            Assert.AreEqual(SoldierDuty.Work, worker.Duty);
            Assert.AreEqual(9, roster.WorkCount);
            trenches.Tick(10f);
            Assert.AreEqual(SoldierDuty.Guard, worker.Duty);
            Assert.IsFalse(worker.GuardRequested);
            Assert.AreEqual(8, roster.WorkCount);
            Assert.AreEqual(1, roster.GuardCount);
            Assert.IsFalse(worker.CanTakeEngineeringWork);
            trenches.Designate(worker.Cell + Vector2Int.left);
            trenches.Tick(1f);
            Assert.AreNotSame(worker, trenches.Jobs[0].Worker);
            worker.RequestDuty(SoldierDuty.Work);
            Assert.IsTrue(worker.CanTakeEngineeringWork);
        }

        [Test]
        public void PendingGuardCanBeCancelledAndCookCannotLeaveWork()
        {
            SoldierView worker = roster.Soldiers[0];
            trenches.Designate(worker.Cell + Vector2Int.down);
            trenches.Tick(0.01f);
            worker.RequestDuty(SoldierDuty.Guard);
            worker.RequestDuty(SoldierDuty.Work);
            trenches.Tick(10f);
            Assert.AreEqual(SoldierDuty.Work, worker.Duty);
            Assert.IsFalse(worker.GuardRequested);
            LogAssert.Expect(LogType.Warning, "Il cuoco resta in Lavoro.");
            Assert.IsFalse(roster.Soldiers[8].RequestDuty(SoldierDuty.Guard));
            Assert.AreEqual(SoldierDuty.Work, roster.Soldiers[8].Duty);
        }

        [Test]
        public void GuardRequestDuringWorkTravelFinishesJobBeforeChangingState()
        {
            var target = new Vector2Int(50, 10);
            trenches.Designate(target);
            trenches.Tick(0.01f);
            SoldierView worker = trenches.Jobs[0].Worker;
            Assert.IsTrue(worker.IsMoving);
            worker.RequestDuty(SoldierDuty.Guard);
            Assert.IsTrue(worker.GuardRequested);
            Assert.AreEqual(SoldierDuty.Work, worker.Duty);
            worker.AdvanceMovement(100f);
            Assert.AreEqual(SoldierDuty.Work, worker.Duty);
            trenches.Tick(1f);
            Assert.IsTrue(worker.HasWorkAssignment);
            Assert.IsTrue(worker.GuardRequested);
            trenches.Tick(10f);
            Assert.IsTrue(map.Data.IsTrench(target));
            Assert.AreEqual(SoldierDuty.Guard, worker.Duty);
            Assert.AreEqual(1, roster.GuardCount);
        }

        [Test]
        public void SelectedWorkerAndMissingSelectionCannotReceiveGuardMoves()
        {
            movement.Selected = null;
            LogAssert.Expect(LogType.Warning, "Seleziona un soldato in Guardia.");
            Assert.IsFalse(movement.IssueMove(new Vector2Int(50, 10)));
            movement.Selected = roster.Soldiers[0];
            LogAssert.Expect(LogType.Warning, "Il soldato deve essere in Guardia e libero.");
            Assert.IsFalse(movement.IssueMove(new Vector2Int(50, 10)));
            Assert.IsFalse(roster.Soldiers[0].IsMoving);
            roster.Soldiers[0].RequestDuty(SoldierDuty.Guard);
            Assert.IsTrue(movement.IssueMove(new Vector2Int(50, 10)));
            roster.Soldiers[0].AdvanceMovement(100f);
            Assert.AreEqual(SoldierDuty.Guard, roster.Soldiers[0].Duty);
            Assert.IsFalse(roster.Soldiers[0].CanTakeEngineeringWork);
            Assert.IsTrue(movement.IssueMove(roster.Soldiers[0].Cell));
        }

        [Test]
        public void LadderWorkUsesAdjacentWorkerExactDurationExperienceAndPersistentTile()
        {
            SoldierView worker = roster.Soldiers[0];
            Vector2Int cell = worker.Cell + Vector2Int.down;
            view.CompleteTrench(map.Data, cell);
            Assert.IsFalse(map.Data.CanStep(worker.Cell, cell));
            Assert.IsTrue(trenches.DesignateLadder(cell));
            Assert.IsFalse(trenches.DesignateLadder(cell));
            trenches.Tick(0.01f);
            TrenchJob job = trenches.Jobs[0];
            Assert.AreEqual(TrenchJobKind.Ladder, job.Kind);
            Assert.AreSame(worker, job.Worker);
            Assert.IsFalse(worker.IsMoving);
            Assert.IsTrue(worker.HasWorkAssignment);
            Assert.AreEqual(TrenchJobKind.Ladder, worker.WorkKind);
            var strength = worker.Data.GetSkill(SoldierSkill.Strength);
            int xp = strength.Experience;
            float duration = 4f / (1f + strength.Value / 100f);
            trenches.Tick(duration / 2f);
            Assert.AreEqual(0.5f, job.Progress, 0.0001f);
            Assert.IsFalse(map.Data.HasLadder(cell));
            trenches.Tick(duration / 2f + 0.001f);
            Assert.Zero(trenches.Jobs.Count);
            Assert.IsTrue(worker.IsAvailable);
            Assert.AreEqual(xp + 25, strength.Experience);
            Assert.IsTrue(map.Data.HasLadder(cell));
            Assert.IsTrue(map.Data.CanStep(worker.Cell, cell));
            Assert.AreEqual("Ladder", view.Tilemap.GetTile(new Vector3Int(cell.x, cell.y, 0)).name);
            view.Render(map.Data);
            Assert.AreEqual("Ladder", view.Tilemap.GetTile(new Vector3Int(cell.x, cell.y, 0)).name);
            Assert.IsFalse(trenches.DesignateLadder(cell));
            worker.RequestDuty(SoldierDuty.Guard);
            movement.Selected = worker;
            Assert.IsTrue(movement.IssueMove(cell));
            worker.AdvanceMovement(100f);
            Assert.AreEqual(cell, worker.Cell);
        }

        [Test]
        public void LadderCancellationPreservesTrenchAndDoesNotOpenAccessOrAwardExperience()
        {
            var worker = roster.Soldiers[0];
            var cell = worker.Cell + Vector2Int.down;
            Assert.IsFalse(trenches.DesignateLadder(cell));
            Assert.IsFalse(trenches.DesignateLadder(new Vector2Int(-1, 0)));
            view.CompleteTrench(map.Data, cell);
            Assert.IsTrue(trenches.DesignateLadder(cell));
            trenches.Tick(0.01f);
            int xp = worker.Data.GetSkill(SoldierSkill.Strength).Experience;
            trenches.Tick(1f);
            Assert.IsTrue(trenches.Cancel(cell));
            Assert.IsTrue(worker.IsAvailable);
            Assert.IsTrue(map.Data.IsTrench(cell));
            Assert.IsFalse(map.Data.HasLadder(cell));
            Assert.IsFalse(map.Data.CanStep(worker.Cell, cell));
            Assert.AreEqual(xp, worker.Data.GetSkill(SoldierSkill.Strength).Experience);
        }

        [Test]
        public void LadderTravelDoesNotCountAsInstallationAndUnreachableWorkWaits()
        {
            var cell = new Vector2Int(50, 10);
            view.CompleteTrench(map.Data, cell);
            foreach (Vector2Int delta in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                map.Data.SetWalkable(cell + delta, false);
            trenches.DesignateLadder(cell);
            trenches.Tick(1f);
            Assert.IsNull(trenches.Jobs[0].Worker);
            Assert.AreEqual("Accesso bloccato o occupato", trenches.Jobs[0].Status);
            map.Data.SetWalkable(cell + Vector2Int.down, true);
            trenches.Tick(1f);
            var worker = trenches.Jobs[0].Worker;
            Assert.IsNotNull(worker);
            Assert.IsTrue(worker.IsMoving);
            trenches.Tick(10f);
            Assert.Zero(trenches.Jobs[0].Progress);
            worker.AdvanceMovement(100f);
            trenches.Tick(10f);
            Assert.IsTrue(map.Data.HasLadder(cell));
        }

        [Test]
        public void ExcavationWaitsForExistingRouteInsteadOfRemovingEdgesUnderMovingSoldier()
        {
            var cell = roster.Soldiers[0].Cell + Vector2Int.down;
            Assert.IsTrue(trenches.Designate(cell));
            trenches.Tick(0.01f);
            var worker = trenches.Jobs[0].Worker;
            var passer = roster.Soldiers[1];
            var route = new List<Vector2Int>(GridPathfinder.FindNearest(map.Data, new[] { passer.Cell }, cell).Cells);
            route.Add(cell + Vector2Int.left);
            passer.BeginMove(view, route);
            trenches.Tick(10f);
            Assert.AreEqual(1f, trenches.Jobs[0].Progress);
            Assert.AreEqual("Attesa: cella o percorso occupato", trenches.Jobs[0].Status);
            Assert.IsFalse(map.Data.IsTrench(cell));
            passer.AdvanceMovement(100f);
            trenches.Tick(0.1f);
            Assert.IsTrue(map.Data.IsTrench(cell));
            Assert.IsTrue(worker.IsAvailable);
        }
    }
}
