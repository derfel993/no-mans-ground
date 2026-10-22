using System;
using System.Collections.Generic;
using UnityEngine;

namespace NoMansGround.Map
{
    public sealed class BuildingRoom
    {
        private readonly HashSet<Vector2Int> completed = new HashSet<Vector2Int>();
        public RectInt Bounds { get; }
        public ItemType Material { get; }
        public bool IsStandaloneWall { get; }
        public bool IsWallAssembly { get; }
        public Vector2Int? Door { get; private set; }
        public bool DoorBuilt { get; private set; }
        public bool IsComplete => IsStandaloneWall ? completed.Count == 1
            : Door.HasValue && DoorBuilt && completed.Count == 2 * Bounds.width + 2 * Bounds.height - 4;
        public int CompletedTiles => completed.Count;

        internal BuildingRoom(RectInt bounds, ItemType material, bool isStandaloneWall = false,
            bool isWallAssembly = false)
        {
            Bounds = bounds;
            Material = material;
            IsStandaloneWall = isStandaloneWall;
            IsWallAssembly = isWallAssembly;
        }

        public bool IsPerimeter(Vector2Int cell)
        {
            return Bounds.Contains(cell) && (cell.x == Bounds.xMin || cell.x == Bounds.xMax - 1
                || cell.y == Bounds.yMin || cell.y == Bounds.yMax - 1);
        }

        public bool IsBuilt(Vector2Int cell) => completed.Contains(cell);

        internal bool SetDoor(Vector2Int cell)
        {
            if (IsStandaloneWall || !IsPerimeter(cell) || Door.HasValue
                || ((cell.x == Bounds.xMin || cell.x == Bounds.xMax - 1)
                    && (cell.y == Bounds.yMin || cell.y == Bounds.yMax - 1)))
                return false;
            Door = cell;
            return true;
        }

        internal bool Complete(Vector2Int cell) => IsPerimeter(cell) && completed.Add(cell);

        internal bool CompleteDoor(Vector2Int cell)
        {
            if (Door != cell || DoorBuilt || !IsPerimeter(cell))
                return false;
            completed.Add(cell);
            DoorBuilt = true;
            return true;
        }

        internal void AbsorbWall(Vector2Int cell, bool isBuilt)
        {
            if (!IsPerimeter(cell))
                throw new ArgumentOutOfRangeException(nameof(cell));
            if (isBuilt)
                completed.Add(cell);
        }
    }

    public sealed class TunnelDoor
    {
        public Vector2Int Cell { get; }
        public bool IsBuilt { get; internal set; }

        internal TunnelDoor(Vector2Int cell)
        {
            Cell = cell;
        }
    }

    public enum MapZoneKind
    {
        Dump,
        MaterialsDepot,
        Cemetery
    }

    public enum RoomSpecialization
    {
        Command,
        TelegraphStation,
        Kitchen,
        Armory,
        Shelter,
        Hospital,
        Warehouse
    }

    public sealed class SpecializedRoom
    {
        public RectInt Bounds { get; }
        public RoomSpecialization Specialization { get; internal set; }
        public BuildingRoom Building { get; }

        internal SpecializedRoom(RectInt bounds, RoomSpecialization specialization, BuildingRoom building)
        {
            Bounds = bounds;
            Specialization = specialization;
            Building = building;
        }
    }

    public enum TerrainKind
    {
        Dirt,
        Grass,
        Mud,
        Trench,
        Road,
        Rock,
        Gravel,
        Mountain,
        Tunnel
    }

