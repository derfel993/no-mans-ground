using System;
using UnityEngine;

namespace NoMansGround.Map
{
    public sealed class MapPrototype : MonoBehaviour
    {
        [SerializeField] private MapGenerationSettings generation = new MapGenerationSettings();
        [SerializeField] private MapTilemapView mapView;
        [SerializeField] private MapCameraController cameraController;

        public MapData Data { get; private set; }
        public MapGenerationSettings Generation => generation;
        public GameClock Clock { get; } = new GameClock();
        public int SimulationSpeed { get; private set; } = 1;
        public event Action<MapData> Generated;
        public event Action<int> SimulationSpeedChanged;

        public void Configure(MapTilemapView view, MapCameraController controller)
        {
            mapView = view;
            cameraController = controller;
        }

        public void SetSimulationSpeed(int multiplier)
        {
            if (multiplier != 1 && multiplier != 2 && multiplier != 5)
                throw new ArgumentOutOfRangeException(nameof(multiplier));
            SimulationSpeed = multiplier;
            Time.timeScale = multiplier;
            SimulationSpeedChanged?.Invoke(multiplier);
        }

        private void Awake()
        {
            SetSimulationSpeed(1);
        }

        private void OnDisable()
        {
            Time.timeScale = 1f;
            SimulationSpeed = 1;
        }

        private void Start()
        {
            Regenerate();
        }

        private void Update()
        {
            Clock.AdvanceTime(Time.deltaTime);
        }

        [ContextMenu("Regenerate Map")]
        public void Regenerate()
        {
            if (mapView == null || cameraController == null)
            {
                Debug.LogError("MapPrototype requires a map view and a camera controller.", this);
                return;
            }

            Data = MapGenerator.Generate(generation);
            mapView.Render(Data);
            cameraController.SetMapBounds(mapView.WorldBounds, true);
            Clock.Reset();
            Generated?.Invoke(Data);
        }
    }
}
