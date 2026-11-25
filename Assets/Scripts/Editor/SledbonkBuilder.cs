#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Re-runnable: overwrites the generated prefabs/materials in Assets/Generated and the scene.
public static class SledbonkBuilder
{
    const string Gen = "Assets/Generated";
    const string SledDir = "Assets/Imported/Sled/GameObject/";
    const string BaseDir = "Assets/Imported/Base/";
    public static string characterName = "Fox";
    static Shader toon;

    [MenuItem("Sledbonk/Montar cena")]
    public static void Build()
    {
        toon = Shader.Find("Sledbonk/Toon");
        if (!toon) { Debug.LogError("Shader Sledbonk/Toon not found"); return; }
        Directory.CreateDirectory(Gen + "/Materials"); Directory.CreateDirectory(Gen + "/Prefabs"); Directory.CreateDirectory(Gen + "/Prefabs/World");
        SetupLayers();
        converted.Clear();
        PlayerSettings.productName = "Fullsend";
        IncludeRuntimeShaders();

        var terrainMat = Mat("Terrain", Color.white, m => { m.SetFloat("_UseVertexColor", 1f); m.SetColor("_ShadeColor", new Color(0.55f, 0.62f, 0.9f)); m.SetColor("_RimColor", new Color(1, 1, 1, 0.12f)); m.SetFloat("_Bands", 0.12f); });
        var farMat = Mat("Far", Color.white, m => { m.SetFloat("_UseVertexColor", 1f); m.SetColor("_ShadeColor", new Color(0.6f, 0.66f, 0.9f)); m.SetColor("_RimColor", new Color(1, 1, 1, 0f)); m.SetFloat("_Bands", 0.3f); });
        var railMat = Mat("Rail", Color.white, m => { m.SetFloat("_PaletteSlot", 4f); m.SetFloat("_TexStrength", 0f); m.SetColor("_Emission", new Color(0.1f, 0.1f, 0.1f)); });
        var platMat = Mat("Platform", new Color(0.75f, 0.45f, 0.25f), null);
        var sparkMat = Mat("Spark", new Color(1f, 0.85f, 0.1f), m => m.SetColor("_Emission", new Color(0.6f, 0.45f, 0f)));
        var ringMat = Mat("Ring", Color.white, m => { m.SetFloat("_PaletteSlot", 4f); m.SetFloat("_TexStrength", 0f); m.SetColor("_Emission", new Color(0.35f, 0.35f, 0.35f)); });
        var finishMat = Mat("Finish", new Color(1f, 0.9f, 0.1f), m => m.SetColor("_Emission", new Color(0.8f, 0.7f, 0f)));
        var chaseMat = Mat("Chase", new Color(0.06f, 0.02f, 0.1f), m => { m.SetColor("_Emission", new Color(0.12f, 0.02f, 0.18f)); m.SetColor("_RimColor", new Color(1f, 0.2f, 0.5f, 1f)); m.SetFloat("_Cull", 0f); });
        var markerMat = Mat("LandingMarker", Color.white, m => { m.SetFloat("_Cull", 0f); m.SetColor("_Emission", Color.white); });
        var comicMat = Mat2("Comic", Shader.Find("Sledbonk/Comic"));
        SetupComicPass(comicMat);

        var character = MakeCharacter();
        var sled = Clean(SledDir + "Toboggan Variant.prefab", "Sled", stripParticles: true);
        var pines = new List<GameObject>();
        foreach (var n in new[] { "Evergreen_Med", "Evergreen_TallTall", "Evergreen_Thick" })
        {
            var tp = Clean(SledDir + n + ".prefab", n, stripParticles: true);
            if (!tp) continue;
            foreach (var r in tp.GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.sharedMaterials) if (m && m.shader == toon) { m.SetFloat("_Cutoff", 0.35f); m.SetFloat("_Cull", 0f); m.SetFloat("_PaletteSlot", 2f); EditorUtility.SetDirty(m); }
            pines.Add(tp);
        }

        var neve = new BiomeDef
        {
            name = "Neve",
            skyTop = new Color(0.3f, 0.5f, 0.92f), skyHorizon = new Color(0.82f, 0.88f, 1f),
            fog = new Color(0.78f, 0.86f, 1f), fogDensity = 0.0008f, sun = new Color(1f, 0.95f, 0.88f), sunIntensity = 1.35f, sunEuler = new Vector3(35f, 40f, 0f),
            ambTop = new Color(0.8f, 0.88f, 1f), ambMid = new Color(0.68f, 0.76f, 0.92f), ambBot = new Color(0.5f, 0.55f, 0.72f),
            road = new Color(1f, 1f, 1f), flat = new Color(0.9f, 0.94f, 1f), slope = new Color(0.55f, 0.72f, 1f), cliff = new Color(0.25f, 0.27f, 0.58f), far = new Color(0.5f, 0.6f, 0.95f),
            palette = new[] { new Color(0.92f, 0.95f, 1f), new Color(0.42f, 0.48f, 0.85f), new Color(0.05f, 0.62f, 0.55f), new Color(0.62f, 0.3f, 0.28f), new Color(1f, 0.22f, 0.45f) },
            toneMid = new Color(0.8f, 0.82f, 1f), toneLow = new Color(0.52f, 0.5f, 0.85f), ink = new Color(0.06f, 0.05f, 0.2f), ui = new Color(1f, 0.5f, 0.15f), ui2 = new Color(0.3f, 0.85f, 1f), uiInk = new Color(0.07f, 0.1f, 0.3f),
            small = Props("Mo_Snow_Rock_"), medium = Concat(pines.ToArray(), Props("Mo_SnowWorld_Tree_")),
            landmarks = Props("Mo_SnowWorld_Ruin_"),
            music = Clips("SHARD6_ICE_A V1", "SHARD6_ICE_B V1", "SHARD6_ICE_C V1"), ambience = Clip("Au_Ambience_Snow")
        };
        var floresta = new BiomeDef
        {
            name = "Floresta",
            skyTop = new Color(0.28f, 0.55f, 0.95f), skyHorizon = new Color(0.76f, 0.9f, 0.93f),
            fog = new Color(0.72f, 0.86f, 0.9f), fogDensity = 0.0009f, sun = new Color(1f, 0.93f, 0.78f), sunIntensity = 1.4f, sunEuler = new Vector3(42f, -20f, 0f),
            ambTop = new Color(0.75f, 0.88f, 1f), ambMid = new Color(0.6f, 0.72f, 0.6f), ambBot = new Color(0.38f, 0.42f, 0.32f),
            road = new Color(1f, 0.86f, 0.4f), flat = new Color(0.52f, 0.86f, 0.28f), slope = new Color(0.25f, 0.62f, 0.32f), cliff = new Color(0.45f, 0.28f, 0.22f), far = new Color(0.3f, 0.6f, 0.5f),
            palette = new[] { new Color(0.55f, 0.86f, 0.3f), new Color(0.7f, 0.68f, 0.62f), new Color(0.1f, 0.62f, 0.3f), new Color(0.55f, 0.3f, 0.18f), new Color(1f, 0.4f, 0.7f) },
            toneMid = new Color(0.78f, 0.86f, 0.82f), toneLow = new Color(0.45f, 0.5f, 0.62f), ink = new Color(0.07f, 0.1f, 0.05f), ui = new Color(1f, 0.86f, 0.1f), ui2 = new Color(0.95f, 0.22f, 0.22f), uiInk = new Color(0.03f, 0.17f, 0.09f),
            small = Props("Mo_Forest_Foliage_"), medium = Props("Mo_Forest_Tree_", "Mo_Fores_Rock_"), landmarks = Props("SM_Forest_Ruins_", "Mo_Forest_White_Tree_"),
            music = Clips("SHARD1_FOR_A V2", "SHARD1_FOR_B V1", "SHARD1_FOR_C V1"), ambience = Clip("Au_Ambience_Forest1")
        };
        var deserto = new BiomeDef
        {
            name = "Deserto",
            skyTop = new Color(0.32f, 0.48f, 0.85f), skyHorizon = new Color(1f, 0.82f, 0.62f),
            fog = new Color(1f, 0.8f, 0.62f), fogDensity = 0.0008f, sun = new Color(1f, 0.78f, 0.55f), sunIntensity = 1.45f, sunEuler = new Vector3(22f, -35f, 0f),
            ambTop = new Color(1f, 0.85f, 0.7f), ambMid = new Color(0.85f, 0.65f, 0.5f), ambBot = new Color(0.55f, 0.38f, 0.3f),
            road = new Color(1f, 0.92f, 0.62f), flat = new Color(1f, 0.76f, 0.35f), slope = new Color(1f, 0.5f, 0.25f), cliff = new Color(0.8f, 0.22f, 0.25f), far = new Color(0.95f, 0.55f, 0.45f),
            palette = new[] { new Color(1f, 0.78f, 0.38f), new Color(0.95f, 0.42f, 0.28f), new Color(0.2f, 0.72f, 0.42f), new Color(0.5f, 0.25f, 0.2f), new Color(0.1f, 0.85f, 0.95f) },
            toneMid = new Color(0.9f, 0.74f, 0.78f), toneLow = new Color(0.62f, 0.4f, 0.6f), ink = new Color(0.18f, 0.04f, 0.08f), ui = new Color(0.15f, 0.92f, 0.95f), ui2 = new Color(1f, 0.82f, 0.15f), uiInk = new Color(0.27f, 0.04f, 0.08f),
            small = Props("Mo_Desert_Cactus_", "Mo_Desert_Rainy_Floor_Rock_"), medium = Props("Mo_Desert_Rainy_Canyon_Rock_"),
            landmarks = Props("Mo_Desert_Rocky_Arch_", "Mo_Desert_Rainy_World_Huge_Tree_"),
            music = Clips("SHARD8_DES_A V2", "SHARD8_DES_B_V1", "SHARD8_DES_C V1"), ambience = Clip("Au_Ambience_Desert")
        };
        var etereo = new BiomeDef
        {
            name = "Etéreo",
            skyTop = new Color(0.22f, 0.13f, 0.48f), skyHorizon = new Color(0.72f, 0.55f, 0.95f),
            fog = new Color(0.62f, 0.5f, 0.9f), fogDensity = 0.0009f, sun = new Color(0.95f, 0.85f, 1f), sunIntensity = 1.3f, sunEuler = new Vector3(30f, 120f, 0f),
            ambTop = new Color(0.85f, 0.75f, 1f), ambMid = new Color(0.6f, 0.5f, 0.85f), ambBot = new Color(0.3f, 0.25f, 0.45f),
            road = new Color(1f, 0.9f, 1f), flat = new Color(0.72f, 0.56f, 1f), slope = new Color(0.45f, 0.32f, 0.92f), cliff = new Color(0.18f, 0.1f, 0.42f), far = new Color(0.4f, 0.28f, 0.75f),
            palette = new[] { new Color(0.75f, 0.6f, 1f), new Color(0.32f, 0.22f, 0.75f), new Color(1f, 0.35f, 0.75f), new Color(1f, 0.85f, 0.6f), new Color(0.25f, 1f, 0.8f) },
            toneMid = new Color(0.8f, 0.72f, 0.95f), toneLow = new Color(0.48f, 0.38f, 0.75f), ink = new Color(0.1f, 0.02f, 0.18f), ui = new Color(0.3f, 1f, 0.8f), ui2 = new Color(1f, 0.35f, 0.75f), uiInk = new Color(0.13f, 0.04f, 0.26f),
            small = Props("Mo_Ethereal_World_Rock_Small_", "Mo_Mushroom_World_Bush_"), medium = Props("Mo_Ethereal_World_Crystal_", "Mo_Ethereal_World_Basic_Rock_", "Mo_Mushroom_World_Main_Big_Mushroom_"),
            landmarks = Props("Mo_Ethereal_World_GroupCrystals_", "Mo_Mushroom_World_Huge_Flower", "Mo_Mushroom_World_Birch_Tree_"),
            music = Clips("ETHERAL_A V1", "ETHERAL_B V1", "ETHERAL_C V1"), ambience = null
        };

        // ---------- scene ----------
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var sky = new Material(Shader.Find("Sledbonk/Sky"));
        AssetDatabase.CreateAsset(sky, Gen + "/Materials/Sky.mat"); RenderSettings.skybox = sky;

        var sun = new GameObject("Sol").AddComponent<Light>();
        sun.type = LightType.Directional; sun.shadows = LightShadows.Hard; sun.shadowStrength = 1f;
        var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urp)
        {
            urp.shadowDistance = 220f; urp.shadowCascadeCount = 4;
            urp.shadowDepthBias = 1.6f; urp.shadowNormalBias = 1.4f;
            EditorUtility.SetDirty(urp);
        }
        RenderSettings.sun = sun;

