using System.Reflection;
using NoMansGround.Map;
using NoMansGround.Soldiers;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

namespace NoMansGround.Tests
{
    public sealed class SoldierInputTests : InputTestFixture
    {
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

        private static void UpdateSelection(SoldierDetailsPanel panel)
        {
            typeof(SoldierDetailsPanel).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(panel, null);
        }
    }
}
