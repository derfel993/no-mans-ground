using System.Reflection;
using NoMansGround.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace NoMansGround.Tests
{
    public sealed class SpriteResolutionTests
    {
        [TestCase("Terrain.png")]
        [TestCase("Ladder.png")]
        [TestCase("Soldier.png")]
        [TestCase("Soldiers/ITA/soldier-ita-1-north.png")]
        [TestCase("Soldiers/ITA/soldier-ita-1-north-east.png")]
        [TestCase("Soldiers/ITA/soldier-ita-1-east.png")]
        [TestCase("Soldiers/ITA/soldier-ita-1-south-east.png")]
        [TestCase("Soldiers/ITA/soldier-ita-1-south.png")]
        [TestCase("Soldiers/ITA/soldier-ita-1-south-west.png")]
        [TestCase("Soldiers/ITA/soldier-ita-1-west.png")]
        [TestCase("Soldiers/ITA/soldier-ita-1-north-west.png")]
        public void GameSpritesUseThirtyTwoPixelsAndKeepOneWorldUnit(string relativePath)
        {
            string path = "Assets/Art/Prototype/" + relativePath;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            Assert.IsNotNull(sprite, path);
            Assert.AreEqual(32, sprite.texture.width, path);
            Assert.AreEqual(32, sprite.texture.height, path);
            Assert.AreEqual(32f, sprite.rect.width, path);
            Assert.AreEqual(32f, sprite.rect.height, path);
            Assert.AreEqual(32f, sprite.pixelsPerUnit, path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Assert.AreEqual(FilterMode.Point, importer.filterMode, path);
            Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression, path);
            Assert.IsFalse(importer.mipmapEnabled, path);
        }

        [Test]
        public void PrototypeGeneratorDoublesPixelsWithoutBlurringOrLosingAlpha()
        {
            MethodInfo generator = typeof(MapPrototypeSetup).GetMethod("CreatePrototypeTexture",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(generator);
            var sample = new System.Func<int, int, Color32>((column, row) =>
                new Color32((byte)(column * 10), (byte)(row * 10), 70, (byte)(column == 0 ? 0 : 255)));
            var texture = (Texture2D)generator.Invoke(null, new object[] { sample });
            try
            {
                Assert.AreEqual(32, texture.width);
                Assert.AreEqual(32, texture.height);
                Color32[] pixels = texture.GetPixels32();
                for (int row = 0; row < 32; row++)
                    for (int column = 0; column < 32; column++)
                        Assert.AreEqual(sample(column / 2, row / 2), pixels[row * 32 + column]);
            }
            finally { Object.DestroyImmediate(texture); }
        }
    }
}