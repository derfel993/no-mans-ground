using System.Collections.Generic;
using NoMansGround.Map;
using UnityEngine;

namespace NoMansGround.Soldiers
{
    [DefaultExecutionOrder(100)]
    public sealed class SoldierFogOfWar : MonoBehaviour
    {
        [SerializeField, Range(1, 64)] private int sightRadius = 12;
        private MapPrototype map;
        private MapTilemapView view;
        private SoldierRoster roster;
        private float nextRefresh;
        private readonly List<Vector2Int> observers = new List<Vector2Int>();

        public int SightRadius => sightRadius;

        public void Configure(MapPrototype prototype, MapTilemapView mapView, SoldierRoster soldiers)
        {
            Unsubscribe();
            map = prototype;
            view = mapView;
            roster = soldiers;
            Subscribe();
            Refresh();
        }

        private void OnEnable()
        {
            Subscribe();
            Refresh();
        }

        private void Subscribe()
        {
            Unsubscribe();
            if (map != null) map.Generated += HandleGenerated;
            if (roster != null) roster.Rebuilt += Refresh;
        }

        private void Unsubscribe()
        {
            if (map != null) map.Generated -= HandleGenerated;
            if (roster != null) roster.Rebuilt -= Refresh;
        }

        private void OnDisable()
        {
            Unsubscribe();
            if (map != null && map.Data != null)
            {
                map.Data.Fog.Disable();
                if (view != null) view.RenderFog(map.Data);
            }
        }

        private void HandleGenerated(MapData data)
        {
            Refresh();
        }

        private void LateUpdate()
        {
            if (Time.realtimeSinceStartup >= nextRefresh)
            {
                Refresh();
                nextRefresh = Time.realtimeSinceStartup + 0.1f;
            }
        }

        public void Refresh()
        {
            if (!isActiveAndEnabled || map == null || map.Data == null || view == null || roster == null)
                return;
            if (!map.Data.Fog.Enabled)
                map.Data.Fog.Enable();
            observers.Clear();
            foreach (SoldierView soldier in roster.Soldiers)
                if (soldier != null && soldier.isActiveAndEnabled && soldier.Data != null)
                    observers.Add(soldier.Cell);
            map.Data.Fog.Update(map.Data, observers, Mathf.Clamp(sightRadius, 1, 64));
            view.RenderFog(map.Data);
        }
    }
}