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
        public event Action<MapData> Generated;

        public void Configure(MapTilemapView view, MapCameraController controller)
        {
            mapView = view;
            cameraController = controller;
        }

        private void Start()
        {
            Regenerate();
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
            Generated?.Invoke(Data);
        }
    }
}