    public sealed class MapData
    {
        private readonly TerrainKind[] cells;
        private readonly bool[] blocked;
        private readonly bool[] ladders;
        private readonly bool[] trees;
        private readonly bool[] storageCells;
        private readonly bool[] pantryCells;
        private readonly ItemType?[] groundItems;
        private readonly MapZoneKind?[] zones;
        private readonly List<BuildingRoom> buildings = new List<BuildingRoom>();
        private readonly List<SpecializedRoom> specializedRooms = new List<SpecializedRoom>();
        private readonly List<TunnelDoor> tunnelDoors = new List<TunnelDoor>();
        private readonly List<Vector2Int> targetCells = new List<Vector2Int>();
        private readonly byte[] tunnelShelterStates;
        private bool sheltersDirty = true;
        private int closedTunnelShelterCount;
        private int openTunnelCount;
        public IReadOnlyList<BuildingRoom> Buildings => buildings.AsReadOnly();
        public IReadOnlyList<SpecializedRoom> SpecializedRooms => specializedRooms.AsReadOnly();
        public IReadOnlyList<TunnelDoor> TunnelDoors => tunnelDoors.AsReadOnly();
        public IReadOnlyList<Vector2Int> TargetCells => targetCells.AsReadOnly();
        public int ClosedTunnelShelterCount { get { UpdateTunnelShelters(); return closedTunnelShelterCount; } }
        public int OpenTunnelCount { get { UpdateTunnelShelters(); return openTunnelCount; } }

        public TunnelDoor GetTunnelDoor(Vector2Int cell)
        {
            foreach (TunnelDoor door in tunnelDoors)
                if (door.Cell == cell)
                    return door;
            return null;
        }

        public bool IsTargetCell(Vector2Int cell) => targetCells.Contains(cell);

        public bool ToggleTargetCell(Vector2Int cell)
        {
            int existing = targetCells.IndexOf(cell);
            if (existing >= 0)
            {
                targetCells.RemoveAt(existing);
                return true;
            }
            if (!Contains(cell) || !Fog.IsExplored(cell) || !IsWalkable(cell) || IsTrench(cell)
                || HasTree(cell) || TryGetGroundItem(cell, out _) || GetBuilding(cell) != null)
                return false;
            targetCells.Add(cell);
            return true;
        }

        public bool TryPlanTunnelDoor(Vector2Int cell)
        {
            if (!IsTunnel(cell) || !IsWalkable(cell) || GetTunnelDoor(cell) != null
                || TryGetGroundItem(cell, out _))
                return false;
            bool horizontalWalls = IsMineable(cell + Vector2Int.left) && IsMineable(cell + Vector2Int.right);
            bool verticalWalls = IsMineable(cell + Vector2Int.down) && IsMineable(cell + Vector2Int.up);
            Vector2Int along = horizontalWalls ? Vector2Int.up : Vector2Int.right;
            if ((!horizontalWalls && !verticalWalls) || !IsWalkable(cell + along) || !IsWalkable(cell - along)
                || IsTrench(cell + along) || IsTrench(cell - along)
                || (!IsTunnel(cell + along) && !IsTunnel(cell - along)))
                return false;
            tunnelDoors.Add(new TunnelDoor(cell));
            return true;
        }

        public bool CompleteTunnelDoor(Vector2Int cell)
        {
            TunnelDoor door = GetTunnelDoor(cell);
            if (door == null || door.IsBuilt || !IsTunnel(cell) || !IsWalkable(cell))
                return false;
            door.IsBuilt = true;
            sheltersDirty = true;
            return true;
        }

        public void CancelTunnelDoor(Vector2Int cell)
        {
            TunnelDoor door = GetTunnelDoor(cell);
            if (door != null && !door.IsBuilt)
                tunnelDoors.Remove(door);
        }

        public bool IsClosedShelter(Vector2Int cell)
        {
            if (!IsTunnel(cell))
                return IsIndoors(cell);
            UpdateTunnelShelters();
            return tunnelShelterStates[cell.y * Width + cell.x] == 2;
        }

