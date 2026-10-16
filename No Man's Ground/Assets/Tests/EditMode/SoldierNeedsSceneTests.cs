using System.Collections;
using NoMansGround.Editor;
using NoMansGround.Map;
using NoMansGround.Soldiers;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace NoMansGround.Tests
{
    public sealed class SoldierNeedsSceneTests
    {
        [UnityTest]
        public IEnumerator RuntimeDecayPauseSelectionAndRegenerationUpdateNeedBars()
        {
            EditorSceneManager.OpenScene(MapPrototypeSetup.ScenePath);
            yield return new EnterPlayMode();
            yield return null;
            var roster = Object.FindAnyObjectByType<SoldierRoster>();
            var panel = Object.FindAnyObjectByType<SoldierDetailsPanel>();
            var map = Object.FindAnyObjectByType<MapPrototype>();
            var first = roster.Soldiers[0].Data.Needs;
            var other = roster.Soldiers[1].Data.Needs;
            panel.Select(roster.Soldiers[0]);
            AssertBars(first);
            float hunger = first.Hunger;
            float thirst = first.Thirst;
            float physical = first.PhysicalNeeds;
            yield return null;
            Assert.Less(first.Hunger, hunger);
            Assert.Less(first.Thirst, thirst);
            Assert.Less(first.PhysicalNeeds, physical);
            float scale = Time.timeScale;
            try
            {
                Time.timeScale = 0f;
                // Allow the frame in which the scale changed to finish.
                yield return null;
                hunger = first.Hunger;
                yield return null;
                yield return null;
                Assert.AreEqual(hunger, first.Hunger);
                first.AdvanceTime(first.HungerDepletionSeconds / 2f);
                AssertBars(first);
                panel.Select(roster.Soldiers[1]);
                AssertBars(other);
                first.AdvanceTime(1380f);
                AssertBars(other);
                panel.Select(roster.Soldiers[0]);
                AssertBars(first);
                Assert.Zero(first.Hunger);
                Assert.Zero(first.Thirst);
                Assert.Zero(first.PhysicalNeeds);
                Assert.IsTrue(roster.Soldiers[0].IsAvailable, "Empty reserves do not prevent work yet.");
                float duration = first.HungerDepletionSeconds;
                map.Regenerate();
                Assert.AreEqual(duration, roster.Soldiers[0].Data.Needs.HungerDepletionSeconds);
                Assert.AreEqual(100f, roster.Soldiers[0].Data.Needs.Hunger);
                Assert.AreEqual(100f, roster.Soldiers[0].Data.Needs.Thirst);
                Assert.AreEqual(100f, roster.Soldiers[0].Data.Needs.PhysicalNeeds);
                AssertBars(roster.Soldiers[0].Data.Needs);
            }
            finally
            {
                Time.timeScale = scale;
            }
            yield return new ExitPlayMode();
        }

        private static void AssertBars(SoldierNeeds state)
        {
            var canvas = GameObject.Find("Soldier Interface").GetComponent<Canvas>();
            string[] names = { "Fame", "Sete", "Bisogni fisiologici" };
            float[] values = { state.Hunger, state.Thirst, state.PhysicalNeeds };
            for (int i = 0; i < names.Length; i++)
            {
                Text label = null;
                foreach (Text text in canvas.GetComponentsInChildren<Text>())
                    if (text.text.StartsWith(names[i] + ": "))
                        label = text;
                Assert.IsNotNull(label);
                Assert.AreEqual($"{names[i]}: {values[i]:0}/100", label.text);
                var fill = label.transform.parent.Find("Track/Fill").GetComponent<Image>();
                Assert.AreEqual(values[i] / 100f, fill.rectTransform.anchorMax.x, 0.00001f);
                Color expected = Color.Lerp(new Color(0.35f, 0.70f, 0.43f),
                    new Color(0.85f, 0.32f, 0.24f), 1f - values[i] / 100f);
                Assert.AreEqual(expected.r, fill.color.r, 0.00001f);
                Assert.AreEqual(expected.g, fill.color.g, 0.00001f);
                Assert.AreEqual(expected.b, fill.color.b, 0.00001f);
            }
        }
    }
}
