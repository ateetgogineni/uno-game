using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace UnoGame.EditorTools
{
    /// <summary>
    /// Project setup and builds. Every step is a menu item and a batch-mode entry point:
    ///   Unity -batchmode -quit -projectPath . -executeMethod UnoGame.EditorTools.Pipeline.Setup
    ///   ... Pipeline.Prepare        (font, art import, scenes)
    ///   ... Pipeline.BuildWindows   (portrait test build: Builds/Windows/UNO Game.exe)
    ///   ... Pipeline.BuildAndroid   (Builds/Android/UNO Game.apk + .aab, signed with Keystore/)
    /// </summary>
    public static class Pipeline
    {
        public const string Product = "UNO Game";
        public const string PackageId = "com.unogame.game";
        public const string Version = "1.0.0";
        public const int VersionCode = 1;

        const string FontTtf = "Assets/UnoGame/Fonts/Inter-SemiBold.ttf";
        const string FontAsset = "Assets/UnoGame/Resources/Fonts/Inter SDF.asset";
        const string OutlineMat = "Assets/UnoGame/Resources/Fonts/Inter SDF Outline.mat";
        const string ConfigAsset = "Assets/UnoGame/Resources/GameConfig.asset";

        [MenuItem("UNO Game/1. Setup (settings + TextMeshPro)")]
        public static void Setup()
        {
            ApplyPlayerSettings();
            ImportTmpEssentials();
            EnsureConfig();
            AssetDatabase.SaveAssets();
            Debug.Log("[Pipeline] setup done");
        }

        [MenuItem("UNO Game/2. Prepare (font + art + scenes)")]
        public static void Prepare()
        {
            EnsureFont();
            ArtImporter.ImportAll();
            ApplyIcons();
            SceneBuilder.BuildAll();
            AssetDatabase.SaveAssets();
            Debug.Log("[Pipeline] prepare done");
        }

        public static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = Product;
            PlayerSettings.productName = Product;
            PlayerSettings.bundleVersion = Version;
            PlayerSettings.Android.bundleVersionCode = VersionCode;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, PackageId);

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.colorSpace = ColorSpace.Gamma; // flat UI art, matches the source colours exactly
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;

            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Low);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)36;
            PlayerSettings.Android.forceInternetPermission = true;
            PlayerSettings.Android.renderOutsideSafeArea = true;
            PlayerSettings.Android.optimizedFramePacing = true;
            PlayerSettings.Android.preferredInstallLocation = AndroidPreferredInstallLocation.Auto;

            PlayerSettings.runInBackground = true;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 450;
            PlayerSettings.defaultScreenHeight = 975;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.visibleInBackground = true;
            PlayerSettings.usePlayerLog = true;
            QualitySettings.antiAliasing = 0;
            QualitySettings.vSyncCount = 0;
        }

        static void ImportTmpEssentials()
        {
            if (Directory.Exists("Assets/TextMesh Pro/Shaders")) return;
            var pkg = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.unity.ugui");
            string path = Path.Combine(pkg.resolvedPath, "Package Resources", "TMP Essential Resources.unitypackage");
            Debug.Log("[Pipeline] importing " + path);
            AssetDatabase.ImportPackage(path, false);
            AssetDatabase.Refresh();
        }

        static void EnsureConfig()
        {
            if (AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigAsset) != null) return;
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigAsset));
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<GameConfig>(), ConfigAsset);
        }

        public static void EnsureFont()
        {
            var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAsset);
            if (fa == null)
            {
                var font = AssetDatabase.LoadAssetAtPath<Font>(FontTtf);
                fa = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
                fa.name = "Inter SDF";
                Directory.CreateDirectory(Path.GetDirectoryName(FontAsset));
                AssetDatabase.CreateAsset(fa, FontAsset);
                fa.atlasTextures[0].name = "Inter SDF Atlas";
                AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
                fa.material.name = "Inter SDF Material";
                AssetDatabase.AddObjectToAsset(fa.material, fa);
                fa.TryAddCharacters(" !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~•…×");
                EditorUtility.SetDirty(fa);
                AssetDatabase.SaveAssets();
                Debug.Log("[Pipeline] created " + FontAsset);
            }

            var mat = AssetDatabase.LoadAssetAtPath<Material>(OutlineMat);
            if (mat == null)
            {
                mat = new Material(fa.material) { name = "Inter SDF Outline" };
                AssetDatabase.CreateAsset(mat, OutlineMat);
            }
            mat.shader = fa.material.shader;
            mat.SetTexture(ShaderUtilities.ID_MainTex, fa.atlasTextures[0]);
            mat.EnableKeyword(ShaderUtilities.Keyword_Outline);
            mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.2f);
            mat.SetColor(ShaderUtilities.ID_OutlineColor, new Color32(27, 24, 48, 255));
            mat.SetFloat(ShaderUtilities.ID_FaceDilate, 0.1f);
            mat.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            mat.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.45f));
            mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.3f);
            mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.9f);
            mat.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.2f);
            mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.25f);
            EditorUtility.SetDirty(mat);

            // Make Inter the TextMeshPro default.
            var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>("Assets/TextMesh Pro/Resources/TMP Settings.asset");
            if (settings != null)
            {
                var so = new SerializedObject(settings);
                var prop = so.FindProperty("m_defaultFontAsset");
                if (prop != null) { prop.objectReferenceValue = fa; so.ApplyModifiedPropertiesWithoutUndo(); }
            }
            AssetDatabase.SaveAssets();
        }

        static void ApplyIcons()
        {
            const string dir = "Assets/UnoGame/Art/Icons/";
            foreach (var n in new[] { "icon_full", "icon_bg", "icon_fg" })
            {
                var ti = (TextureImporter)AssetImporter.GetAtPath(dir + n + ".png");
                if (ti == null) continue;
                ti.textureType = TextureImporterType.Default;
                ti.mipmapEnabled = false;
                ti.alphaIsTransparency = true;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.npotScale = TextureImporterNPOTScale.None;
                ti.SaveAndReimport();
            }
            var full = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "icon_full.png");
            var bg = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "icon_bg.png");
            var fg = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "icon_fg.png");
            if (full == null) return;
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { full }, IconKind.Any);

            var android = NamedBuildTarget.Android;
            foreach (var kind in PlayerSettings.GetSupportedIconKinds(android))
            {
                var icons = PlayerSettings.GetPlatformIcons(android, kind);
                foreach (var icon in icons)
                {
                    if (icon.maxLayerCount >= 2) icon.SetTextures(bg, fg);
                    else icon.SetTexture(full);
                }
                PlayerSettings.SetPlatformIcons(android, kind, icons);
            }
        }

        // ------------------------------------------------------------------ builds

        static string[] Scenes => new[] { SceneBuilder.HomePath, SceneBuilder.GamePath };

        [MenuItem("UNO Game/Build Windows (test)")]
        public static void BuildWindows()
        {
            ApplyPlayerSettings();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = "Builds/Windows/UNO Game.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });
            Finish(report, "Windows");
        }

        [MenuItem("UNO Game/Build Android (APK + AAB)")]
        public static void BuildAndroid()
        {
            ApplyPlayerSettings();
            ApplySigning();
            Directory.CreateDirectory("Builds/Android");

            EditorUserBuildSettings.buildAppBundle = false;
            var apk = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = $"Builds/Android/{Product}-{Version}.apk",
                target = BuildTarget.Android,
                options = BuildOptions.None,
            });
            Finish(apk, "Android APK");
            if (apk.summary.result != BuildResult.Succeeded) return;

            EditorUserBuildSettings.buildAppBundle = true;
            var aab = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = $"Builds/Android/{Product}-{Version}.aab",
                target = BuildTarget.Android,
                options = BuildOptions.None,
            });
            EditorUserBuildSettings.buildAppBundle = false;
            Finish(aab, "Android AAB");
        }

        /// <summary>
        /// Release signing with the upload key in Keystore/ (created once by Tools/make-keystore.ps1;
        /// passwords are read from Keystore/keystore.properties, never stored in the project).
        /// </summary>
        static void ApplySigning()
        {
            const string props = "Keystore/keystore.properties";
            if (!File.Exists(props))
            {
                Debug.LogWarning("[Pipeline] no Keystore/keystore.properties: building with the debug key");
                PlayerSettings.Android.useCustomKeystore = false;
                return;
            }
            string store = null, storePass = null, alias = null, keyPass = null;
            foreach (var line in File.ReadAllLines(props))
            {
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string k = line.Substring(0, eq).Trim(), v = line.Substring(eq + 1).Trim();
                if (k == "storeFile") store = v;
                else if (k == "storePassword") storePass = v;
                else if (k == "keyAlias") alias = v;
                else if (k == "keyPassword") keyPass = v;
            }
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = Path.GetFullPath(Path.Combine("Keystore", store));
            PlayerSettings.Android.keystorePass = storePass;
            PlayerSettings.Android.keyaliasName = alias;
            PlayerSettings.Android.keyaliasPass = keyPass;
            Debug.Log("[Pipeline] signing with " + PlayerSettings.Android.keystoreName + " alias " + alias);
        }

        static void Finish(BuildReport report, string label)
        {
            var s = report.summary;
            Debug.Log($"[Pipeline] {label} build {s.result}: {s.outputPath} ({s.totalSize / (1024f * 1024f):0.0} MB, {s.totalTime.TotalSeconds:0}s, {s.totalErrors} errors)");
            if (s.result != BuildResult.Succeeded && Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}
