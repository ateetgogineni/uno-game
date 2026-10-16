using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace UnoGame.EditorTools
{
    /// <summary>
    /// Imports the atlases written by Tools/art/generate.js: slices sprites from the JSON maps
    /// (keeping sprite ids stable so references survive re-imports), sets 9-slice borders and mobile
    /// compression, and fills Resources/ArtLibrary.asset.
    /// </summary>
    public static class ArtImporter
    {
        const string Dir = "Assets/UnoGame/Art/Generated/";
        const string LibraryPath = "Assets/UnoGame/Resources/ArtLibrary.asset";

        [Serializable] class SpriteDef { public string name; public int x, y, w, h; public int[] border; }
        [Serializable] class AtlasMap { public string texture; public int width, height; public SpriteDef[] sprites; }

        [MenuItem("UNO Game/Import Art")]
        public static void ImportAll()
        {
            AssetDatabase.Refresh();
            ImportAtlas("cards_a", TextureImporterFormat.ASTC_6x6);
            ImportAtlas("cards_b", TextureImporterFormat.ASTC_6x6);
            ImportAtlas("ui", TextureImporterFormat.ASTC_5x5);
            ImportSingle("logo", TextureImporterFormat.ASTC_5x5, 1024);
            ImportSingle("bg_table", TextureImporterFormat.ASTC_8x8, 2048);
            ImportSingle("bg_table_land", TextureImporterFormat.ASTC_8x8, 2048);
            ImportSingle("bg_home", TextureImporterFormat.ASTC_8x8, 2048);
            BuildLibrary();
        }

        static void Common(TextureImporter ti, TextureImporterFormat android, int maxSize)
        {
            ti.textureType = TextureImporterType.Sprite;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.filterMode = FilterMode.Bilinear;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.maxTextureSize = maxSize;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.spritePixelsPerUnit = 100;
            var a = ti.GetPlatformTextureSettings("Android");
            a.overridden = true;
            a.maxTextureSize = maxSize;
            a.format = android;
            a.compressionQuality = 100;
            ti.SetPlatformTextureSettings(a);
        }

        static void ImportAtlas(string name, TextureImporterFormat android)
        {
            string png = Dir + name + ".png";
            var map = JsonUtility.FromJson<AtlasMap>(File.ReadAllText(Dir + name + ".json"));
            AssetDatabase.ImportAsset(png, ImportAssetOptions.ForceSynchronousImport);
            var ti = (TextureImporter)AssetImporter.GetAtPath(png);
            Common(ti, android, 4096);
            ti.spriteImportMode = SpriteImportMode.Multiple;
            ti.SaveAndReimport();

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var dp = factory.GetSpriteEditorDataProviderFromObject(ti);
            dp.InitSpriteEditorDataProvider();
            var existing = dp.GetSpriteRects().GroupBy(r => r.name).ToDictionary(g => g.Key, g => g.First());
            var rects = new List<SpriteRect>();
            foreach (var s in map.sprites)
            {
                if (!existing.TryGetValue(s.name, out var r)) r = new SpriteRect { spriteID = GUID.Generate() };
                r.name = s.name;
                r.rect = new Rect(s.x, map.height - s.y - s.h, s.w, s.h);
                r.alignment = SpriteAlignment.Center;
                r.pivot = new Vector2(0.5f, 0.5f);
                var b = s.border ?? new int[4];
                r.border = new Vector4(b[0], b[1], b[2], b[3]); // left, bottom, right, top
                rects.Add(r);
            }
            dp.SetSpriteRects(rects.ToArray());
            var names = dp.GetDataProvider<ISpriteNameFileIdDataProvider>();
            names?.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)).ToList());
            dp.Apply();
            ti.SaveAndReimport();
            Debug.Log($"[Art] {name}: {rects.Count} sprites");
        }

        static void ImportSingle(string name, TextureImporterFormat android, int maxSize)
        {
            string png = Dir + name + ".png";
            AssetDatabase.ImportAsset(png, ImportAssetOptions.ForceSynchronousImport);
            var ti = (TextureImporter)AssetImporter.GetAtPath(png);
            Common(ti, android, maxSize);
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.SaveAndReimport();
        }

        static Dictionary<string, Sprite> Sprites(string name) =>
            AssetDatabase.LoadAllAssetsAtPath(Dir + name + ".png").OfType<Sprite>().ToDictionary(s => s.name, s => s);

        static void BuildLibrary()
        {
            var lib = AssetDatabase.LoadAssetAtPath<ArtLibrary>(LibraryPath);
            if (lib == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LibraryPath));
                lib = ScriptableObject.CreateInstance<ArtLibrary>();
                AssetDatabase.CreateAsset(lib, LibraryPath);
            }
            var cards = Sprites("cards_a").Concat(Sprites("cards_b")).ToDictionary(p => p.Key, p => p.Value);
            var ui = Sprites("ui");
            string[] colors = { "red", "yellow", "green", "blue" };
            string[] dark = { "pink", "teal", "orange", "purple" };
            lib.cards = cards.Where(p => p.Key.StartsWith("c_") || p.Key.StartsWith("w_") || p.Key.StartsWith("wd_"))
                .Select(p => p.Value).OrderBy(s => s.name).ToList();
            lib.cardBack = cards["card_back"];
            lib.cardMask = cards["card_mask"];
            lib.avatars = Enumerable.Range(0, 12).Select(i => ui["avatar_" + i]).ToArray();
            lib.emotes = Enumerable.Range(0, 6).Select(i => ui["emote_" + i]).ToArray();
            lib.balls = colors.Select(c => ui["ball_" + c]).ToArray();
            lib.darkBalls = dark.Select(c => ui["ball_" + c]).ToArray();
            lib.ui = ui.Values.OrderBy(s => s.name).ToList();
            lib.logo = AssetDatabase.LoadAssetAtPath<Sprite>(Dir + "logo.png");
            lib.bgTable = AssetDatabase.LoadAssetAtPath<Sprite>(Dir + "bg_table.png");
            lib.bgTableLand = AssetDatabase.LoadAssetAtPath<Sprite>(Dir + "bg_table_land.png");
            lib.bgHome = AssetDatabase.LoadAssetAtPath<Sprite>(Dir + "bg_home.png");
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Art] library: {lib.cards.Count} card faces, {lib.ui.Count} ui sprites");
        }
    }
}
