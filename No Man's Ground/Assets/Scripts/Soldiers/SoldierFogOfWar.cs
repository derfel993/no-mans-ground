using System.Collections.Generic;
using NoMansGround.Map;
using UnityEngine;

namespace NoMansGround.Soldiers
{
    [DefaultExecutionOrder(100)]
    public sealed class SoldierFogOfWar : MonoBehaviour
    {
        [SerializeField, Range(1, 64)] private int sightRadius = 12;
        [SerializeField, Range(1, 64)] private int nightSightRadius = 6;
        private MapPrototype map;
        private MapTilemapView view;
        private SoldierRoster roster;
        private float nextRefresh;
        private readonly List<FogObserver> observers = new List<FogObserver>();

        public int SightRadius => sightRadius;
        public int NightSightRadius => nightSightRadius;
        public int CurrentSightRadius
        {
            get
            {
                int dayRadius = Mathf.Clamp(sightRadius, 1, 64);
                DayNightCycle cycle = map != null ? map.GetComponent<DayNightCycle>() : null;
                if (cycle == null)
                    return dayRadius;
                float daylight = cycle.Evaluate(map.Clock.TimeOfDayHours).Daylight;
                return Mathf.RoundToInt(Mathf.Lerp(Mathf.Clamp(nightSightRadius, 1, dayRadius), dayRadius, daylight));
            }
        }

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
                    observers.Add(new FogObserver(soldier.Cell, soldier.LookDirection));
            map.Data.Fog.Update(map.Data, observers, CurrentSightRadius);
            view.RenderFog(map.Data);
        }
    }
}