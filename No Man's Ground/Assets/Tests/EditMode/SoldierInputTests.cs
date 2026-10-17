using System.Collections;
using System.Reflection;
using NoMansGround.Map;
using NoMansGround.Soldiers;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;
using UnityEngine.TestTools;

namespace NoMansGround.Tests
{
    public sealed class SoldierInputTests : InputTestFixture
    {
        [UnityTest]
        public IEnumerator KitchenClickTakesPrecedenceOverNearbyCookAtDistantZoom()
        {
            EditorSceneManager.OpenScene(NoMansGround.Editor.MapPrototypeSetup.ScenePath);
            yield return new EnterPlayMode();
            yield return null;
            var kitchen = Object.FindAnyObjectByType<KitchenSystem>();
            var panel = Object.FindAnyObjectByType<SoldierDetailsPanel>();
            var view = Object.FindAnyObjectByType<MapTilemapView>();
            kitchen.Cook.AdvanceMovement(100f);
            kitchen.Tick(30f);
            Camera camera = Camera.main;
            var viewport = new RenderTexture(800, 600, 0);
            try
            {
                camera.targetTexture = viewport;
                camera.orthographicSize = 24f;
                camera.GetComponent<MapCameraController>().FocusOn(new Vector3(64f, 24f, 0f));
                Vector2 pointer = camera.WorldToScreenPoint(view.Tilemap.GetCellCenterWorld(
                    new Vector3Int(kitchen.KitchenCell.x, kitchen.KitchenCell.y, 0)));
                Assert.AreSame(kitchen.Cook, panel.FindAtScreenPosition(pointer),
                    "The cook must be inside the soldier selection radius for this regression.");
                Mouse mouse = InputSystem.AddDevice<Mouse>();
                InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState
                    { position = pointer, buttons = 1 });
                yield return null;
                yield return null;
                Assert.IsNull(panel.Selected, "Clicking the kitchen selects its stock, not the nearby cook.");
                bool shown = false;
                foreach (UnityEngine.UI.Text text in GameObject.Find("Soldier Interface")
                    .GetComponentsInChildren<UnityEngine.UI.Text>())
                    if (text.text.Contains("Dispensa non designata"))
                        shown = true;
                Assert.IsTrue(shown);
                InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState { position = pointer });
            }
            finally
            {
                camera.targetTexture = null;
                Object.DestroyImmediate(viewport);
            }
            yield return new ExitPlayMode();
        }

        [Test]
        public void LeftClickSelectsSoldierAndEmptyTerrainClearsSelection()
        {
            var root = new GameObject("Test soldiers", typeof(Grid), typeof(MapPrototype));
            var terrain = new GameObject("Ground", typeof(Tilemap), typeof(TilemapRenderer), typeof(MapTilemapView));
            terrain.transform.SetParent(root.transform, false);
            var cameraObject = new GameObject("Camera", typeof(Camera));
            cameraObject.transform.SetParent(root.transform, false);
            var rosterObject = new GameObject("Roster", typeof(SoldierRoster));
            rosterObject.transform.SetParent(root.transform, false);
            var panelObject = new GameObject("Selection", typeof(SoldierDetailsPanel));
            panelObject.transform.SetParent(root.transform, false);
            var viewport = new RenderTexture(800, 600, 0);
            try
            {
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.targetTexture = viewport;
                camera.orthographic = true;
                camera.orthographicSize = 24f;
                camera.transform.position = new Vector3(64f, 24f, -10f);
                var roster = rosterObject.GetComponent<SoldierRoster>();
                roster.Configure(root.GetComponent<MapPrototype>(), terrain.GetComponent<MapTilemapView>(),
                    AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Prototype/Soldier.png"));
                roster.Spawn(new MapData(128, 128));
                var panel = panelObject.GetComponent<SoldierDetailsPanel>();
                panel.Configure(roster, camera);
                Mouse mouse = InputSystem.AddDevice<Mouse>();
                SoldierView soldier = roster.Soldiers[1];
                Set(mouse.position, (Vector2)camera.WorldToScreenPoint(soldier.transform.position));
                Press(mouse.leftButton);
                UpdateSelection(panel);
                Assert.AreSame(soldier, panel.Selected);
                Assert.IsTrue(soldier.IsSelected);
                Release(mouse.leftButton);
                Set(mouse.position, new Vector2(10f, 10f));
                Press(mouse.leftButton);
                UpdateSelection(panel);
                Assert.IsNull(panel.Selected);
                Assert.IsFalse(soldier.IsSelected);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(viewport);
            }
        }

        [Test]
        public void ZoneModeDesignatesTheRectangleDraggedWithTheLeftMouseButton()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var scene = EditorSceneManager.OpenScene(NoMansGround.Editor.MapPrototypeSetup.ScenePath,
                OpenSceneMode.Additive);
            var viewport = new RenderTexture(800, 600, 0);
            try
            {
                MapPrototype map = null;
                SoldierRoster roster = null;
                SoldierDetailsPanel panel = null;
                Camera camera = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (root.TryGetComponent(out MapPrototype prototype)) map = prototype;
                    if (root.TryGetComponent(out SoldierRoster soldiers)) roster = soldiers;
                    if (root.TryGetComponent(out SoldierDetailsPanel selection)) panel = selection;
                    if (root.TryGetComponent(out Camera foundCamera)) camera = foundCamera;
                }
                map.Regenerate();
                roster.Spawn(map.Data);
                camera.targetTexture = viewport;
                camera.GetComponent<MapCameraController>().FocusOn(new Vector3(64f, 64f, 0f));
                var view = map.GetComponentInChildren<MapTilemapView>();
                Vector2Int start = FindEmptyRectangle(map.Data, 3, 2);
                Vector2Int end = start + new Vector2Int(2, 1);
                Mouse mouse = InputSystem.AddDevice<Mouse>();
                panel.SetZoneMode(MapZoneKind.Dump);
                Set(mouse.position, (Vector2)camera.WorldToScreenPoint(view.Tilemap.GetCellCenterWorld(
                    new Vector3Int(start.x, start.y, 0))));
                Press(mouse.leftButton);
                UpdateSelection(panel);
                Set(mouse.position, (Vector2)camera.WorldToScreenPoint(view.Tilemap.GetCellCenterWorld(
                    new Vector3Int(end.x, end.y, 0))));
                UpdateSelection(panel);
                Release(mouse.leftButton);
                UpdateSelection(panel);
                for (int y = start.y; y <= end.y; y++)
                    for (int x = start.x; x <= end.x; x++)
                    {
                        Assert.IsTrue(map.Data.TryGetZone(new Vector2Int(x, y), out MapZoneKind kind));
                        Assert.AreEqual(MapZoneKind.Dump, kind);
                    }
                var tilePosition = new Vector3Int(start.x, start.y, 0);
                Color zoneTint = Color.Lerp(Color.white, MapTilemapView.ZoneColor(MapZoneKind.Dump), 0.42f);
                Assert.AreEqual(zoneTint, view.Tilemap.GetColor(tilePosition));
                view.RenderZones(map.Data);
                Assert.AreEqual(zoneTint, view.Tilemap.GetColor(tilePosition));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                Object.DestroyImmediate(viewport);
            }
        }

        private static Vector2Int FindEmptyRectangle(MapData map, int width, int height)
        {
            for (int y = 8; y < map.Height - height; y++)
                for (int x = 8; x < map.Width - width; x++)
                {
                    bool valid = true;
                    for (int dy = 0; dy < height && valid; dy++)
                        for (int dx = 0; dx < width; dx++)
                        {
                            var cell = new Vector2Int(x + dx, y + dy);
                            if (!map.IsWalkable(cell) || map.IsTrench(cell)
                                || map[cell.x, cell.y] == TerrainKind.Road)
                            {
                                valid = false;
                                break;
                            }
                        }
                    if (valid)
                        return new Vector2Int(x, y);
                }
            Assert.Fail("No valid terrain rectangle found.");
            return default;
        }

        private static void UpdateSelection(SoldierDetailsPanel panel)
        {
            typeof(SoldierDetailsPanel).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(panel, null);
        }

        [Test]
        public void RightClickMovesOnlySelectedGuardAndShiftRightClickEditsObstacles()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var scene = EditorSceneManager.OpenScene(NoMansGround.Editor.MapPrototypeSetup.ScenePath, OpenSceneMode.Additive);
            var viewport = new RenderTexture(800, 600, 0);
            try
            {
                MapPrototype map = null;
                SoldierRoster roster = null;
                SoldierMovementOrders orders = null;
                Camera camera = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (root.TryGetComponent(out MapPrototype foundMap))
                        map = foundMap;
                    if (root.TryGetComponent(out SoldierRoster foundRoster))
                    {
                        roster = foundRoster;
                        orders = root.GetComponent<SoldierMovementOrders>();
                    }
                    if (root.TryGetComponent(out Camera foundCamera))
                        camera = foundCamera;
                }
                Assert.IsNotNull(map);
                Assert.IsNotNull(orders);
                map.Regenerate();
                roster.Spawn(map.Data);
                camera.targetTexture = viewport;
                camera.GetComponent<MapCameraController>().FocusOn(new Vector3(64f, 24f, 0f));
                var view = map.GetComponentInChildren<MapTilemapView>();
                Mouse mouse = InputSystem.AddDevice<Mouse>();
                Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
                var cell = new Vector2Int(64, 10);
                Set(mouse.position, (Vector2)camera.WorldToScreenPoint(
                    view.Tilemap.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0))));
                Press(keyboard.leftShiftKey);
                Press(mouse.rightButton);
                UpdateOrders(orders);
                Assert.IsFalse(map.Data.IsWalkable(cell));
                Release(mouse.rightButton);
                Press(mouse.rightButton);
                UpdateOrders(orders);
                Assert.IsTrue(map.Data.IsWalkable(cell));
                Release(mouse.rightButton);
                Release(keyboard.leftShiftKey);
                orders.Selected = roster.Soldiers[7];
                orders.Selected.RequestDuty(SoldierDuty.Guard);
                Press(mouse.rightButton);
                UpdateOrders(orders);
                int moving = 0;
                foreach (SoldierView soldier in roster.Soldiers)
                {
                    if (!soldier.IsMoving)
                        continue;
                    moving++;
                    Assert.AreEqual(cell, soldier.Destination);
                }
                Assert.AreEqual(1, moving);
                Assert.IsTrue(roster.Soldiers[7].IsMoving,
                    "The selected guard must move, not the worker nearest to the destination.");
                SoldierView guard = orders.Selected;
                guard.AdvanceMovement(0.4f / guard.MovementSpeed);
                Vector3 position = guard.transform.position;
                Release(mouse.rightButton);
                var latest = new Vector2Int(60, 15);
                Set(mouse.position, (Vector2)camera.WorldToScreenPoint(
                    view.Tilemap.GetCellCenterWorld(new Vector3Int(latest.x, latest.y, 0))));
                Press(mouse.rightButton);
                UpdateOrders(orders);
                Assert.AreEqual(latest, guard.Destination);
                Assert.AreEqual(position, guard.transform.position);
                guard.AdvanceMovement(100f);
                Assert.AreEqual(latest, guard.Cell);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                Object.DestroyImmediate(viewport);
            }
        }

        private static void UpdateOrders(SoldierMovementOrders orders)
        {
            typeof(SoldierMovementOrders).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(orders, null);
        }

        [Test]
        public void DigModeUsesKeyboardDragAndCancelWithoutLeakingRightClickOrders()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var scene = EditorSceneManager.OpenScene(NoMansGround.Editor.MapPrototypeSetup.ScenePath, OpenSceneMode.Additive);
            var viewport = new RenderTexture(800, 600, 0);
            try
            {
                MapPrototype map = null;
                SoldierRoster roster = null;
                Camera camera = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (root.TryGetComponent(out MapPrototype prototype)) map = prototype;
                    if (root.TryGetComponent(out SoldierRoster soldiers)) roster = soldiers;
                    if (root.TryGetComponent(out Camera foundCamera)) camera = foundCamera;
                }
                map.Regenerate();
                roster.Spawn(map.Data);
                var trenches = roster.GetComponent<TrenchWorkOrders>();
                var orders = roster.GetComponent<SoldierMovementOrders>();
                var view = map.GetComponentInChildren<MapTilemapView>();
                camera.targetTexture = viewport;
                camera.GetComponent<MapCameraController>().FocusOn(new Vector3(64f, 24f, 0f));
                var mouse = InputSystem.AddDevice<Mouse>();
                var keyboard = InputSystem.AddDevice<Keyboard>();
                Press(keyboard.tKey);
                UpdateTrenches(trenches);
                Assert.AreEqual(DiggingMode.Dig, trenches.Mode);
                Release(keyboard.tKey);
                Set(mouse.position, (Vector2)camera.WorldToScreenPoint(
                    view.Tilemap.GetCellCenterWorld(new Vector3Int(60, 10, 0))));
                Press(mouse.leftButton);
                UpdateTrenches(trenches);
                Assert.Zero(trenches.Jobs.Count, "Dragging only previews until release.");
                Set(mouse.position, (Vector2)camera.WorldToScreenPoint(
                    view.Tilemap.GetCellCenterWorld(new Vector3Int(64, 10, 0))));
                UpdateTrenches(trenches);
                Release(mouse.leftButton);
                UpdateTrenches(trenches);
                Assert.AreEqual(5, trenches.Jobs.Count);
                Press(mouse.rightButton);
                UpdateTrenches(trenches);
                UpdateOrders(orders);
                Assert.AreEqual(DiggingMode.None, trenches.Mode);
                foreach (SoldierView soldier in roster.Soldiers)
                    Assert.IsFalse(soldier.IsMoving, "Exiting dig mode must not issue a move.");
                Release(mouse.rightButton);
                Press(keyboard.xKey);
                UpdateTrenches(trenches);
                Release(keyboard.xKey);
                Assert.AreEqual(DiggingMode.Cancel, trenches.Mode);
                Press(mouse.leftButton);
                UpdateTrenches(trenches);
                Release(mouse.leftButton);
                UpdateTrenches(trenches);
                Assert.AreEqual(4, trenches.Jobs.Count);
                Press(keyboard.escapeKey);
                UpdateTrenches(trenches);
                Assert.AreEqual(DiggingMode.None, trenches.Mode);
                Release(keyboard.escapeKey);
                var ladderCell = new Vector2Int(68, 10);
                view.CompleteTrench(map.Data, ladderCell);
                Press(keyboard.lKey);
                UpdateTrenches(trenches);
                Release(keyboard.lKey);
                Assert.AreEqual(DiggingMode.Ladder, trenches.Mode);
                Set(mouse.position, (Vector2)camera.WorldToScreenPoint(
                    view.Tilemap.GetCellCenterWorld(new Vector3Int(ladderCell.x, ladderCell.y, 0))));
                Press(mouse.leftButton);
                UpdateTrenches(trenches);
                Assert.AreEqual(5, trenches.Jobs.Count);
                Assert.AreEqual(TrenchJobKind.Ladder, trenches.Jobs[4].Kind);
                Assert.AreEqual(ladderCell, trenches.Jobs[4].Cell);
                Release(mouse.leftButton);
                UpdateTrenches(trenches);
                Assert.AreEqual(5, trenches.Jobs.Count, "A release must not duplicate the ladder order.");
                Press(keyboard.lKey);
                UpdateTrenches(trenches);
                Assert.AreEqual(DiggingMode.None, trenches.Mode);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                Object.DestroyImmediate(viewport);
            }
        }

        private static void UpdateTrenches(TrenchWorkOrders orders)
        {
            typeof(TrenchWorkOrders).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(orders, null);
        }
    }
}