        var volGo = new GameObject("Pós-processamento"); var vol = volGo.AddComponent<Volume>(); vol.isGlobal = true;
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        var tm = profile.Add<Tonemapping>(true); tm.mode.Override(TonemappingMode.None);
        var bl = profile.Add<Bloom>(true); bl.intensity.Override(0.3f); bl.threshold.Override(1.1f); bl.scatter.Override(0.5f);
        var vg = profile.Add<Vignette>(true); vg.intensity.Override(0.16f);
        AssetDatabase.CreateAsset(profile, Gen + "/PostProfile.asset"); vol.sharedProfile = profile;

        var managers = new GameObject("Jogo");
        managers.AddComponent<ComicFX>();
        var lib = managers.AddComponent<BiomeLibrary>(); lib.biomes = new[] { neve, floresta, deserto, etereo }; lib.skybox = sky;
        var track = managers.AddComponent<Track>();
        AssignCustomerAssets(track);
        track.pizzeria = MakePizzeriaPrefab();
        track.terrainMat = terrainMat; track.farMat = farMat; track.railMat = railMat; track.sparkMat = sparkMat; track.ringMat = ringMat; track.finishMat = finishMat;

        var playerGo = new GameObject("Jogador"); playerGo.layer = PlayerCharacter.PlayerLayer;
        var rb = playerGo.AddComponent<Rigidbody>(); rb.mass = 50f; rb.useGravity = false; rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.constraints = RigidbodyConstraints.FreezeRotation; rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        var cap = playerGo.AddComponent<CapsuleCollider>(); cap.radius = 0.45f; cap.height = 1.6f; cap.center = new Vector3(0, 0.8f, 0);
        var pm = new PhysicsMaterial("Player") { dynamicFriction = 0f, staticFriction = 0f, frictionCombine = PhysicsMaterialCombine.Minimum };
        AssetDatabase.CreateAsset(pm, Gen + "/PlayerPhysics.asset"); cap.sharedMaterial = pm;
        var pc = playerGo.AddComponent<PlayerCharacter>(); pc.bodyCollider = cap;
        playerGo.AddComponent<PlayerMovement>(); playerGo.AddComponent<PlayerSlowMovement>();
        playerGo.AddComponent<TrickSystem>(); playerGo.AddComponent<PlayerMods>();
        var visRoot = new GameObject("Visual").transform; visRoot.SetParent(playerGo.transform, false);
        var pv = playerGo.AddComponent<PlayerVisual>(); pv.player = pc; pv.root = visRoot;
        var ch = (GameObject)PrefabUtility.InstantiatePrefab(character, visRoot); pv.character = ch.transform; pv.animator = ch.GetComponentInChildren<Animator>();
        if (sled) { var s = (GameObject)PrefabUtility.InstantiatePrefab(sled, visRoot); s.transform.localPosition = Vector3.zero; pv.sled = s.transform; }

