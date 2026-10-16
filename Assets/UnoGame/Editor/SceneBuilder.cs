using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnoGame.Game;
using UnoGame.Home;
using UnoGame.UI;

namespace UnoGame.EditorTools
{
    /// <summary>
    /// Generates the scenes, the card prefab and the matchmaking animations. Layout code lives next to
    /// each screen (GameController.Build, HomeLayout.Build) so runtime and layout stay in one place;
    /// the generated scenes are normal Unity scenes you can open and tweak.
    /// </summary>
    public static class SceneBuilder
    {
        public const string HomePath = "Assets/UnoGame/Scenes/Home.unity";
        public const string GamePath = "Assets/UnoGame/Scenes/Game.unity";
        const string CardPrefabPath = "Assets/UnoGame/Prefabs/Card.prefab";
        const string AnimDir = "Assets/UnoGame/Animations/";

        [MenuItem("UNO Game/Rebuild Scenes")]
        public static void BuildAll()
        {
            Directory.CreateDirectory("Assets/UnoGame/Scenes");
            Directory.CreateDirectory("Assets/UnoGame/Prefabs");
            Directory.CreateDirectory(AnimDir);
            var card = BuildCardPrefab();
            var anim = BuildMatchmakingAnimator();
            BuildHome(anim);
            BuildGame(card);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(HomePath, true), new EditorBuildSettingsScene(GamePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[Scenes] built Home + Game");
        }

        static CardView BuildCardPrefab()
        {
            var cv = CardView.Build();
            PrefabUtility.SaveAsPrefabAsset(cv.gameObject, CardPrefabPath);
            Object.DestroyImmediate(cv.gameObject);
            return AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath).GetComponent<CardView>();
        }

        static RectTransform NewCanvasScene(Color background)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = background;
            cam.orthographic = true;
            cam.cullingMask = 0;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.layer = 5;
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f;
            canvasGo.AddComponent<AdaptiveScaler>();
            return (RectTransform)canvasGo.transform;
        }

