using System;
using System.Collections.Generic;
using NoMansGround.Map;
using UnityEngine;

namespace NoMansGround.Soldiers
{
    public sealed class SoldierRoster : MonoBehaviour
    {
        [SerializeField] private MapPrototype map;
        [SerializeField] private MapTilemapView mapView;
        [SerializeField] private Sprite soldierSprite;
        [SerializeField, Range(1, SoldierGenerator.MaximumSoldiers)] private int soldierCount = 8;
        [SerializeField] private int seed = 1917;

        private readonly List<SoldierView> soldiers = new List<SoldierView>();
        private IReadOnlyList<SoldierView> readOnlySoldiers;

        public IReadOnlyList<SoldierView> Soldiers => readOnlySoldiers ?? (readOnlySoldiers = soldiers.AsReadOnly());
        public event Action Rebuilt;

        public void Configure(MapPrototype prototype, MapTilemapView view, Sprite sprite)
        {
            map = prototype;
            mapView = view;
            soldierSprite = sprite;
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;
            if (map == null || mapView == null || soldierSprite == null)
            {
                Debug.LogError("SoldierRoster requires a map, map view and soldier sprite.", this);
                enabled = false;
                return;
            }
            map.Generated += Spawn;
            if (map.Data != null)
                Spawn(map.Data);
        }

        private void OnDisable()
        {
            if (map != null)
                map.Generated -= Spawn;
        }

        public void Spawn(MapData data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (mapView == null || soldierSprite == null)
                throw new InvalidOperationException("Configure the roster before spawning soldiers.");
            int count = Mathf.Min(soldierCount, data.Width * data.Height);
            IReadOnlyList<SoldierData> generated = SoldierGenerator.Generate(count, seed);
            if (count != soldierCount)
                Debug.LogWarning($"Map has room for only {count} of the requested {soldierCount} soldiers.", this);

            foreach (SoldierView soldier in soldiers)
            {
                if (soldier == null)
                    continue;
                soldier.gameObject.SetActive(false);
                if (Application.isPlaying)
                    Destroy(soldier.gameObject);
                else
                    DestroyImmediate(soldier.gameObject);
            }
            soldiers.Clear();

            // Keep arrivals centred horizontally in the lowest available rows.
            int columns = Mathf.Min(data.Width, Mathf.CeilToInt(Mathf.Sqrt(count)));
            int rows = Mathf.CeilToInt((float)count / columns);
            if (rows > data.Height)
            {
                columns = Mathf.CeilToInt((float)count / data.Height);
                rows = Mathf.CeilToInt((float)count / columns);
            }
            int spacingX = Mathf.Min(3, Mathf.Max(1, (data.Width - 1) / Mathf.Max(1, columns - 1)));
            const int spacingY = 1;
            int startX = (data.Width - 1 - (columns - 1) * spacingX) / 2;
            int startY = Mathf.Min(1, data.Height - 1 - (rows - 1) * spacingY);

            for (int i = 0; i < count; i++)
            {
                var cell = new Vector2Int(startX + i % columns * spacingX, startY + i / columns * spacingY);
                var soldierObject = new GameObject(generated[i].FullName, typeof(SpriteRenderer), typeof(SoldierView));
                soldierObject.transform.SetParent(transform, false);
                soldierObject.transform.position = mapView.Tilemap.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));
                soldierObject.transform.localScale = Vector3.one * 0.9f;
                SoldierView soldier = soldierObject.GetComponent<SoldierView>();
                soldier.Initialize(generated[i], cell, soldierSprite);
                soldiers.Add(soldier);
            }
            Rebuilt?.Invoke();
        }
    }
}