        private void UpdateTunnelShelters()
        {
            if (!sheltersDirty)
                return;
            Array.Clear(tunnelShelterStates, 0, tunnelShelterStates.Length);
            closedTunnelShelterCount = 0;
            openTunnelCount = 0;
            var pending = new Queue<Vector2Int>();
            var region = new List<int>();
            Vector2Int[] offsets = { Vector2Int.left, Vector2Int.right, Vector2Int.down, Vector2Int.up };
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    var start = new Vector2Int(x, y);
                    if (!IsTunnel(start) || !IsWalkable(start) || GetTunnelDoor(start)?.IsBuilt == true
                        || tunnelShelterStates[y * Width + x] != 0)
                        continue;
                    region.Clear();
                    pending.Enqueue(start);
                    tunnelShelterStates[y * Width + x] = 1;
                    bool closed = true;
                    while (pending.Count > 0)
                    {
                        Vector2Int cell = pending.Dequeue();
                        region.Add(cell.y * Width + cell.x);
                        foreach (Vector2Int offset in offsets)
                        {
                            Vector2Int neighbour = cell + offset;
                            if (!Contains(neighbour))
                            {
                                closed = false;
                                continue;
                            }
                            if (!IsWalkable(neighbour) || GetTunnelDoor(neighbour)?.IsBuilt == true)
                                continue;
                            if (!IsTunnel(neighbour))
                            {
                                closed = false;
                                continue;
                            }
                            int index = neighbour.y * Width + neighbour.x;
                            if (tunnelShelterStates[index] != 0)
                                continue;
                            tunnelShelterStates[index] = 1;
                            pending.Enqueue(neighbour);
                        }
                    }
                    if (closed)
                    {
                        closedTunnelShelterCount++;
                        foreach (int index in region)
                            tunnelShelterStates[index] = 2;
                    }
                    else
                        openTunnelCount++;
                }
            sheltersDirty = false;
        }

        public BuildingRoom GetBuilding(Vector2Int cell)
        {
            foreach (BuildingRoom room in buildings)
                if (room.Bounds.Contains(cell))
                    return room;
            return null;
        }

        public bool IsIndoors(Vector2Int cell)
        {
            BuildingRoom room = GetBuilding(cell);
            return IsTunnel(cell) || (room != null && room.IsComplete && !room.IsPerimeter(cell));
        }

        public bool IsTunnel(Vector2Int cell)
        {
            return Contains(cell) && this[cell.x, cell.y] == TerrainKind.Tunnel;
        }

        public bool IsMineable(Vector2Int cell)
        {
            return Contains(cell) && (this[cell.x, cell.y] == TerrainKind.Rock
                || this[cell.x, cell.y] == TerrainKind.Mountain);
        }

        public bool BlocksSight(Vector2Int cell)
        {
            if (!IsWalkable(cell) || GetTunnelDoor(cell)?.IsBuilt == true)
                return true;
            BuildingRoom room = GetBuilding(cell);
            return room != null && room.IsPerimeter(cell) && room.IsBuilt(cell);
        }

