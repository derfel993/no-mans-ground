using System;
using UnityEngine;

namespace NoMansGround.Soldiers
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SoldierView : MonoBehaviour
    {
        private SpriteRenderer body;
        private SpriteRenderer selection;

        public SoldierData Data { get; private set; }
        public Vector2Int Cell { get; private set; }
        public bool IsSelected { get; private set; }

        public void Initialize(SoldierData data, Vector2Int cell, Sprite sprite)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (sprite == null)
                throw new ArgumentNullException(nameof(sprite));

            Data = data;
            Cell = cell;
            name = data.FullName;
            body = GetComponent<SpriteRenderer>();
            body.sprite = sprite;
            body.color = Color.white;
            body.sortingOrder = 10;
            var highlight = new GameObject("Selection", typeof(SpriteRenderer));
            highlight.transform.SetParent(transform, false);
            highlight.transform.localScale = Vector3.one * 1.3f;
            selection = highlight.GetComponent<SpriteRenderer>();
            selection.sprite = sprite;
            selection.color = new Color(1f, 0.85f, 0.25f);
            selection.sortingOrder = 9;
            SetSelected(false);
        }

        public void SetSelected(bool selected)
        {
            if (selection == null)
                throw new InvalidOperationException("Initialize the soldier view before selecting it.");
            IsSelected = selected;
            selection.enabled = selected;
        }
    }
}