        var camRig = new GameObject("Câmera").AddComponent<CameraRig>(); camRig.player = pc;
        var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera"; camGo.transform.SetParent(camRig.transform, false);
        var cam = camGo.AddComponent<Camera>(); cam.nearClipPlane = 0.25f; cam.farClipPlane = 6000f; camRig.cam = cam;
        var urpCam = camGo.AddComponent<UniversalAdditionalCameraData>(); urpCam.renderPostProcessing = true; urpCam.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        camGo.AddComponent<AudioListener>();

        var sfx = managers.AddComponent<Sfx>();
        sfx.windGround = Clip("Au_WindGrounded"); sfx.windAir = Clip("Au_WindAir"); sfx.windFastFall = Clip("Au_WindFastFall");
        sfx.boostLoop = Clip("Au_SpeedBoost_Loop"); sfx.sledLoop = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Imported/Sled/AudioClip/sled_loop_snow.ogg"); sfx.crackLoop = Clip("Au_WorldCrackLoop");
        sfx.jump = Clip("Au_BoardJump"); sfx.landBad = Clip("Au_WallHitLayer1"); sfx.landOk = Clip("Au_Land_OK_1"); sfx.landGood = Clip("Au_Board_Land1"); sfx.landPerfect = Clip("Au_Landing_Good");
        sfx.trick = Clip("Au_CoinGet1"); sfx.bail = Clip("Au_Tutorial_Land_Error"); sfx.grindStart = Clip("Au_Board_Booster2"); sfx.ring = Clip("Au_RingBoost"); sfx.spark = Clip("Au_CoinBlip1");
        sfx.wallHit = Clip("Au_WallHitLayer1"); sfx.boostStart = Clip("Au_DashBoost");
        sfx.uiSelect = Clip("Au_UI_Select1"); sfx.uiHover = Clip("Au_UI_Item_Hover"); sfx.itemPick = Clip("Au_UI_Powerup_Select"); sfx.levelEnd = Clip("Au_ShardExit");
        sfx.countdown = Clip("Au_UI_Select2"); sfx.go = Clip("Au_DashBoost");

        var rm = managers.AddComponent<RunManager>(); rm.player = pc; rm.sun = sun; rm.chaseMat = chaseMat;

        var ui = new GameObject("UI");
        var hud = ui.AddComponent<Hud>(); hud.player = pc; hud.markerMat = markerMat;
        hud.heartTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Imported/UI/Heart.png");
        hud.titleFont = AssetDatabase.LoadAssetAtPath<Font>(BaseDir + "Font/AkzidenzGroteskPro-BoldCnIt.otf");
        hud.bodyFont = AssetDatabase.LoadAssetAtPath<Font>(BaseDir + "Font/AkzidenzGroteskPro-Bold.otf");