        public bool TryPlanBuilding(Vector2Int from, Vector2Int to, ItemType material, out BuildingRoom room)
        {
            room = null;
            if (material != ItemType.Wood && material != ItemType.BuildingStone)
                return false;
            int minX = Math.Min(from.x, to.x);
            int minY = Math.Min(from.y, to.y);
            int maxX = Math.Max(from.x, to.x);
            int maxY = Math.Max(from.y, to.y);
            if (!Contains(new Vector2Int(minX, minY)) || !Contains(new Vector2Int(maxX, maxY))
                || maxX - minX < 2 || maxY - minY < 2)
                return false;
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (!Fog.IsExplored(cell) || !IsWalkable(cell) || IsTrench(cell) || IsTunnel(cell) || HasTree(cell) || TryGetGroundItem(cell, out _)
                        || IsStorageCell(cell) || IsPantryCell(cell) || TryGetZone(cell, out _)
                        || GetBuilding(cell) != null || this[x, y] == TerrainKind.Road)
                        return false;
                }
            room = new BuildingRoom(new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1), material);
            buildings.Add(room);
            return true;
        }

        public bool TryPlanWall(Vector2Int cell, ItemType material, out BuildingRoom wall)
        {
            wall = null;
            if ((material != ItemType.Wood && material != ItemType.BuildingStone)
                || !Contains(cell) || !Fog.IsExplored(cell) || !IsWalkable(cell) || IsTrench(cell)
                || IsTunnel(cell) || HasTree(cell)
                || IsStorageCell(cell) || IsPantryCell(cell) || TryGetZone(cell, out _)
                || GetBuilding(cell) != null || this[cell.x, cell.y] == TerrainKind.Road)
                return false;
            wall = new BuildingRoom(new RectInt(cell.x, cell.y, 1, 1), material, true);
            buildings.Add(wall);
            return true;
        }

        public bool TrySetBuildingDoor(Vector2Int cell)
        {
            BuildingRoom room = GetBuilding(cell);
            return room != null && !room.IsComplete && (room.Door == cell || room.SetDoor(cell));
        }

        public void CancelBuilding(BuildingRoom room)
        {
            if (room.CompletedTiles == 0)
                buildings.Remove(room);
        }

        public bool CompleteBuildingTile(Vector2Int cell)
        {
            BuildingRoom room = GetBuilding(cell);
            if (room == null || !room.IsPerimeter(cell)
                || (!room.IsStandaloneWall && !room.IsWallAssembly && !room.Door.HasValue))
                return false;
            bool completed = room.Door == cell ? room.CompleteDoor(cell) : room.Complete(cell);
            if (!completed)
                return false;
            sheltersDirty = true;
            return true;
        }

        public bool TryMergeWallRectangle(Vector2Int cell, out BuildingRoom room,
            out IReadOnlyList<BuildingRoom> mergedWalls)
        {
            room = null;
            mergedWalls = Array.Empty<BuildingRoom>();
            BuildingRoom start = GetBuilding(cell);
            if (start == null || !start.IsStandaloneWall)
                return false;

            var walls = new List<BuildingRoom>();
            var wallCells = new HashSet<Vector2Int>();
            var pending = new Queue<Vector2Int>();
            pending.Enqueue(cell);
            wallCells.Add(cell);
            Vector2Int[] neighbours = { Vector2Int.left, Vector2Int.right, Vector2Int.down, Vector2Int.up };
            while (pending.Count > 0)
            {
                Vector2Int current = pending.Dequeue();
                BuildingRoom wall = GetBuilding(current);
                if (wall == null || !wall.IsStandaloneWall || wall.Material != start.Material)
                    return false;
                walls.Add(wall);
                foreach (Vector2Int offset in neighbours)
                {
                    Vector2Int adjacent = current + offset;
                    BuildingRoom next = GetBuilding(adjacent);
                    if (next == null || !next.IsStandaloneWall || next.Material != start.Material
                        || !wallCells.Add(adjacent))
                        continue;
                    pending.Enqueue(adjacent);
                }
            }

            int minX = int.MaxValue;
            int minY = int.MaxValue;
            int maxX = int.MinValue;
            int maxY = int.MinValue;
            foreach (Vector2Int wallCell in wallCells)
            {
                minX = Math.Min(minX, wallCell.x);
                minY = Math.Min(minY, wallCell.y);
                maxX = Math.Max(maxX, wallCell.x);
                maxY = Math.Max(maxY, wallCell.y);
            }
            int width = maxX - minX + 1;
            int height = maxY - minY + 1;
            int perimeterCount = 2 * width + 2 * height - 4;
            if (width < 3 || height < 3 || wallCells.Count != perimeterCount)
                return false;

            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    bool perimeter = x == minX || x == maxX || y == minY || y == maxY;
                    if (perimeter != wallCells.Contains(new Vector2Int(x, y)))
                        return false;
                }

            room = new BuildingRoom(new RectInt(minX, minY, width, height), start.Material,
                isWallAssembly: true);
            foreach (BuildingRoom wall in walls)
            {
                Vector2Int wallCell = wall.Bounds.min;
                room.AbsorbWall(wallCell, wall.IsComplete);
                buildings.Remove(wall);
            }
            buildings.Add(room);
            mergedWalls = walls.AsReadOnly();
            sheltersDirty = true;
            return true;
        }

        public bool TryDesignateSpecializedRoom(RoomSpecialization specialization, Vector2Int from,
            Vector2Int to, out SpecializedRoom designated)
        {
            designated = null;
            if (!Enum.IsDefined(typeof(RoomSpecialization), specialization))
                return false;
            int minX = Math.Min(from.x, to.x);
            int minY = Math.Min(from.y, to.y);
            int maxX = Math.Max(from.x, to.x);
            int maxY = Math.Max(from.y, to.y);
            var bounds = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
            BuildingRoom building = null;
            for (int y = bounds.yMin; y < bounds.yMax; y++)
                for (int x = bounds.xMin; x < bounds.xMax; x++)
                {
                    var cell = new Vector2Int(x, y);
                    BuildingRoom candidate = GetBuilding(cell);
                    if (candidate == null || !candidate.IsComplete || !IsIndoors(cell) || candidate.IsPerimeter(cell)
                        || !Fog.IsExplored(cell) || (building != null && building != candidate))
                        return false;
                    building = candidate;
                }

            foreach (SpecializedRoom existing in specializedRooms)
            {
                if (!existing.Bounds.Overlaps(bounds))
                    continue;
                if (existing.Bounds.Equals(bounds) && existing.Building == building)
                {
                    existing.Specialization = specialization;
                    designated = existing;
                    return true;
                }
                return false;
            }

            designated = new SpecializedRoom(bounds, specialization, building);
            specializedRooms.Add(designated);
            return true;
        }

        public int Width { get; }
        public int Height { get; }
        public FogOfWarData Fog { get; }
        public int TreeCount { get; private set; }
        public int GroundItemCount { get; private set; }
        public bool HasStorageArea { get; private set; }
        public Vector2Int StorageOrigin { get; private set; }
        public int StorageWidth { get; private set; }
        public int StorageHeight { get; private set; }
        public bool HasPantryArea { get; private set; }
        public Vector2Int PantryOrigin { get; private set; }
        public int PantryWidth { get; private set; }
        public int PantryHeight { get; private set; }
        public ItemStockpile StorageStockpile { get; } = new ItemStockpile();
        public ItemStockpile PantryStockpile { get; } = new ItemStockpile();

        public MapData(int width, int height)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), "Map dimensions must be positive.");

            Width = width;
            Height = height;
            Fog = new FogOfWarData(width, height);
            cells = new TerrainKind[checked(width * height)];
            blocked = new bool[cells.Length];
            ladders = new bool[cells.Length];
            trees = new bool[cells.Length];
            storageCells = new bool[cells.Length];
            pantryCells = new bool[cells.Length];
            groundItems = new ItemType?[cells.Length];
            zones = new MapZoneKind?[cells.Length];
            tunnelShelterStates = new byte[cells.Length];
        }

        public TerrainKind this[int x, int y]
        {
            get => cells[IndexOf(x, y)];
            set
            {
                int index = IndexOf(x, y);
                if ((value == TerrainKind.Rock || value == TerrainKind.Mountain) && (storageCells[index] || pantryCells[index]
                    || groundItems[index].HasValue))
                    throw new InvalidOperationException("Storage areas cannot contain rock.");
                cells[index] = value;
                sheltersDirty = true;
                if (value != TerrainKind.Trench)
                    ladders[index] = false;
                if ((value == TerrainKind.Rock || value == TerrainKind.Mountain) && trees[index])
                {
                    trees[index] = false;
                    TreeCount--;
                }
            }
        }

        public bool Contains(Vector2Int cell)
        {
            return cell.x >= 0 && cell.y >= 0 && cell.x < Width && cell.y < Height;
        }

        public bool TryGetZone(Vector2Int cell, out MapZoneKind kind)
        {
            kind = default;
            if (!Contains(cell) || !zones[cell.y * Width + cell.x].HasValue)
                return false;
            kind = zones[cell.y * Width + cell.x].Value;
            return true;
        }

        public bool HasZone(MapZoneKind kind)
        {
            for (int i = 0; i < zones.Length; i++)
                if (zones[i] == kind)
                    return true;
            return false;
        }

        public bool CreateStorageArea(Vector2Int origin, int width, int height)
        {
            if (HasStorageArea || !CreateReservedArea(origin, width, height, storageCells))
                return false;
            HasStorageArea = true;
            StorageOrigin = origin;
            StorageWidth = width;
            StorageHeight = height;
            return true;
        }

        public bool CreatePantryArea(Vector2Int origin, int width, int height)
        {
            if (HasPantryArea || !CreateReservedArea(origin, width, height, pantryCells))
                return false;
            HasPantryArea = true;
            PantryOrigin = origin;
            PantryWidth = width;
            PantryHeight = height;
            return true;
        }

        private bool CreateReservedArea(Vector2Int origin, int width, int height, bool[] area)
        {
            if (width <= 0 || height <= 0 || !Contains(origin)
                || !Contains(origin + new Vector2Int(width - 1, height - 1)))
                return false;
            for (int y = origin.y; y < origin.y + height; y++)
                for (int x = origin.x; x < origin.x + width; x++)
                {
                    int index = y * Width + x;
                    if (zones[index].HasValue || storageCells[index] || pantryCells[index])
                        return false;
                }

            for (int y = origin.y; y < origin.y + height; y++)
                for (int x = origin.x; x < origin.x + width; x++)
                {
                    int index = y * Width + x;
                    if (trees[index])
                    {
                        trees[index] = false;
                        TreeCount--;
                    }
                    if (cells[index] == TerrainKind.Rock || cells[index] == TerrainKind.Mountain)
                        cells[index] = TerrainKind.Dirt;
                    blocked[index] = false;
                    area[index] = true;
                }
            sheltersDirty = true;
            return true;
        }

        public bool IsStorageCell(Vector2Int cell)
        {
            return Contains(cell) && storageCells[cell.y * Width + cell.x];
        }

        public bool IsPantryCell(Vector2Int cell)
        {
            return Contains(cell) && pantryCells[cell.y * Width + cell.x];
        }

        public bool TryFindPantryCell(Vector2Int origin, out Vector2Int cell)
        {
            int bestDistance = int.MaxValue;
            cell = default;
            bool found = false;
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    var candidate = new Vector2Int(x, y);
                    if (!pantryCells[y * Width + x] || !IsWalkable(candidate))
                        continue;
                    int distance = Mathf.Abs(candidate.x - origin.x) + Mathf.Abs(candidate.y - origin.y);
                    if (distance >= bestDistance)
                        continue;
                    bestDistance = distance;
                    cell = candidate;
                    found = true;
                }
            return found;
        }

        public bool TryDesignateZone(MapZoneKind kind, Vector2Int from, Vector2Int to, out int changed)
        {
            changed = 0;
            if (!Enum.IsDefined(typeof(MapZoneKind), kind))
                return false;
            int minX = Math.Min(from.x, to.x);
            int maxX = Math.Max(from.x, to.x);
            int minY = Math.Min(from.y, to.y);
            int maxY = Math.Max(from.y, to.y);
            if (!Contains(new Vector2Int(minX, minY)) || !Contains(new Vector2Int(maxX, maxY)))
                return false;

            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    var cell = new Vector2Int(x, y);
                    int index = y * Width + x;
                    if (!Fog.IsExplored(cell) || !IsWalkable(cell) || IsTrench(cell) || GetBuilding(cell) != null || storageCells[index] || pantryCells[index]
                        || (zones[index].HasValue && zones[index] != kind))
                        return false;
                }

            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    int index = y * Width + x;
                    if (zones[index] == kind)
                        continue;
                    zones[index] = kind;
                    changed++;
                }
            return true;
        }

        // TODO: Bathroom/waste and material-hauling jobs must consume these zone destinations.
        public bool TryFindZoneCell(MapZoneKind kind, Vector2Int origin, out Vector2Int cell)
        {
            int bestDistance = int.MaxValue;
            cell = default;
            bool found = false;
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    var candidate = new Vector2Int(x, y);
                    if (zones[y * Width + x] != kind || !IsWalkable(candidate) || IsTrench(candidate))
                        continue;
                    int distance = Mathf.Abs(candidate.x - origin.x) + Mathf.Abs(candidate.y - origin.y);
                    if (distance >= bestDistance)
                        continue;
                    bestDistance = distance;
                    cell = candidate;
                    found = true;
                }
            return found;
        }

        public bool IsWalkable(Vector2Int cell)
        {
            if (!Contains(cell) || IsMineable(cell)
                || blocked[cell.y * Width + cell.x])
                return false;
            BuildingRoom room = GetBuilding(cell);
            return room == null || !room.IsBuilt(cell) || (room.Door == cell && room.DoorBuilt);
        }

        public void SetWalkable(Vector2Int cell, bool walkable)
        {
            blocked[IndexOf(cell.x, cell.y)] = !walkable;
            sheltersDirty = true;
        }

        public bool IsTrench(Vector2Int cell)
        {
            return Contains(cell) && this[cell.x, cell.y] == TerrainKind.Trench;
        }

        public bool HasTree(Vector2Int cell)
        {
            return Contains(cell) && trees[cell.y * Width + cell.x];
        }

        public bool TryGetGroundItem(Vector2Int cell, out ItemType item)
        {
            item = default;
            if (!Contains(cell) || !groundItems[cell.y * Width + cell.x].HasValue)
                return false;
            item = groundItems[cell.y * Width + cell.x].Value;
            return true;
        }

        public bool TryTakeGroundItem(Vector2Int cell, out ItemType item)
        {
            item = default;
            if (!Contains(cell))
                return false;
            int index = cell.y * Width + cell.x;
            if (!groundItems[index].HasValue)
                return false;
            item = groundItems[index].Value;
            groundItems[index] = null;
            GroundItemCount--;
            return true;
        }

        public void AddGroundItem(Vector2Int cell, ItemType item)
        {
            int index = IndexOf(cell.x, cell.y);
            if (!Enum.IsDefined(typeof(ItemType), item))
                throw new ArgumentOutOfRangeException(nameof(item));
            if (trees[index] || IsMineable(cell) || storageCells[index] || pantryCells[index])
                throw new InvalidOperationException("An item cannot be dropped on a tree, rock or storage area.");
            if (groundItems[index].HasValue)
                throw new InvalidOperationException("This cell already contains a ground item.");
            groundItems[index] = item;
            GroundItemCount++;
        }

        public void SetTree(Vector2Int cell, bool hasTree)
        {
            int index = IndexOf(cell.x, cell.y);
            if (hasTree && (IsMineable(cell) || IsTunnel(cell) || storageCells[index] || pantryCells[index]
                || groundItems[index].HasValue))
                throw new InvalidOperationException("A tree cannot be placed on rock, items or a storage area.");
            if (trees[index] != hasTree)
                TreeCount += hasTree ? 1 : -1;
            trees[index] = hasTree;
        }

        public bool HasLadder(Vector2Int cell)
        {
            return IsTrench(cell) && ladders[cell.y * Width + cell.x];
        }

        public void AddLadder(Vector2Int cell)
        {
            if (!IsTrench(cell) || !IsWalkable(cell))
                throw new InvalidOperationException("A ladder requires a walkable trench cell.");
            if (HasLadder(cell))
                throw new InvalidOperationException("This trench already has a ladder.");
            ladders[IndexOf(cell.x, cell.y)] = true;
        }

        public bool CanStep(Vector2Int from, Vector2Int to)
        {
            int dx = to.x - from.x;
            int dy = to.y - from.y;
            if (!IsWalkable(from) || !IsWalkable(to) || Mathf.Abs(dx) > 1 || Mathf.Abs(dy) > 1
                || (dx == 0 && dy == 0))
                return false;
            bool fromTrench = IsTrench(from);
            bool toTrench = IsTrench(to);
            if (dx == 0 || dy == 0)
                return fromTrench == toTrench || HasLadder(fromTrench ? from : to);
            var sideX = new Vector2Int(from.x + dx, from.y);
            var sideY = new Vector2Int(from.x, from.y + dy);
            return fromTrench == toTrench && IsWalkable(sideX) && IsWalkable(sideY)
                && IsTrench(sideX) == fromTrench && IsTrench(sideY) == fromTrench;
        }

        private int IndexOf(int x, int y)
        {
            if (!Contains(new Vector2Int(x, y)))
                throw new ArgumentOutOfRangeException(nameof(x), $"Cell ({x}, {y}) is outside the map.");

            return y * Width + x;
        }
    }
}
