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
        [SerializeField] private Sprite[] directionalSoldierSprites = new Sprite[8];
        [SerializeField] private Sprite corpseSprite;
        [SerializeField, Range(1, SoldierGenerator.MaximumSoldiers)] private int soldierCount = 8;
        [SerializeField] private int seed = 1917;
        [SerializeField] private bool spawnCook;

        private readonly List<SoldierView> soldiers = new List<SoldierView>();
        private readonly List<GameObject> corpses = new List<GameObject>();
        private IReadOnlyList<SoldierView> readOnlySoldiers;

        public IReadOnlyList<SoldierView> Soldiers => readOnlySoldiers ?? (readOnlySoldiers = soldiers.AsReadOnly());
        public event Action Rebuilt;
        public event Action DutiesChanged;
        public event Action<SoldierView> SoldierDied;
        public int GuardCount
        {
            get
            {
                int count = 0;
                foreach (SoldierView soldier in soldiers)
                    if (soldier != null && soldier.Duty == SoldierDuty.Guard) count++;
                return count;
            }
        }
        public int WorkCount
        {
            get
            {
                int count = 0;
                foreach (SoldierView soldier in soldiers)
                    if (soldier != null && soldier.Duty == SoldierDuty.Work) count++;
                return count;
            }
        }

        private void NotifyDutiesChanged() { DutiesChanged?.Invoke(); }

        public void ConfigureCook(bool includeCook)
        {
            spawnCook = includeCook;
        }

        public void Configure(MapPrototype prototype, MapTilemapView view, Sprite sprite,
            Sprite[] spritesByDirection = null, Sprite deadSprite = null)
        {
            map = prototype;
            mapView = view;
            soldierSprite = sprite;
            if (spritesByDirection != null && spritesByDirection.Length == 8)
                directionalSoldierSprites = spritesByDirection;
            if (deadSprite != null)
                corpseSprite = deadSprite;
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
            ConfigureFog();
            if (map.Data != null)
                Spawn(map.Data);
        }

        private void ConfigureFog()
        {
            SoldierFogOfWar fog = GetComponent<SoldierFogOfWar>();
            if (fog == null) fog = gameObject.AddComponent<SoldierFogOfWar>();
            fog.Configure(map, mapView, this);
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
            if (count != soldierCount)
                Debug.LogWarning($"Map has room for only {count} of the requested {soldierCount} soldiers.", this);
            bool includeCook = spawnCook && count < SoldierGenerator.MaximumSoldiers && count < data.Width * data.Height;
            int engineerCount = count;
            if (includeCook)
                count++;
            IReadOnlyList<SoldierData> generated = SoldierGenerator.Generate(count, seed, includeCook);

            foreach (SoldierView soldier in soldiers)
            {
                if (soldier == null)
                    continue;
                soldier.DutyChanged -= NotifyDutiesChanged;
                soldier.Died -= HandleSoldierDied;
                soldier.gameObject.SetActive(false);
                if (Application.isPlaying)
                    Destroy(soldier.gameObject);
                else
                    DestroyImmediate(soldier.gameObject);
            }
            soldiers.Clear();
            foreach (GameObject corpse in corpses)
            {
                if (corpse == null)
                    continue;
                if (Application.isPlaying)
                    Destroy(corpse);
                else
                    DestroyImmediate(corpse);
            }
            corpses.Clear();

            // Keep arrivals centred horizontally six tiles above the road when possible.
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
            int startY = Mathf.Min(7, data.Height - rows);
            var spawnCells = new HashSet<Vector2Int>();

            for (int i = 0; i < count; i++)
            {
                var desiredCell = new Vector2Int(startX + i % columns * spacingX,
                    startY + i / columns * spacingY);
                Vector2Int cell = FindSpawnCell(data, desiredCell, spawnCells);
                spawnCells.Add(cell);
                var soldierObject = new GameObject(generated[i].FullName, typeof(SpriteRenderer), typeof(SoldierView));
                soldierObject.transform.SetParent(transform, false);
                soldierObject.transform.position = mapView.Tilemap.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));
                soldierObject.transform.localScale = Vector3.one * 0.9f;
                SoldierView soldier = soldierObject.GetComponent<SoldierView>();
                soldier.Initialize(generated[i], cell, soldierSprite, directionalSoldierSprites, corpseSprite);
                soldier.DutyChanged += NotifyDutiesChanged;
                soldier.Died += HandleSoldierDied;
                soldiers.Add(soldier);
            }
            Rebuilt?.Invoke();
            DutiesChanged?.Invoke();
        }

        private void HandleSoldierDied(SoldierView soldier)
        {
            if (!soldiers.Remove(soldier))
                return;
            corpses.Add(soldier.gameObject);
            soldier.DutyChanged -= NotifyDutiesChanged;
            soldier.Died -= HandleSoldierDied;
            if (map != null && map.Data != null && map.Data.Contains(soldier.Cell))
            {
                foreach (ItemType item in Enum.GetValues(typeof(ItemType)))
                {
                    int count = soldier.Data.Inventory.Count(item);
                    if (count == 0 || !soldier.Data.Inventory.TryTake(item, count))
                        continue;
                    for (int index = 0; index < count; index++)
                        if (!TryDropItem(soldier.Cell, item))
                        {
                            map.Data.StorageStockpile.Add(item, count - index);
                            break;
                        }
                }
                if (mapView != null)
                    mapView.RefreshGroundItems(map.Data);
            }
            SoldierDied?.Invoke(soldier);
            DutiesChanged?.Invoke();
        }

        private bool TryDropItem(Vector2Int origin, ItemType item)
        {
            for (int radius = 0; radius < map.Data.Width + map.Data.Height; radius++)
                for (int offsetY = -radius; offsetY <= radius; offsetY++)
                {
                    int offsetX = radius - Mathf.Abs(offsetY);
                    for (int side = 0; side < (offsetX == 0 ? 1 : 2); side++)
                    {
                        var cell = new Vector2Int(origin.x + (side == 0 ? -offsetX : offsetX), origin.y + offsetY);
                        if (!map.Data.IsWalkable(cell) || map.Data.IsMineable(cell) || map.Data.HasTree(cell)
                            || map.Data.IsStorageCell(cell) || map.Data.IsPantryCell(cell)
                            || map.Data.TryGetGroundItem(cell, out _))
                            continue;
                        map.Data.AddGroundItem(cell, item);
                        return true;
                    }
                }
            return false;
        }

        private static Vector2Int FindSpawnCell(MapData data, Vector2Int desired, HashSet<Vector2Int> occupied)
        {
            int maximumRadius = data.Width + data.Height;
            for (int radius = 0; radius <= maximumRadius; radius++)
                for (int offsetY = -radius; offsetY <= radius; offsetY++)
                {
                    int offsetX = radius - Mathf.Abs(offsetY);
                    var left = new Vector2Int(desired.x - offsetX, desired.y + offsetY);
                    if (IsAvailableSpawnCell(data, left, occupied))
                        return left;
                    if (offsetX == 0)
                        continue;
                    var right = new Vector2Int(desired.x + offsetX, desired.y + offsetY);
                    if (IsAvailableSpawnCell(data, right, occupied))
                        return right;
                }
            return desired;
        }

        private static bool IsAvailableSpawnCell(MapData data, Vector2Int cell, HashSet<Vector2Int> occupied)
        {
            return data.IsWalkable(cell) && !data.IsStorageCell(cell) && !data.IsPantryCell(cell)
                && !occupied.Contains(cell);
        }
    }
}