        Directory.CreateDirectory("Assets/Scenes");
        MakePropMeshesReadable();
        SetupSmokePass();
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Sledbonk.unity");
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/Sledbonk.unity", true) };
        AssetDatabase.SaveAssets();
        Debug.Log("Sledbonk: scene built");
    }

    static AudioClip Clip(string name)
    {
        foreach (var ext in new[] { ".ogg", ".wav" })
        {
            var c = AssetDatabase.LoadAssetAtPath<AudioClip>(BaseDir + "AudioClip/" + name + ext);
            if (c) return c;
        }
        Debug.LogWarning("Missing clip " + name); return null;
    }
    static AudioClip[] Clips(params string[] names) { var l = new List<AudioClip>(); foreach (var n in names) { var c = Clip(n); if (c) l.Add(c); } return l.ToArray(); }
    static GameObject[] Concat(GameObject[] a, GameObject[] b) { var l = new List<GameObject>(a); l.AddRange(b); return l.ToArray(); }

    static GameObject[] Props(params string[] prefixes)
    {
        var res = new List<GameObject>();
        foreach (var f in Directory.GetFiles(BaseDir + "GameObject", "*.prefab"))
        {
            var n = Path.GetFileNameWithoutExtension(f);
            bool match = false; foreach (var p in prefixes) if (n.StartsWith(p)) { match = true; break; }
            if (!match) continue;
            var g = CleanImported(f.Replace('\\', '/'), n);
            if (g) res.Add(g);
        }
        return res.ToArray();
    }

    static GameObject CleanImported(string src, string name)
    {
        paletteMode = true;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(src);
        if (!prefab) return null;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        foreach (var lg in go.GetComponentsInChildren<LODGroup>(true))
        {
            var lods = lg.GetLODs();
            var keep = new HashSet<Renderer>(lods.Length > 0 ? lods[0].renderers : new Renderer[0]);
            for (int i = 1; i < lods.Length; i++)
                foreach (var r in lods[i].renderers)
                    if (r && !keep.Contains(r)) { if (r.transform != go.transform) Object.DestroyImmediate(r.gameObject); else Object.DestroyImmediate(r); }
            Object.DestroyImmediate(lg);
        }
        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true)) if (ps) Object.DestroyImmediate(ps.gameObject);
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            if (!r) continue;
            foreach (var m in r.sharedMaterials) if (m && m.name.Contains("Impostor")) { Object.DestroyImmediate(r); break; }
        }
        StripAndConvert(go);
        paletteMode = false;
        go.transform.position = Vector3.zero;
        var res = PrefabUtility.SaveAsPrefabAsset(go, Gen + "/Prefabs/World/" + name + ".prefab");
        Object.DestroyImmediate(go);
        return res;
    }

    static void SetupLayers()
    {
        var tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tm.FindProperty("layers");
        layers.GetArrayElementAtIndex(PlayerCharacter.TerrainLayer).stringValue = "Terrain";
        layers.GetArrayElementAtIndex(PlayerCharacter.PlayerLayer).stringValue = "Player";
        layers.GetArrayElementAtIndex(PlayerCharacter.EnemyLayer).stringValue = "Enemy";
        layers.GetArrayElementAtIndex(PlayerCharacter.PropLayer).stringValue = "Prop";
        layers.GetArrayElementAtIndex(SmokeSignal.Layer).stringValue = "Smoke";
        tm.ApplyModifiedProperties();
    }

    static Material Mat(string name, Color c, System.Action<Material> setup)
    {
        var path = Gen + "/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m) { m = new Material(toon); AssetDatabase.CreateAsset(m, path); }
        m.shader = toon; m.SetColor("_BaseColor", c); setup?.Invoke(m); EditorUtility.SetDirty(m);
        return m;
    }

    static readonly Dictionary<Material, Material> converted = new Dictionary<Material, Material>();
    static bool paletteMode;

    static float SlotFor(string name)
    {
        var n = name.ToLower();
        foreach (var k in new[] { "leaf", "foliage", "fern", "grass", "bush", "flower", "lavender", "bamboo", "evergreen", "pine", "cactus", "plant", "needle" }) if (n.Contains(k)) return 2f;
        foreach (var k in new[] { "crystal", "emissive", "shroom", "mushroom", "coral", "bell" }) if (n.Contains(k)) return 4f;
        foreach (var k in new[] { "wood", "trunk", "bark", "log", "tree" }) if (n.Contains(k)) return 3f;
        foreach (var k in new[] { "rock", "stone", "ruin", "canyon", "cliff", "statue", "arch", "trim", "formation" }) if (n.Contains(k)) return 1f;
        return 0f;
    }

    static Material Mat2(string name, Shader sh)
    {
        var path = Gen + "/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
        m.shader = sh; EditorUtility.SetDirty(m); return m;
    }

    // adds (once) a Full Screen Pass feature running Sledbonk/Comic to every URP renderer in the project
    // a non-readable mesh can't be turned into a collider, so every prop would be ghost.
    [MenuItem("Sledbonk/Malhas dos props legíveis")]
    public static void MakePropMeshesReadable()
    {
        var guids = AssetDatabase.FindAssets("t:Prefab", new[] { Gen + "/Prefabs/World" });
        var paths = new List<string>(); foreach (var g in guids) paths.Add(AssetDatabase.GUIDToAssetPath(g));
        foreach (var dep in AssetDatabase.GetDependencies(paths.ToArray(), true))
        {
            if (AssetImporter.GetAtPath(dep) is ModelImporter imp) { if (!imp.isReadable) { imp.isReadable = true; imp.SaveAndReimport(); } continue; }
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(dep))
            {
                if (!(o is Mesh m) || m.isReadable) continue;
                var so = new SerializedObject(m); var pr = so.FindProperty("m_IsReadable");
                if (pr == null) continue;
                pr.boolValue = true; so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(m);
            }
        }
        AssetDatabase.SaveAssets();
    }

    // with the slice logo, a stack of boxes on the counter and two lanterns. Saved as a prefab the track places.
    [MenuItem("Sledbonk/Pizzaria")]
    public static GameObject MakePizzeriaPrefab()
    {
        if (!toon) toon = Shader.Find("Sledbonk/Toon");
        Material M(string name, Color c, System.Action<Material> setup = null)
        {
            var path = Gen + "/Materials/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(toon); AssetDatabase.CreateAsset(m, path); }
            m.shader = toon; m.SetFloat("_PaletteSlot", -1f); m.SetColor("_BaseColor", c); setup?.Invoke(m); EditorUtility.SetDirty(m);
            return m;
        }
        var srcMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Imported/Sled/Material/Props Atlas Material.mat");
        ReadSaved(srcMat, out var atlas, out _);
        var stallMat = M("Pizzaria Barraca (toon)", Color.white, m => { if (atlas) m.SetTexture("_BaseMap", atlas); });
        var kraft = M("Pizzaria Caixa (toon)", new Color(0.87f, 0.66f, 0.42f), m => { m.SetFloat("_TexStrength", 0f); m.SetColor("_Emission", new Color(0.05f, 0.04f, 0.02f)); });
        var red = M("Pizzaria Vermelho (toon)", new Color(0.86f, 0.18f, 0.16f), m => { m.SetFloat("_TexStrength", 0f); m.SetColor("_Emission", new Color(0.1f, 0f, 0f)); });
        var wood = M("Pizzaria Madeira (toon)", new Color(0.45f, 0.27f, 0.15f), m => m.SetFloat("_TexStrength", 0f));
        var lamp = M("Pizzaria Lampiao (toon)", new Color(1f, 0.88f, 0.5f), m => { m.SetFloat("_TexStrength", 0f); m.SetColor("_Emission", new Color(1f, 0.7f, 0.25f)); });
        var logoTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/PizzaLogo.png");
        var logo = M("Pizzaria Logo (toon)", Color.white, m => { m.SetTexture("_BaseMap", logoTex); m.SetFloat("_Cutoff", 0.5f); m.SetFloat("_TexStrength", 0f); m.SetFloat("_Cull", 0f); });

        var root = new GameObject("Pizzaria");
        GameObject Part(PrimitiveType t, Vector3 pos, Vector3 scl, Vector3 eul, Material m)
        {
            var g = GameObject.CreatePrimitive(t); Object.DestroyImmediate(g.GetComponent<Collider>());
            g.transform.SetParent(root.transform, false); g.transform.localPosition = pos; g.transform.localScale = scl; g.transform.localEulerAngles = eul;
            g.GetComponent<MeshRenderer>().sharedMaterial = m; return g;
        }
        // the stall, scaled up so it reads from the sled
        var stall = new GameObject("barraca"); stall.transform.SetParent(root.transform, false);
        stall.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Imported/Sled/Mesh/default_10.asset");
        stall.AddComponent<MeshRenderer>().sharedMaterial = stallMat;
        stall.transform.localScale = Vector3.one * 1.6f;
        var b = stall.GetComponent<MeshRenderer>().bounds;
        stall.transform.localPosition = new Vector3(-b.center.x, -b.min.y, -b.center.z);
        b = stall.GetComponent<MeshRenderer>().bounds;
        float top = b.max.y;
        // the round sign on two posts above the stall
        Part(PrimitiveType.Cylinder, new Vector3(-2.2f, top + 1.2f, 0f), new Vector3(0.25f, 1.2f, 0.25f), Vector3.zero, wood);
        Part(PrimitiveType.Cylinder, new Vector3(2.2f, top + 1.2f, 0f), new Vector3(0.25f, 1.2f, 0.25f), Vector3.zero, wood);
        Part(PrimitiveType.Cylinder, new Vector3(0f, top + 3.4f, -0.05f), new Vector3(4.6f, 0.15f, 4.6f), new Vector3(90f, 0f, 0f), red);
        Part(PrimitiveType.Quad, new Vector3(0f, top + 3.4f, 0.12f), new Vector3(4.4f, 4.4f, 1f), new Vector3(0f, 180f, 0f), logo);   // faces the road (+z)
        // boxes on the counter
        for (int k = 0; k < 4; k++)
        {
            var bx = Part(PrimitiveType.Cube, new Vector3(-1.6f + (k % 2) * 0.1f, b.center.y + 0.15f + k * 0.17f, 0.75f), new Vector3(1.1f, 0.15f, 1.1f), new Vector3(0f, k * 7f, 0f), kraft);
        }
        // lanterns either side
        foreach (var x in new[] { -4.2f, 4.2f })
        {
            Part(PrimitiveType.Cylinder, new Vector3(x, 1.7f, 1.2f), new Vector3(0.2f, 1.7f, 0.2f), Vector3.zero, wood);
            Part(PrimitiveType.Sphere, new Vector3(x, 3.6f, 1.2f), Vector3.one * 0.8f, Vector3.zero, lamp);
        }
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, Gen + "/Prefabs/Pizzaria.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    // on the toon shader (Agency panels in our own "enemy" colours, biome rocks/ice on the palette), every VFX renderer
    // (beams, warning spheres, glows) kept but switched off so the runtime code can reuse its mesh with our own beam
    public static readonly string[] ObstacleNames = {
        "AgencyBomb", "AgencyBomb_Moving", "Laser_Flying_Single", "Laser_flying_Group_Line", "Laser_flying_Group_Square", "Laser_flying_Group_X", "Pole_Block", "Pole_Modular_Fixed", "Pole_Modular_Fixed_Linked",
        "LaserGridOrb", "LaserGridOrbBig", "LaserGridOrbHuge", "MiningLaser", "MiningHole", "GemProjectile", "MissileTower",
        "Missile", "Barrage", "LaserProj", "DropPod", "Wall", "Meteor", "Gayser", "IceSpike", "Desert_MovingSpike",
        "LaserSpinner", "LaserRing", "LaserRing_Big" };

    [MenuItem("Sledbonk/Obstáculos")]
    public static void MakeObstaclePrefabs()
    {
        if (!toon) toon = Shader.Find("Sledbonk/Toon");
        const string dir = Gen + "/Prefabs/World/Obstaculos";
        Directory.CreateDirectory(dir);
        Material Own(string name, Color c, Color em)
        {
            var path = Gen + "/Materials/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(toon); AssetDatabase.CreateAsset(m, path); }
            m.shader = toon; m.SetFloat("_PaletteSlot", -1f); m.SetFloat("_TexStrength", 0f);
            m.SetColor("_BaseColor", c); m.SetColor("_Emission", em); EditorUtility.SetDirty(m);
            return m;
        }
        // the toon shader, orange emissive lights. Biome rocks/ice stay on the palette.
        var agencyCache = new Dictionary<Material, Material>();
        Material Agency(Material src)
        {
            if (!src) return null;
            var n = src.name.ToLower();
            bool agencyish = n.Contains("agency") || n == "m_metal" || n.StartsWith("missile") || System.Text.RegularExpressions.Regex.IsMatch(n, @"^\d\d - default");
            if (!agencyish) return null;
            if (agencyCache.TryGetValue(src, out var done)) return done;
            ReadSaved(src, out var tex, out var col);
            if (!(tex is Texture2D)) tex = null;
            var path = Gen + "/Materials/Agency " + src.name.Replace("/", "_") + " (toon).mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(toon); AssetDatabase.CreateAsset(m, path); }
            m.shader = toon; m.SetFloat("_PaletteSlot", -1f);
            col.a = 1f; m.SetColor("_BaseColor", col);
            m.SetTexture("_BaseMap", tex); m.SetFloat("_TexStrength", tex ? 0.6f : 0f);
            bool lit = n.Contains("emissive");
            m.SetColor("_Emission", lit ? col * 0.9f : Color.black);
            if (lit) m.SetFloat("_TexStrength", 0f);
            EditorUtility.SetDirty(m);
            agencyCache[src] = m;
            return m;
        }
        bool Vfx(Material m) { if (!m) return true; var n = m.name.ToLower(); return n.StartsWith("m_vfx") || n.Contains("glow") || n.Contains("coin") || n.Contains("grid prototype") || n.Contains("warning") || n.Contains("ethbeam") || n.Contains("ethring"); }

        var list = new List<GameObject>();
        foreach (var name in ObstacleNames)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BaseDir + "GameObject/" + name + ".prefab");
            if (!prefab) { Debug.LogWarning("Missing obstacle " + name); continue; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            var hd = ReadObstacleData(prefab, go);
            foreach (var lg in go.GetComponentsInChildren<LODGroup>(true))
            {
                var lods = lg.GetLODs();
                var keep = new HashSet<Renderer>(lods.Length > 0 ? lods[0].renderers : new Renderer[0]);
                for (int i = 1; i < lods.Length; i++)
                    foreach (var r in lods[i].renderers)
                        if (r && !keep.Contains(r)) { if (r.transform != go.transform) Object.DestroyImmediate(r.gameObject); else Object.DestroyImmediate(r); }
                Object.DestroyImmediate(lg);
            }
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                if (t && t != go.transform && (t.name.StartsWith("PropBlocker") || t.name == "PropDestroyer" || t.name.StartsWith("Grunt")))
                    Object.DestroyImmediate(t.gameObject);
            foreach (var sk in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)) if (sk) Object.DestroyImmediate(sk);
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true)) if (ps) { var pr = ps.GetComponent<ParticleSystemRenderer>(); Object.DestroyImmediate(ps); if (pr) Object.DestroyImmediate(pr); }
            foreach (var tr in go.GetComponentsInChildren<TrailRenderer>(true)) Object.DestroyImmediate(tr);
            foreach (var lr in go.GetComponentsInChildren<LineRenderer>(true)) Object.DestroyImmediate(lr);
            foreach (var an in go.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(an);
            foreach (var an in go.GetComponentsInChildren<Animation>(true)) Object.DestroyImmediate(an);
            foreach (var l in go.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(l);
            // remember what each slot was before the toon conversion renames everything
            var vfx = new HashSet<Renderer>(); var agency = new Dictionary<Renderer, Material[]>();
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var ms = r.sharedMaterials;
                if (System.Array.Exists(ms, Vfx)) { vfx.Add(r); continue; }
                var ag = new Material[ms.Length]; bool any = false;
                for (int i = 0; i < ms.Length; i++) { ag[i] = Agency(ms[i]); any |= ag[i]; }
                if (any) agency[r] = ag;
            }
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) if (c.isTrigger || !c.enabled || c.gameObject.layer == 8) Object.DestroyImmediate(c);
            var solids = new List<(GameObject host, Collider data)>();
            foreach (var c in go.GetComponentsInChildren<Collider>(true))
            {
                var holder = new GameObject("_col"); holder.SetActive(false);
                var copy = (Collider)holder.AddComponent(c.GetType());
                EditorUtility.CopySerialized(c, copy);
                solids.Add((c.gameObject, copy));
            }
            paletteMode = true;
            StripAndConvert(go);
            paletteMode = false;
            foreach (var (host, data) in solids)
            {
                if (host) { var c = (Collider)host.AddComponent(data.GetType()); EditorUtility.CopySerialized(data, c); c.enabled = true; host.layer = PlayerCharacter.PropLayer; }
                Object.DestroyImmediate(data.gameObject);
            }
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (vfx.Contains(r)) { r.enabled = false; continue; }
                r.shadowCastingMode = ShadowCastingMode.On;
                if (agency.TryGetValue(r, out var ag))
                {
                    var ms = r.sharedMaterials;
                    for (int i = 0; i < ms.Length && i < ag.Length; i++) if (ag[i]) ms[i] = ag[i];
                    r.sharedMaterials = ms;
                }
            }
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true)) if (mf.sharedMesh && !mf.sharedMesh.isReadable) MakeReadable(mf.sharedMesh);
            go.transform.position = Vector3.zero; go.name = name;
            var moving = new HashSet<Transform>();
            foreach (var x in hd.spins) if (x.t) moving.Add(x.t);
            foreach (var x in hd.movers) if (x.t) moving.Add(x.t);
            foreach (var x in hd.follows) if (x.t) moving.Add(x.t);
            // only the static giants get merged; pods, walls, spikes, meteors are animated piece by piece by the game
            if (name == "LaserSpinner" || name.StartsWith("LaserRing") || name == "Barrage")
            {
                foreach (var t in go.GetComponentsInChildren<Transform>(true)) if (t.name == "Spawn") moving.Add(t);   // the barrage turret aims
                CombineParts(go, dir, name, moving);
            }
            hd.spins.RemoveAll(x => !x.t); hd.movers.RemoveAll(x => !x.t); hd.follows.RemoveAll(x => !x.t || !x.target); hd.beams.RemoveAll(x => !x.t); hd.snaps.RemoveAll(x => !x.t);
            list.Add(PrefabUtility.SaveAsPrefabAsset(go, dir + "/" + name + ".prefab"));
            Object.DestroyImmediate(go);
        }
        AssetDatabase.SaveAssets();
        var track = Object.FindFirstObjectByType<Track>();
        if (track)
        {
            track.obstacles = list.ToArray(); EditorUtility.SetDirty(track);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(track.gameObject.scene);
        }
        Debug.Log("Obstacles: " + list.Count);
    }

    const string GuidRotate = "c5809eabcc804b3ebf9eef0e9a96b1e8", GuidMoving = "07801299a8778710a50812c0d05e8d91",
        GuidSpring = "e5656a0fb799f822b7ef66e75b411d6c", GuidLaser = "0e2252e52c33037e86e5e54efa7a7986", GuidSnap = "1d27d60b676973968c11260e7fea6771";

    static ObstacleData ReadObstacleData(GameObject asset, GameObject inst)
    {
        var path = AssetDatabase.GetAssetPath(asset);
        var text = File.ReadAllText(path);
        var byId = new Dictionary<long, Object>();
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
            if (o && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o, out string g, out long id)) byId[id] = o;
        var pair = new Dictionary<Transform, Transform>();
        void Walk(Transform a, Transform b) { pair[a] = b; for (int i = 0; i < a.childCount && i < b.childCount; i++) Walk(a.GetChild(i), b.GetChild(i)); }
        Walk(asset.transform, inst.transform);
        Transform T(string fid)
        {
            if (!long.TryParse(fid, out long id) || !byId.TryGetValue(id, out var o)) return null;
            Transform at = o is Transform tt ? tt : o is GameObject go ? go.transform : o is Component c ? c.transform : null;
            return at && pair.TryGetValue(at, out var it) ? it : null;
        }
        float F(string doc, string key, float def)
        {
            var m = System.Text.RegularExpressions.Regex.Match(doc, @"\n  " + key + @": (-?[0-9.eE+-]+)");
            return m.Success && float.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : def;
        }
        Vector3 V(string doc, string key)
        {
            var m = System.Text.RegularExpressions.Regex.Match(doc, @"\n  " + key + @": \{x: (-?[0-9.eE+-]+), y: (-?[0-9.eE+-]+)(?:, z: (-?[0-9.eE+-]+))?");
            if (!m.Success) return Vector3.zero;
            float P(string s) => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
            return new Vector3(P(m.Groups[1].Value), P(m.Groups[2].Value), m.Groups[3].Success ? P(m.Groups[3].Value) : 0f);
        }
        string Ref(string doc, string key) { var m = System.Text.RegularExpressions.Regex.Match(doc, @"\n  " + key + @": \{fileID: (-?\d+)"); return m.Success ? m.Groups[1].Value : null; }

        var data = inst.GetComponent<ObstacleData>(); if (!data) data = inst.AddComponent<ObstacleData>();
        float rootScale = Mathf.Max(1e-4f, inst.transform.lossyScale.x);
        var docs = System.Text.RegularExpressions.Regex.Split(text, @"\n--- !u!\d+ &");
        foreach (var d in docs)
        {
            if (!d.Contains("m_Script:")) continue;
            var host = T(Ref(d, "m_GameObject"));
            if (!host) continue;
            if (d.Contains(GuidRotate))
                data.spins.Add(new ObstacleData.Spin { t = host, rotation = V(d, "rotation"), random = F(d, "random", 0f), world = F(d, "space", 1f) == 0f });
            else if (d.Contains(GuidMoving))
            {
                var mv = new ObstacleData.Mover { t = host, startId = (int)F(d, "startingTargetId", 0), baseSpeed = F(d, "baseSpeed", 5f), threshold = F(d, "distanceThreshold", 2f),
                    lerp = F(d, "lerpSpeed", 0.1f), drag = F(d, "drag", 0.98f), rotateSpeed = F(d, "rotateSpeed", 1f), randomStart = F(d, "randomizeStartId", 0f) > 0f,
                    startAtFirst = F(d, "startAtFirstPoint", 1f) > 0f, loop = F(d, "shoudLoop", 1f) > 0f };
                var m = V(d, "speedMultiplier"); mv.mult = new Vector2(m.x, m.y);
                var block = System.Text.RegularExpressions.Regex.Match(d, @"\n  movePoints:\n((?:  - \{fileID: -?\d+\}\n)+)");
                if (block.Success)
                    foreach (System.Text.RegularExpressions.Match pm in System.Text.RegularExpressions.Regex.Matches(block.Groups[1].Value, @"fileID: (-?\d+)"))
                    { var pt = T(pm.Groups[1].Value); if (pt) mv.points.Add(pt); }
                if (mv.points.Count > 0) data.movers.Add(mv);
            }
            else if (d.Contains(GuidSpring))
            {
                var tg = T(Ref(d, "target"));
                if (tg) data.follows.Add(new ObstacleData.Follow { t = host, target = tg, damp = F(d, "dampRatio", 0.5f), period = F(d, "secondsPerOscillation", 0.5f), rotSpring = F(d, "rotSpring", 5f) });
            }
            else if (d.Contains(GuidLaser))
            {
                // the beam mesh (LaserV3, extent 1) under the Scaler: its world scale is the beam radius
                float r = 3f;
                foreach (var mf in host.GetComponentsInChildren<MeshFilter>(true))
                    if (mf.name.StartsWith("LaserV3") || mf.name.StartsWith("Laser_Ringlaser")) { r = mf.transform.lossyScale.x; break; }
                data.beams.Add(new ObstacleData.Beam { t = host, maxLength = F(d, "maxLength", 300f), radius = r / rootScale });
            }
            else if (d.Contains(GuidSnap))
                data.snaps.Add(new ObstacleData.Snap { t = host, radius = F(d, "radius", 0f) });
        }
        return data;
    }

    // turning laser heads and rings, drone movers) as its own merged mesh so it can still turn.
    static readonly string[] MovingParts = { "Mo_Agency_Structure_2_LasersAndSphere", "Mo_Agency_Structure_3_Lasers", "Mover", "Followpos", "LaserCage", "Bomb" };
    static void CombineParts(GameObject go, string dir, string name, HashSet<Transform> moving)
    {
        var rs = new List<MeshRenderer>();
        foreach (var mr in go.GetComponentsInChildren<MeshRenderer>(true)) if (mr.enabled && mr.GetComponent<MeshFilter>() && mr.GetComponent<MeshFilter>().sharedMesh) rs.Add(mr);
        if (rs.Count < 24) return;
        var groups = new Dictionary<Transform, Dictionary<Material, List<CombineInstance>>>();
        foreach (var mr in rs)
        {
            Transform g = go.transform;
            for (var t = mr.transform; t && t != go.transform; t = t.parent) if (moving.Contains(t)) { g = t; break; }
            if (!groups.TryGetValue(g, out var byMat)) groups[g] = byMat = new Dictionary<Material, List<CombineInstance>>();
            var mesh = mr.GetComponent<MeshFilter>().sharedMesh; var mats = mr.sharedMaterials;
            for (int sm = 0; sm < mesh.subMeshCount && sm < mats.Length; sm++)
            {
                if (!mats[sm]) continue;
                if (!byMat.TryGetValue(mats[sm], out var list)) byMat[mats[sm]] = list = new List<CombineInstance>();
                list.Add(new CombineInstance { mesh = mesh, subMeshIndex = sm, transform = g.worldToLocalMatrix * mr.transform.localToWorldMatrix });
            }
        }
        foreach (var mr in rs) { var mf = mr.GetComponent<MeshFilter>(); Object.DestroyImmediate(mr); Object.DestroyImmediate(mf); }
        int k = 0;
        foreach (var g in groups)
            foreach (var bm in g.Value)
            {
                var m = new Mesh { name = name + "_" + k, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                m.CombineMeshes(bm.Value.ToArray(), true, true); m.RecalculateBounds();
                var path = dir + "/" + name + "_" + k + ".asset";
                AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(m, path);
                var c = new GameObject("junto_" + k); c.transform.SetParent(g.Key, false);
                c.AddComponent<MeshFilter>().sharedMesh = m;
                var r = c.AddComponent<MeshRenderer>(); r.sharedMaterial = bm.Key;
                k++;
            }
    }

    static void MakeReadable(Mesh mesh)
    {
        var path = AssetDatabase.GetAssetPath(mesh);
        if (string.IsNullOrEmpty(path) || !path.EndsWith(".asset")) return;
        var so = new SerializedObject(mesh); var p = so.FindProperty("m_IsReadable");
        if (p != null && !p.boolValue) { p.boolValue = true; so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(mesh); }
    }

    public static void AssignCustomerAssets(Track track)
    {
        const string hutPath = Gen + "/Prefabs/World/Mo_Desert_Rainy_World_Hut_2.prefab";
        if (!AssetDatabase.LoadAssetAtPath<GameObject>(hutPath)) CleanImported(BaseDir + "GameObject/Mo_Desert_Rainy_World_Hut_2.prefab", "Mo_Desert_Rainy_World_Hut_2");
        track.customerHouse = AssetDatabase.LoadAssetAtPath<GameObject>(hutPath);
        track.customerSkins = new[] { "Penguin_Default", "Duck_Mallard", "Frog_Default", "Panda_Bear" }
            .Select(n => AssetDatabase.LoadAssetAtPath<Material>(Gen + "/Materials/Skin_" + n + " (toon).mat")).ToArray();
        EditorUtility.SetDirty(track);
    }

    [MenuItem("Sledbonk/Incluir shaders de runtime")]
    public static void IncludeRuntimeShaders()
    {
        var gs = new SerializedObject(UnityEditor.AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
        var list = gs.FindProperty("m_AlwaysIncludedShaders");
        foreach (var name in new[] { "Sledbonk/UnlitTex", "Sledbonk/UISticker", "Sledbonk/Beam" })
        {
            var sh = Shader.Find(name); if (!sh) { Debug.LogError("Shader " + name + " not found"); continue; }
            bool has = false;
            for (int i = 0; i < list.arraySize; i++) if (list.GetArrayElementAtIndex(i).objectReferenceValue == sh) has = true;
            if (has) continue;
            list.InsertArrayElementAtIndex(list.arraySize); list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = sh;
        }
        gs.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
    }

    // Smoke is transparent, so it writes no depth and the comic pass (which finds edges in depth/normals) would draw
    // the outlines of whatever is behind it right over it. The Smoke layer is taken out of the normal passes and drawn
    // by its own pass right after the comic one.
    [MenuItem("Sledbonk/Passe da fumaça")]
    public static void SetupSmokePass()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:UniversalRendererData"))
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(guid));
            if (!data) continue;
            data.opaqueLayerMask &= ~(1 << SmokeSignal.Layer);
            data.transparentLayerMask &= ~(1 << SmokeSignal.Layer);
            RenderObjects f = null;
            foreach (var rf in data.rendererFeatures) if (rf is RenderObjects ro && rf.name == "Smoke") f = ro;
            if (!f)
            {
                f = ScriptableObject.CreateInstance<RenderObjects>();
                f.name = "Smoke";
                AssetDatabase.AddObjectToAsset(f, data);
                data.rendererFeatures.Add(f);   // after "Comic" in the list, so it runs after it at the same event
                var so = new SerializedObject(data);
                var map = so.FindProperty("m_RendererFeatureMap");
                if (map != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(f, out string _, out long localId)) { map.arraySize++; map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId; }
                so.ApplyModifiedProperties();
            }
            f.settings.Event = (RenderPassEvent)((int)RenderPassEvent.BeforeRenderingPostProcessing + 1);
            f.settings.filterSettings.RenderQueueType = RenderQueueType.Transparent;
            f.settings.filterSettings.LayerMask = 1 << SmokeSignal.Layer;
            f.SetActive(true);
            EditorUtility.SetDirty(f); EditorUtility.SetDirty(data);
        }
        AssetDatabase.SaveAssets();
    }

    static void SetupComicPass(Material mat)
    {
        foreach (var guid in AssetDatabase.FindAssets("t:UniversalRendererData"))
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(guid));
            if (!data) continue;
            FullScreenPassRendererFeature f = null;
            foreach (var rf in data.rendererFeatures) if (rf is FullScreenPassRendererFeature fs && rf.name == "Comic") f = fs;
            if (!f)
            {
                f = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
                f.name = "Comic";
                AssetDatabase.AddObjectToAsset(f, data);
                data.rendererFeatures.Add(f);
                var so = new SerializedObject(data);
                var map = so.FindProperty("m_RendererFeatureMap");
                if (map != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(f, out string _, out long localId)) { map.arraySize++; map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId; }
                so.ApplyModifiedProperties();
            }
            f.passMaterial = mat;
            f.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing;
            f.requirements = ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal | ScriptableRenderPassInput.Color;
            f.fetchColorBuffer = true;
            f.SetActive(true);
            EditorUtility.SetDirty(f); EditorUtility.SetDirty(data);
        }
    }

    static Material Convert(Material src, bool particle)
    {
        if (!src) return null;
        if (src.shader == toon) return src;
        if (converted.TryGetValue(src, out var done)) return done;
        ReadSaved(src, out var tex, out var col);
        var top = ReadColor(src, "_Color_Top", "_TopColor", "_ColorTop");
        var main = ReadColor(src, "_ColorMainText");
        if (main.HasValue) col = main.Value;
        Material m;
        if (particle)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 2f);   // transparent, additive
            m.SetOverrideTag("RenderType", "Transparent"); m.renderQueue = 3000;
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); m.SetInt("_DstBlend", (int)BlendMode.One); m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (tex) m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", col);
        }
        else
        {
            m = new Material(toon);
            if (tex) m.SetTexture("_BaseMap", tex);
            col.a = 1f; m.SetColor("_BaseColor", col);
            if (top.HasValue && top.Value.maxColorComponent > 0.01f) { var tc = top.Value; tc.a = 1f; m.SetColor("_TopColor", tc); m.SetFloat("_TopAmount", 1f); }
            m.SetColor("_RimColor", new Color(1, 1, 1, 0f));
            if (paletteMode) m.SetFloat("_PaletteSlot", SlotFor(src.name));
            var cutoff = ReadFloat(src, "_Cutoff");
            if (src.name.Contains("Eyes") || src.name.Contains("Face") || src.name.Contains("Mouth")) { m.SetFloat("_Cutoff", 0.5f); m.SetFloat("_Cull", 0f); }
            if (src.name.ToLower().Contains("leaf") || src.name.ToLower().Contains("foliage")) { m.SetFloat("_Cutoff", Mathf.Max(0.3f, cutoff ?? 0.3f)); m.SetFloat("_Cull", 0f); }
        }
        var safe = string.Join("_", src.name.Split(Path.GetInvalidFileNameChars()));
        var path = AssetDatabase.GenerateUniqueAssetPath(Gen + "/Materials/" + safe + (particle ? " (fx)" : " (toon)") + ".mat");
        AssetDatabase.CreateAsset(m, path);
        converted[src] = m;
        return m;
    }

    // Reads the texture/color straight from the material's saved properties, so it works even when the
    public static void ReadSaved(Material src, out Texture tex, out Color col)
    {
        tex = null; col = Color.white;
        var so = new SerializedObject(src);
        var texEnvs = so.FindProperty("m_SavedProperties.m_TexEnvs");
        var prefer = new[] { "_BaseMap", "_MainTex", "_Albedo", "_AlbedoMap", "_MainTexture", "_BaseColorMap", "_TintTex", "_Texture", "_Diffuse" };
        var found = new Dictionary<string, Texture>();
        for (int i = 0; texEnvs != null && i < texEnvs.arraySize; i++)
        {
            var e = texEnvs.GetArrayElementAtIndex(i);
            var t = e.FindPropertyRelative("second.m_Texture").objectReferenceValue as Texture;
            if (t) found[e.FindPropertyRelative("first").stringValue] = t;
        }
        foreach (var n in prefer) if (found.TryGetValue(n, out var t)) { tex = t; break; }
        if (!tex) foreach (var kv in found) { var k = kv.Key.ToLower(); if (!k.Contains("bump") && !k.Contains("normal") && !k.Contains("emission") && !k.Contains("mask") && !k.Contains("noise") && !k.Contains("ramp") && !k.Contains("detail")) { tex = kv.Value; break; } }
        var cols = so.FindProperty("m_SavedProperties.m_Colors");
        var cprefer = new[] { "_BaseColor", "_Color", "_TintColor", "_MainColor", "_AlbedoColor" };
        var cfound = new Dictionary<string, Color>();
        for (int i = 0; cols != null && i < cols.arraySize; i++)
        {
            var e = cols.GetArrayElementAtIndex(i);
            cfound[e.FindPropertyRelative("first").stringValue] = e.FindPropertyRelative("second").colorValue;
        }
        foreach (var n in cprefer) if (cfound.TryGetValue(n, out var c)) { col = c; break; }
    }

    static Color? ReadColor(Material src, params string[] names)
    {
        var cols = new SerializedObject(src).FindProperty("m_SavedProperties.m_Colors");
        for (int i = 0; cols != null && i < cols.arraySize; i++)
        {
            var e = cols.GetArrayElementAtIndex(i); var n = e.FindPropertyRelative("first").stringValue;
            foreach (var want in names) if (n == want) return e.FindPropertyRelative("second").colorValue;
        }
        return null;
    }

    static float? ReadFloat(Material src, string name)
    {
        var fl = new SerializedObject(src).FindProperty("m_SavedProperties.m_Floats");
        for (int i = 0; fl != null && i < fl.arraySize; i++)
        {
            var e = fl.GetArrayElementAtIndex(i);
            if (e.FindPropertyRelative("first").stringValue == name) return e.FindPropertyRelative("second").floatValue;
        }
        return null;
    }

    static void StripAndConvert(GameObject go)
    {
        foreach (var t in go.GetComponentsInChildren<Transform>(true)) GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
        foreach (var j in go.GetComponentsInChildren<Joint>(true)) Object.DestroyImmediate(j);
        foreach (var r in go.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(r);
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        foreach (var f in go.GetComponentsInChildren<AudioLowPassFilter>(true)) Object.DestroyImmediate(f);
        foreach (var a in go.GetComponentsInChildren<AudioSource>(true)) Object.DestroyImmediate(a);
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            bool particle = r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer;
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = Convert(mats[i], particle);
            r.sharedMaterials = mats;
            if (particle) r.shadowCastingMode = ShadowCastingMode.Off;
        }
        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true)) { var main = ps.main; main.loop = main.loop && ps.transform != go.transform; main.playOnAwake = true; }
    }

    static GameObject Clean(string src, string name, bool stripParticles = false)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(src);
        if (!prefab) { Debug.LogWarning("Missing prefab " + src); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        if (stripParticles)
        {
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true)) if (ps) Object.DestroyImmediate(ps.gameObject);
            foreach (var tr in go.GetComponentsInChildren<TrailRenderer>(true)) if (tr) Object.DestroyImmediate(tr);
        }
        StripAndConvert(go);
        go.transform.position = Vector3.zero;
        var res = PrefabUtility.SaveAsPrefabAsset(go, Gen + "/Prefabs/" + name + ".prefab");
        Object.DestroyImmediate(go);
        return res;
    }

    static GameObject MakeCharacter()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SledDir + "Player Networked.prefab");
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        var anim = go.GetComponentInChildren<Animator>(true);
        var model = anim ? anim.gameObject : go;
        model.transform.SetParent(null, true);
        Object.DestroyImmediate(go);
        model.SetActive(true);
        model.name = "Personagem";
        StripAndConvert(model);
        var gfx = FindDeep(model.transform, "Graphics (all characters start enabled)") ?? model.transform;
        foreach (Transform c in gfx)
        {
            bool isChar = c.GetComponentInChildren<SkinnedMeshRenderer>(true) && !c.name.StartsWith("metarig");
            if (isChar) c.gameObject.SetActive(c.name == characterName);
        }
        if (anim) { anim.applyRootMotion = false; anim.cullingMode = AnimatorCullingMode.AlwaysAnimate; }
        model.transform.position = Vector3.zero; model.transform.rotation = Quaternion.identity;
        var res = PrefabUtility.SaveAsPrefabAsset(model, Gen + "/Prefabs/Personagem.prefab");
        Object.DestroyImmediate(model);
        return res;
    }

    static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform c in t) { var r = FindDeep(c, name); if (r) return r; }
        return null;
    }
}
#endif
