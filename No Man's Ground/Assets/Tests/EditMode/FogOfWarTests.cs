using NoMansGround.Map;
using NoMansGround.Editor;
using NoMansGround.Soldiers;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace NoMansGround.Tests
{
    public sealed class FogOfWarTests
    {
        [Test]
        public void OneHundredEightyDegreeConeExcludesRearAndRotatesWithObserver()
        {
            var map = new MapData(25, 25);
            var origin = new Vector2Int(12, 12);
            map.Fog.Enable();
            map.Fog.Update(map, new[] { new FogObserver(origin, Vector2.up) }, 8);
            Assert.IsTrue(map.Fog.IsVisible(origin));
            Assert.IsTrue(map.Fog.IsVisible(origin + new Vector2Int(0, 7)));
            Assert.IsTrue(map.Fog.IsVisible(origin + new Vector2Int(7, 0)));
            Assert.IsTrue(map.Fog.IsVisible(origin + new Vector2Int(-7, 0)));
            Assert.IsFalse(map.Fog.IsVisible(origin + new Vector2Int(7, -1)));
            Assert.IsFalse(map.Fog.IsVisible(origin + new Vector2Int(6, -2)));
            Assert.IsFalse(map.Fog.IsVisible(origin + new Vector2Int(0, -7)));
            map.Fog.Update(map, new[] { new FogObserver(origin, Vector2.down) }, 8);
            Assert.IsTrue(map.Fog.IsVisible(origin + new Vector2Int(0, -7)));
            Assert.IsFalse(map.Fog.IsVisible(origin + new Vector2Int(0, 7)));
            Assert.IsTrue(map.Fog.IsExplored(origin + new Vector2Int(0, 7)));
        }

        [Test]
        public void DirectionalObserversShareVisionWithoutSeeingThroughSolids()
        {
            var map = new MapData(25, 25);
            var origin = new Vector2Int(12, 12);
            map[12, 14] = TerrainKind.Mountain;
            map.Fog.Enable();
            map.Fog.Update(map, new[]
            {
                new FogObserver(origin, Vector2.up),
                new FogObserver(origin, Vector2.down)
            }, 8);
            Assert.IsTrue(map.Fog.IsVisible(new Vector2Int(12, 14)));
            Assert.IsFalse(map.Fog.IsVisible(new Vector2Int(12, 15)));
            Assert.IsTrue(map.Fog.IsVisible(new Vector2Int(12, 5)));
            Assert.Throws<System.ArgumentException>(() => new FogObserver(origin, Vector2.zero));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new FogObserver(origin, Vector2.up, 361f));
        }

        [Test]
        public void VisionUsesSharedRadiusAndRetainsExplorationAfterObserversLeave()
        {
            var map = new MapData(30, 20);
            map.Fog.Enable();
            Assert.IsFalse(map.Fog.IsExplored(new Vector2Int(3, 3)));
            map.Fog.Update(map, new[] { new Vector2Int(3, 3), new Vector2Int(20, 10) }, 4);
            Assert.IsTrue(map.Fog.IsVisible(new Vector2Int(3, 3)));
            Assert.IsTrue(map.Fog.IsVisible(new Vector2Int(7, 3)));
            Assert.IsFalse(map.Fog.IsVisible(new Vector2Int(7, 7)));
            Assert.IsTrue(map.Fog.IsVisible(new Vector2Int(20, 10)));
            map.Fog.Update(map, new[] { new Vector2Int(20, 10) }, 4);
            Assert.IsFalse(map.Fog.IsVisible(new Vector2Int(3, 3)));
            Assert.IsTrue(map.Fog.IsExplored(new Vector2Int(3, 3)));
            Assert.IsFalse(map.Fog.IsExplored(new Vector2Int(29, 19)));
            map.Fog.Update(map, new Vector2Int[0], 4);
            Assert.IsFalse(map.Fog.IsVisible(new Vector2Int(20, 10)));
            Assert.IsTrue(map.Fog.IsExplored(new Vector2Int(20, 10)));
        }

        [Test]
        public void BuildingAndWallCanBeDesignatedOnRememberedTerrain()
        {
            var map = new MapData(40, 32);
            map.Fog.Enable();
            map.Fog.Update(map, new[] { new FogObserver(new Vector2Int(8, 8), Vector2.up, 360f) }, 10);
            map.Fog.Update(map, new[] { new FogObserver(new Vector2Int(30, 25), Vector2.up, 360f) }, 2);
            Assert.IsTrue(map.Fog.IsExplored(new Vector2Int(7, 7)));
            Assert.IsFalse(map.Fog.IsVisible(new Vector2Int(7, 7)));
            Assert.IsTrue(map.TryPlanBuilding(new Vector2Int(5, 5), new Vector2Int(9, 9),
                ItemType.Wood, out _));

            var wallCell = new Vector2Int(10, 8);
            Assert.IsTrue(map.Fog.IsExplored(wallCell));
            Assert.IsFalse(map.Fog.IsVisible(wallCell));
            Assert.IsTrue(map.TryPlanWall(wallCell, ItemType.BuildingStone, out _));
        }

        [TestCase(TerrainKind.Rock)]
        [TestCase(TerrainKind.Mountain)]
        public void SolidTerrainIsVisibleButHidesCellsBehindItAndDiagonalCorners(TerrainKind terrain)
        {
            var map = new MapData(10, 10);
            map[4, 3] = terrain;
            map[3, 4] = terrain;
            map.Fog.Enable();
            map.Fog.Update(map, new[] { new Vector2Int(3, 3) }, 6);
            Assert.IsTrue(map.Fog.IsVisible(new Vector2Int(4, 3)));
            Assert.IsFalse(map.Fog.IsVisible(new Vector2Int(5, 3)));
            Assert.IsFalse(map.Fog.IsVisible(new Vector2Int(4, 4)));
            map[4, 3] = terrain == TerrainKind.Mountain ? TerrainKind.Tunnel : TerrainKind.Dirt;
            map.Fog.Update(map, new[] { new Vector2Int(3, 3) }, 6);
            Assert.IsTrue(map.Fog.IsVisible(new Vector2Int(5, 3)));
        }

        [Test]
        public void BuiltWallsAndDoorsBlockSightButPlansDoNot()
        {
            var map = new MapData(12, 12);
            Assert.IsTrue(map.TryPlanBuilding(new Vector2Int(3, 3), new Vector2Int(7, 7), ItemType.Wood, out _));
            Assert.IsTrue(map.TrySetBuildingDoor(new Vector2Int(5, 3)));
            map.Fog.Enable();
            map.Fog.Update(map, new[] { new Vector2Int(5, 2) }, 6);
            Assert.IsTrue(map.Fog.IsVisible(new Vector2Int(5, 4)));
            Assert.IsTrue(map.CompleteBuildingTile(new Vector2Int(5, 3)));
            map.Fog.Update(map, new[] { new Vector2Int(5, 2) }, 6);
            Assert.IsTrue(map.Fog.IsVisible(new Vector2Int(5, 3)));
            Assert.IsTrue(map.Fog.IsVisible(new Vector2Int(5, 4)));
            Assert.IsFalse(map.Fog.IsVisible(new Vector2Int(5, 6)));
            map.Fog.Update(map, new[] { new Vector2Int(5, 4) }, 6);
            Assert.IsTrue(map.Fog.IsVisible(new Vector2Int(5, 5)));
        }

        [Test]
        public void BuiltWallTilesRevealTwoTilesInEveryDirection()
        {
            var map = new MapData(20, 20);
            Assert.IsTrue(map.TryPlanBuilding(new Vector2Int(8, 8), new Vector2Int(10, 10),
                ItemType.Wood, out BuildingRoom building));
            Assert.IsTrue(map.TrySetBuildingDoor(new Vector2Int(9, 8)));
            map.Fog.Enable();
            map.Fog.Update(map, new FogObserver[0], 8);
            Assert.IsFalse(map.Fog.IsVisible(new Vector2Int(9, 6)));

            Assert.IsTrue(map.CompleteBuildingTile(new Vector2Int(8, 9)));
            Assert.IsTrue(map.CompleteBuildingTile(new Vector2Int(9, 8)));
            Assert.IsTrue(map.CompleteBuildingTile(new Vector2Int(10, 9)));
            Assert.IsTrue(map.CompleteBuildingTile(new Vector2Int(9, 10)));
            Assert.IsFalse(building.IsComplete);

            map.Fog.Update(map, new FogObserver[0], 8);
            Assert.IsTrue(map.Fog.IsVisible(new Vector2Int(9, 6)));
            Assert.IsTrue(map.Fog.IsVisible(new Vector2Int(9, 12)));
            Assert.IsTrue(map.Fog.IsVisible(new Vector2Int(6, 9)));
            Assert.IsTrue(map.Fog.IsVisible(new Vector2Int(12, 9)));
            Assert.IsFalse(map.Fog.IsVisible(new Vector2Int(9, 5)));
            Assert.IsTrue(map.Fog.IsExplored(new Vector2Int(9, 6)));
        }

        [Test]
        public void RenderingRemembersLastSeenTerrainAndCoversResourcesOutsideSight()
        {
            EditorSceneManager.OpenScene(MapPrototypeSetup.ScenePath);
            try
            {
                var view = Object.FindAnyObjectByType<MapTilemapView>();
                var map = new MapData(20, 12);
                var remembered = new Vector2Int(4, 3);
                map.AddGroundItem(remembered, ItemType.Wood);
                map.Fog.Enable();
                map.Fog.Update(map, new[] { new Vector2Int(3, 3) }, 3);
                view.Render(map);
                Tilemap fog = view.transform.Find("Fog of War").GetComponent<Tilemap>();
                Tilemap memory = view.transform.Find("Explored Terrain").GetComponent<Tilemap>();
                Assert.IsFalse(fog.HasTile(new Vector3Int(4, 3, 0)));
                Assert.IsTrue(fog.HasTile(new Vector3Int(15, 3, 0)));
                map.Fog.Update(map, new[] { new Vector2Int(15, 3) }, 3);
                map[4, 3] = TerrainKind.Grass;
                view.Render(map);
                Assert.IsTrue(fog.HasTile(new Vector3Int(4, 3, 0)));
                Assert.AreEqual("Dirt", memory.GetTile(new Vector3Int(4, 3, 0)).name);
                Assert.Less(memory.GetColor(new Vector3Int(4, 3, 0)).r, 0.5f);
                Assert.Greater(fog.GetComponent<TilemapRenderer>().sortingOrder, 20);
                map.Fog.Disable();
                view.RenderFog(map);
                Assert.IsFalse(fog.HasTile(new Vector3Int(4, 3, 0)));
                Assert.IsFalse(memory.HasTile(new Vector3Int(4, 3, 0)));
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        [Test]
        public void SoldiersShareVisionAndRegenerationClearsOldExploration()
        {
            EditorSceneManager.OpenScene(MapPrototypeSetup.ScenePath);
            try
            {
                var map = Object.FindAnyObjectByType<MapPrototype>();
                var roster = Object.FindAnyObjectByType<SoldierRoster>();
                var view = map.GetComponentInChildren<MapTilemapView>();
                map.Regenerate();
                roster.Spawn(map.Data);
                var fog = roster.gameObject.AddComponent<SoldierFogOfWar>();
                fog.Configure(map, view, roster);
                foreach (SoldierView soldier in roster.Soldiers)
                    Assert.IsTrue(map.Data.Fog.IsVisible(soldier.Cell));
                Assert.IsFalse(map.Data.Fog.IsExplored(new Vector2Int(120, 120)));
                map.Data.Fog.Update(map.Data, new[] { new Vector2Int(120, 120) }, 3);
                Assert.IsTrue(map.Data.Fog.IsExplored(new Vector2Int(120, 120)));
                map.Regenerate();
                roster.Spawn(map.Data);
                fog.Refresh();
                Assert.IsFalse(map.Data.Fog.IsExplored(new Vector2Int(120, 120)));
                Assert.IsTrue(map.Data.Fog.IsVisible(roster.Soldiers[0].Cell));
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        [Test]
        public void UnknownCellsRejectOrdersAndVisibleResourcesCanBeDesignated()
        {
            EditorSceneManager.OpenScene(MapPrototypeSetup.ScenePath);
            try
            {
                var map = Object.FindAnyObjectByType<MapPrototype>();
                var roster = Object.FindAnyObjectByType<SoldierRoster>();
                var view = map.GetComponentInChildren<MapTilemapView>();
                map.Regenerate();
                roster.Spawn(map.Data);
                for (int y = 6; y <= 14; y++)
                    for (int x = 45; x <= 70; x++)
                    {
                        map.Data[x, y] = TerrainKind.Dirt;
                        map.Data.SetTree(new Vector2Int(x, y), false);
                        map.Data.SetWalkable(new Vector2Int(x, y), true);
                    }
                var dig = new Vector2Int(50, 10);
                var resource = new Vector2Int(51, 10);
                var rock = new Vector2Int(52, 10);
                var tree = new Vector2Int(50, 12);
                map.Data.AddGroundItem(resource, ItemType.Wood);
                map.Data[rock.x, rock.y] = TerrainKind.Rock;
                map.Data.SetTree(tree, true);
                view.Render(map.Data);
                var orders = roster.GetComponent<TrenchWorkOrders>();
                var movement = roster.GetComponent<SoldierMovementOrders>();
                movement.Selected = roster.Soldiers[0];
                movement.Selected.RequestDuty(SoldierDuty.Guard);
                map.Data.Fog.Enable();
                Assert.IsFalse(orders.Designate(dig));
                Assert.IsFalse(orders.DesignateCollection(resource));
                Assert.IsFalse(orders.DesignateMine(rock));
                Assert.IsFalse(orders.DesignateTree(tree));
                Assert.IsFalse(orders.DesignateDoor(dig));
                Assert.IsFalse(movement.IssueMove(new Vector2Int(49, 10)));
                Assert.IsFalse(map.Data.TryDesignateZone(MapZoneKind.Dump, new Vector2Int(55, 11), new Vector2Int(57, 13), out _));
                Assert.IsFalse(orders.DesignateBuilding(new Vector2Int(55, 11), new Vector2Int(57, 13), ItemType.Wood));
                Assert.Zero(orders.Jobs.Count);
                map.Data.Fog.Update(map.Data, new[] { new Vector2Int(51, 11) }, 4);
                Assert.IsTrue(orders.Designate(dig));
                Assert.IsTrue(orders.DesignateCollection(resource));
                Assert.IsTrue(orders.DesignateMine(rock));
                Assert.IsTrue(orders.DesignateTree(tree));
                Assert.IsTrue(movement.IssueMove(new Vector2Int(49, 10)));
                Assert.AreEqual(4, orders.Jobs.Count);
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        [TestCase(1280, 720)]
        [TestCase(600, 1000)]
        public void RenderedFogIsOpaqueAndRememberedTerrainDoesNotRevealHiddenChanges(int width, int height)
        {
            EditorSceneManager.OpenScene(MapPrototypeSetup.ScenePath);
            Camera camera = Camera.main;
            var target = new RenderTexture(width, height, 24);
            var pixels = new Texture2D(width, height, TextureFormat.RGBA32, false);
            RenderTexture previous = RenderTexture.active;
            RenderTexture oldTarget = camera.targetTexture;
            Vector3 oldPosition = camera.transform.position;
            float oldSize = camera.orthographicSize;
            GameObject hidden = null;
            try
            {
                var view = Object.FindAnyObjectByType<MapTilemapView>();
                var map = new MapData(20, 12);
                map.Fog.Enable();
                map.Fog.Update(map, new[] { new Vector2Int(3, 3) }, 3);
                view.Render(map);
                hidden = new GameObject("Hidden object", typeof(SpriteRenderer));
                var renderer = hidden.GetComponent<SpriteRenderer>();
                renderer.sprite = view.Tilemap.GetSprite(new Vector3Int(4, 3, 0));
                renderer.sortingOrder = 40;
                renderer.color = Color.red;
                hidden.transform.position = view.Tilemap.GetCellCenterWorld(new Vector3Int(4, 3, 0));
                map.Fog.Update(map, new[] { new Vector2Int(15, 3) }, 3);
                view.RenderFog(map);
                camera.targetTexture = target;
                camera.orthographicSize = Mathf.Max(8f, 12f * height / width);
                camera.transform.position = new Vector3(10f, 6f, -10f);
                Color Sample(Vector2Int cell)
                {
                    Vector3 point = camera.WorldToScreenPoint(view.Tilemap.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0)));
                    return pixels.GetPixel(Mathf.FloorToInt(point.x), Mathf.FloorToInt(point.y));
                }
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                pixels.Apply();
                Color memory = Sample(new Vector2Int(4, 3));
                Color visible = Sample(new Vector2Int(15, 3));
                Assert.Less(Sample(new Vector2Int(10, 10)).maxColorComponent, 0.01f);
                Assert.Greater(visible.grayscale, memory.grayscale);
                Assert.Greater(memory.grayscale, 0.001f);
                map[4, 3] = TerrainKind.Grass;
                renderer.color = Color.green;
                view.Render(map);
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                pixels.Apply();
                Assert.Less(((Vector4)memory - (Vector4)Sample(new Vector2Int(4, 3))).sqrMagnitude, 0.0001f);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                    $"NoMansGround-Fog-{width}x{height}.png"), pixels.EncodeToPNG());
                hidden.SetActive(false);
                var origin = new Vector2Int(10, 6);
                Vector2 leftLook = Quaternion.Euler(0, 0, 45f) * Vector2.up;
                Vector2 rightLook = Quaternion.Euler(0, 0, -45f) * Vector2.up;
                map.Fog.Enable();
                map.Fog.Update(map, new[] { new FogObserver(origin, leftLook) }, 8);
                view.RenderFog(map);
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                pixels.Apply();
                Color leftFlank = Sample(new Vector2Int(4, 3));
                Assert.Greater(leftFlank.grayscale, 0.02f);
                Assert.Less(Sample(new Vector2Int(16, 3)).maxColorComponent, 0.01f);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                    $"NoMansGround-GuardLeft-{width}x{height}.png"), pixels.EncodeToPNG());
                map.Fog.Update(map, new[] { new FogObserver(origin, rightLook) }, 8);
                view.RenderFog(map);
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                pixels.Apply();
                Assert.Greater(Sample(new Vector2Int(16, 3)).grayscale, 0.02f);
                Assert.Less(Sample(new Vector2Int(4, 3)).grayscale, leftFlank.grayscale);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                    $"NoMansGround-GuardRight-{width}x{height}.png"), pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = oldTarget;
                camera.transform.position = oldPosition;
                camera.orthographicSize = oldSize;
                if (hidden != null) Object.DestroyImmediate(hidden);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(pixels);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        [Test]
        public void GuardSweepChangesVisibleFlanksWithoutChangingPositionAndStopsWhenWorkingOrPaused()
        {
            EditorSceneManager.OpenScene(MapPrototypeSetup.ScenePath);
            try
            {
                var map = Object.FindAnyObjectByType<MapPrototype>();
                var roster = Object.FindAnyObjectByType<SoldierRoster>();
                map.Regenerate();
                roster.Spawn(map.Data);
                SoldierView soldier = roster.Soldiers[0];
                Vector3 position = soldier.transform.position;
                Vector2 facing = soldier.FacingDirection;
                soldier.RequestDuty(SoldierDuty.Guard);
                soldier.AdvanceLook(1.5f);
                Vector2 left = soldier.LookDirection;
                Assert.AreEqual(45f, Vector2.SignedAngle(facing, left), 0.001f);
                soldier.AdvanceLook(0f);
                Assert.AreEqual(left, soldier.LookDirection);
                soldier.AdvanceLook(3f);
                Vector2 right = soldier.LookDirection;
                Assert.AreEqual(-45f, Vector2.SignedAngle(facing, right), 0.001f);
                soldier.AdvanceLook(3f);
                Assert.Less(Vector2.Distance(left, soldier.LookDirection), 0.0001f);
                Assert.AreEqual(position, soldier.transform.position);
                var ground = new MapData(25, 25);
                var center = new Vector2Int(12, 12);
                ground.Fog.Enable();
                ground.Fog.Update(ground, new[] { new FogObserver(center, left) }, 8);
                Assert.IsTrue(ground.Fog.IsVisible(new Vector2Int(6, 9)));
                Assert.IsFalse(ground.Fog.IsVisible(new Vector2Int(18, 9)));
                ground.Fog.Update(ground, new[] { new FogObserver(center, right) }, 8);
                Assert.IsFalse(ground.Fog.IsVisible(new Vector2Int(6, 9)));
                Assert.IsTrue(ground.Fog.IsVisible(new Vector2Int(18, 9)));
                Assert.IsTrue(ground.Fog.IsExplored(new Vector2Int(6, 9)));
                soldier.RequestDuty(SoldierDuty.Work);
                soldier.AdvanceLook(3f);
                Assert.AreEqual(facing, soldier.LookDirection);
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        [Test]
        public void MovingSoldierLooksAlongCurrentStepAndKeepsLastDirectionOnArrival()
        {
            EditorSceneManager.OpenScene(MapPrototypeSetup.ScenePath);
            try
            {
                var map = Object.FindAnyObjectByType<MapPrototype>();
                var roster = Object.FindAnyObjectByType<SoldierRoster>();
                var view = map.GetComponentInChildren<MapTilemapView>();
                map.Regenerate();
                roster.Spawn(map.Data);
                SoldierView soldier = roster.Soldiers[0];
                Vector2Int start = soldier.Cell;
                soldier.RequestDuty(SoldierDuty.Guard);
                soldier.AdvanceLook(1.5f);
                soldier.BeginMove(view, new[] { start, start + Vector2Int.right, start + Vector2Int.right + Vector2Int.up });
                soldier.AdvanceMovement(0.01f);
                soldier.AdvanceLook(1f);
                Assert.Less(Vector2.Distance(Vector2.right, soldier.LookDirection), 0.0001f);
                soldier.AdvanceMovement(100f);
                Assert.IsFalse(soldier.IsMoving);
                Assert.AreEqual(Vector2.up, soldier.FacingDirection);
                soldier.AdvanceLook(1.5f);
                Assert.AreEqual(45f, Vector2.SignedAngle(Vector2.up, soldier.LookDirection), 0.001f);
                soldier.RequestDuty(SoldierDuty.Work);
                soldier.AdvanceLook(0f);
                Assert.AreEqual(Vector2.up, soldier.LookDirection);
                Assert.Throws<System.ArgumentOutOfRangeException>(() => soldier.AdvanceLook(float.NaN));
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        [Test]
        public void RosterInitializationAutomaticallyEnablesSoldierVision()
        {
            EditorSceneManager.OpenScene(MapPrototypeSetup.ScenePath);
            try
            {
                var map = Object.FindAnyObjectByType<MapPrototype>();
                var roster = Object.FindAnyObjectByType<SoldierRoster>();
                map.Regenerate();
                roster.Spawn(map.Data);
                typeof(SoldierRoster).GetMethod("ConfigureFog", System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.NonPublic).Invoke(roster, null);
                var fog = roster.GetComponent<SoldierFogOfWar>();
                Assert.IsNotNull(fog);
                Assert.IsTrue(map.Data.Fog.Enabled);
                foreach (SoldierView soldier in roster.Soldiers)
                    Assert.IsTrue(map.Data.Fog.IsVisible(soldier.Cell));
                Assert.IsFalse(map.Data.Fog.IsExplored(new Vector2Int(120, 120)));
                Assert.IsNotNull(map.GetComponentInChildren<MapTilemapView>().transform.Find("Fog of War"));
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        [Test]
        public void ResetAndDisableAreExplicitAndMapBoundsAreRespected()
        {
            var map = new MapData(3, 3);
            Assert.IsTrue(map.Fog.IsVisible(Vector2Int.zero));
            Assert.IsFalse(map.Fog.IsVisible(new Vector2Int(-1, 0)));
            map.Fog.Enable();
            map.Fog.Update(map, new[] { Vector2Int.zero }, 3);
            Assert.IsTrue(map.Fog.IsExplored(Vector2Int.zero));
            map.Fog.Enable();
            Assert.IsFalse(map.Fog.IsExplored(Vector2Int.zero));
            map.Fog.Disable();
            Assert.IsTrue(map.Fog.IsVisible(Vector2Int.one));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => map.Fog.Update(map, new Vector2Int[0], 0));
        }
    }
}