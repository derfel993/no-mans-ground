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
        [SerializeField] private bool spawnCook;

        private readonly List<SoldierView> soldiers = new List<SoldierView>();
        private IReadOnlyList<SoldierView> readOnlySoldiers;

        public IReadOnlyList<SoldierView> Soldiers => readOnlySoldiers ?? (readOnlySoldiers = soldiers.AsReadOnly());
        public event Action Rebuilt;
        public event Action DutiesChanged;
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
            if (count != soldierCount)
                Debug.LogWarning($"Map has room for only {count} of the requested {soldierCount} soldiers.", this);
            bool includeCook = spawnCook && count < SoldierGenerator.MaximumSoldiers && count < data.Width * data.Height;
            int engineerCount = count;
            if (includeCook)
                count++;
            IReadOnlyList<SoldierData> generated = SoldierGenerator.Generate(count, seed);

            foreach (SoldierView soldier in soldiers)
            {
                if (soldier == null)
                    continue;
                soldier.DutyChanged -= NotifyDutiesChanged;
                soldier.gameObject.SetActive(false);
                if (Application.isPlaying)
                    Destroy(soldier.gameObject);
                else
                    DestroyImmediate(soldier.gameObject);
            }
            soldiers.Clear();

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
                SoldierData person = generated[i];
                if (includeCook && i == engineerCount)
                {
                    var levels = new int[5];
                    for (int skill = 0; skill < levels.Length; skill++)
                        levels[skill] = person.GetSkill((SoldierSkill)skill).Level;
                    person = new SoldierData(person.Id, person.FirstName, person.LastName,
                        person.Needs, levels, SoldierProfession.Cook);
                }
                soldier.Initialize(person, cell, soldierSprite);
                soldier.DutyChanged += NotifyDutiesChanged;
                soldiers.Add(soldier);
            }
            Rebuilt?.Invoke();
            DutiesChanged?.Invoke();
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