        static void BuildHome(RuntimeAnimatorController anim)
        {
            var root = NewCanvasScene(new Color(0.1f, 0.06f, 0.25f));
            HomeLayout.Build(root, anim);
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), HomePath);
        }

        static void BuildGame(CardView card)
        {
            var root = NewCanvasScene(new Color(0.12f, 0.16f, 0.17f));
            var bg = UIKit.Img(root, "Background", ArtLibrary.I.bgTable, Color.white);
            bg.rectTransform.Fill();
            var fit = bg.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = 1080f / 1920f;
            var safe = UIKit.Node("SafeArea", root).Fill();
            safe.gameObject.AddComponent<SafeArea>();
            var game = UIKit.Node("Game", safe);
            var gc = game.gameObject.AddComponent<GameController>();
            var overlays = UIKit.Node("Overlays", root).Fill();
            gc.Build(card, overlays);
            gc.tableBg = bg;
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), GamePath);
        }

        // ------------------------------------------------------------------ matchmaking animator

        static AnimationCurve Linear(params (float t, float v)[] keys)
        {
            var c = new AnimationCurve();
            foreach (var k in keys) c.AddKey(new Keyframe(k.t, k.v));
            for (int i = 0; i < c.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(c, i, AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(c, i, AnimationUtility.TangentMode.Linear);
            }
            return c;
        }

        static AnimationCurve Smooth(params (float t, float v)[] keys)
        {
            var c = new AnimationCurve();
            foreach (var k in keys) c.AddKey(new Keyframe(k.t, k.v));
            for (int i = 0; i < c.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(c, i, AnimationUtility.TangentMode.ClampedAuto);
                AnimationUtility.SetKeyRightTangentMode(c, i, AnimationUtility.TangentMode.ClampedAuto);
            }
            return c;
        }

        static void Set(AnimationClip clip, string path, System.Type type, string prop, AnimationCurve curve) =>
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, type, prop), curve);

        static void Scale(AnimationClip clip, string path, AnimationCurve curve)
        {
            Set(clip, path, typeof(RectTransform), "m_LocalScale.x", curve);
            Set(clip, path, typeof(RectTransform), "m_LocalScale.y", curve);
        }

        const string Ring1 = "Safe/Center/Ring1", Ring2 = "Safe/Center/Ring2", Orbit = "Safe/Center/Orbit";
        const string Me = "Safe/Center/Me", Opp = "Safe/Center/Opp", Vs = "Safe/Center/VS", Status = "Safe/Status";

        static AnimationClip SearchClip()
        {
            var clip = new AnimationClip { name = "MM_Search", frameRate = 60 };
            Scale(clip, Ring1, Linear((0, 0.55f), (2, 1.65f)));
            Set(clip, Ring1, typeof(Image), "m_Color.a", Linear((0, 0.75f), (2, 0f)));
            Scale(clip, Ring2, Linear((0, 1.1f), (1, 1.65f), (1.001f, 0.55f), (2, 1.1f)));
            Set(clip, Ring2, typeof(Image), "m_Color.a", Linear((0, 0.375f), (1, 0f), (1.001f, 0.75f), (2, 0.375f)));
            Set(clip, Orbit, typeof(RectTransform), "localEulerAnglesRaw.z", Linear((0, 0), (2, -180)));
            Set(clip, Orbit, typeof(CanvasGroup), "m_Alpha", Linear((0, 1), (2, 1)));
            Scale(clip, Me, Smooth((0, 1f), (1, 1.06f), (2, 1f)));
            Set(clip, Me, typeof(RectTransform), "m_AnchoredPosition.x", Linear((0, 0), (2, 0)));
            Set(clip, Opp, typeof(RectTransform), "m_AnchoredPosition.x", Linear((0, 760), (2, 760)));
            Set(clip, Opp, typeof(CanvasGroup), "m_Alpha", Linear((0, 0), (2, 0)));
            Scale(clip, Opp, Linear((0, 1), (2, 1)));
            Scale(clip, Vs, Linear((0, 0), (2, 0)));
            Set(clip, Vs, typeof(CanvasGroup), "m_Alpha", Linear((0, 0), (2, 0)));
            Set(clip, Status, typeof(CanvasGroup), "m_Alpha", Linear((0, 1), (2, 1)));
            var s = AnimationUtility.GetAnimationClipSettings(clip);
            s.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, s);
            return clip;
        }

        static AnimationClip FoundClip()
        {
            var clip = new AnimationClip { name = "MM_Found", frameRate = 60 };
            Scale(clip, Ring1, Linear((0, 1.2f), (0.3f, 2.2f)));
            Set(clip, Ring1, typeof(Image), "m_Color.a", Linear((0, 0.4f), (0.3f, 0f)));
            Scale(clip, Ring2, Linear((0, 1.4f), (0.3f, 2.4f)));
            Set(clip, Ring2, typeof(Image), "m_Color.a", Linear((0, 0.3f), (0.3f, 0f)));
            Set(clip, Orbit, typeof(RectTransform), "localEulerAnglesRaw.z", Smooth((0, 0), (0.4f, -120)));
            Set(clip, Orbit, typeof(CanvasGroup), "m_Alpha", Linear((0, 1), (0.3f, 0)));
            Set(clip, Me, typeof(RectTransform), "m_AnchoredPosition.x", Smooth((0, 0), (0.35f, -275), (0.5f, -250)));
            Scale(clip, Me, Smooth((0, 1f), (0.35f, 0.92f), (0.5f, 0.9f)));
            Set(clip, Opp, typeof(RectTransform), "m_AnchoredPosition.x", Smooth((0, 760), (0.15f, 760), (0.5f, 225), (0.65f, 250)));
            Set(clip, Opp, typeof(CanvasGroup), "m_Alpha", Linear((0, 0), (0.15f, 0), (0.3f, 1)));
            Scale(clip, Opp, Linear((0, 0.9f), (0.65f, 0.9f)));
            Scale(clip, Vs, Smooth((0, 0), (0.45f, 0), (0.46f, 3f), (0.62f, 0.85f), (0.75f, 1.05f), (0.85f, 1f)));
            Set(clip, Vs, typeof(CanvasGroup), "m_Alpha", Linear((0, 0), (0.45f, 0), (0.55f, 1)));
            Set(clip, Status, typeof(CanvasGroup), "m_Alpha", Linear((0, 1), (1.2f, 1)));
            return clip;
        }

        static RuntimeAnimatorController BuildMatchmakingAnimator()
        {
            string ctrlPath = AnimDir + "Matchmaking.controller";
            foreach (var p in new[] { ctrlPath, AnimDir + "MM_Search.anim", AnimDir + "MM_Found.anim" })
                if (File.Exists(p)) AssetDatabase.DeleteAsset(p);
            var search = SearchClip();
            var found = FoundClip();
            AssetDatabase.CreateAsset(search, AnimDir + "MM_Search.anim");
            AssetDatabase.CreateAsset(found, AnimDir + "MM_Found.anim");
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
            ctrl.AddParameter("found", AnimatorControllerParameterType.Trigger);
            var sm = ctrl.layers[0].stateMachine;
            var sSearch = sm.AddState("Search");
            sSearch.motion = search;
            var sFound = sm.AddState("Found");
            sFound.motion = found;
            sm.defaultState = sSearch;
            var t = sSearch.AddTransition(sFound);
            t.hasExitTime = false;
            t.duration = 0f;
            t.AddCondition(AnimatorConditionMode.If, 0, "found");
            AssetDatabase.SaveAssets();
            return ctrl;
        }
    }
}
