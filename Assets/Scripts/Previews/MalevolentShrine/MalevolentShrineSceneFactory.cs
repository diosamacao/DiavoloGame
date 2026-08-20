using UnityEngine;

/// <summary>在空根节点下程序化搭建御厨子、假建筑、假敌、灯光与摄像机。</summary>
public static class MalevolentShrineSceneFactory
{
    public static MalevolentShrinePreviewRefs Build(Transform root, MalevolentShrinePreviewSettings settings)
    {
        if (root == null)
            throw new System.ArgumentNullException(nameof(root));
        if (settings == null)
            settings = MalevolentShrinePreviewSettings.CreateDefault();

        ClearChildren(root);
        MalevolentShrineMaterials materials = MalevolentShrineMaterials.Create();
        MalevolentShrinePreviewRefs refs = new MalevolentShrinePreviewRefs
        {
            root = root,
            materials = materials,
            slashMesh = MalevolentShrineMeshFactory.CreateSlashCard()
        };

        BuildGround(root, settings, materials);
        refs.groundGlow = BuildGroundGlow(root, materials);
        refs.ring = BuildRing(root, settings, materials);
        refs.shrine = BuildShrine(root, settings, materials, refs);
        refs.shrineDust = BuildDust(refs.shrine, materials.dust, new Vector3(0f, 0.4f, 0f), 3.2f);
        refs.shrineSmoke = BuildLoopSmoke(refs.shrine, materials.smoke, new Vector3(0f, 0.35f, 0f), 2.4f, 14f, 2.2f, 0.55f);
        refs.fieldSmoke = BuildLoopSmoke(root, materials.smoke, new Vector3(0f, 0.2f, 0f), settings.radius * 0.82f, 28f, 3.4f, 0.35f);
        refs.ambientAsh = BuildAsh(root, materials.ash, settings.radius);
        refs.sliceSmoke = BuildSliceSmoke(root, materials.dust);
        BuildCaster(root, materials, refs);
        refs.buildings = BuildBuildings(root, settings, materials);
        refs.targets = BuildTargets(root, settings, materials);
        BuildLights(root, settings, refs);
        BuildCamera(root, refs);
        return refs;
    }

    public static void ClearChildren(Transform root)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);
            if (Application.isPlaying)
                Object.Destroy(child.gameObject);
            else
                Object.DestroyImmediate(child.gameObject);
        }
    }

    static void BuildGround(Transform root, MalevolentShrinePreviewSettings settings, MalevolentShrineMaterials materials)
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.SetParent(root, false);
        ground.transform.localScale = Vector3.one * (settings.radius * 0.14f);
        ground.GetComponent<Renderer>().sharedMaterial = materials.ground;

        GameObject inner = new GameObject("AshDisc");
        inner.transform.SetParent(root, false);
        inner.transform.localPosition = new Vector3(0f, 0.02f, 0f);
        MeshFilter filter = inner.AddComponent<MeshFilter>();
        filter.sharedMesh = MalevolentShrineMeshFactory.CreateDisc(64, settings.radius * 0.22f);
        MeshRenderer renderer = inner.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = materials.wood;
    }

    static Transform BuildGroundGlow(Transform root, MalevolentShrineMaterials materials)
    {
        GameObject glow = new GameObject("DomainGlow");
        glow.transform.SetParent(root, false);
        glow.transform.localPosition = new Vector3(0f, 0.03f, 0f);
        MeshFilter filter = glow.AddComponent<MeshFilter>();
        filter.sharedMesh = MalevolentShrineMeshFactory.CreateDisc(72, 1f);
        MeshRenderer renderer = glow.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = materials.groundGlow;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        glow.transform.localScale = Vector3.zero;
        return glow.transform;
    }

    static Transform BuildRing(Transform root, MalevolentShrinePreviewSettings settings, MalevolentShrineMaterials materials)
    {
        GameObject ring = new GameObject("DomainRing");
        ring.transform.SetParent(root, false);
        ring.transform.localPosition = new Vector3(0f, 0.04f, 0f);
        MeshFilter filter = ring.AddComponent<MeshFilter>();
        filter.sharedMesh = MalevolentShrineMeshFactory.CreateDisc(72, 1f);
        MeshRenderer renderer = ring.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = materials.ring;
        ring.transform.localScale = Vector3.zero;
        return ring.transform;
    }

    static Transform BuildShrine(
        Transform root,
        MalevolentShrinePreviewSettings settings,
        MalevolentShrineMaterials materials,
        MalevolentShrinePreviewRefs refs)
    {
        Transform shrine = New("Shrine", root, new Vector3(0f, -settings.shrineHeight - 1.5f, 0f));
        refs.upperJaws = new Transform[4];
        refs.lowerJaws = new Transform[4];

        CreateBox("Pedestal", shrine, new Vector3(0f, 0.55f, 0f), Vector3.zero, new Vector3(6.4f, 1.1f, 6.4f), materials.wood);
        ScatterSkulls(shrine, materials, 18, 2.9f, 0.35f);

        CreateBox("Sanctum", shrine, new Vector3(0f, 3.35f, 0f), Vector3.zero, new Vector3(4.8f, 4.4f, 4.8f), materials.wood);
        CreateBox("RoofBase", shrine, new Vector3(0f, 5.75f, 0f), Vector3.zero, new Vector3(6.6f, 0.35f, 6.6f), materials.wood);
        CreateBox("Ridge", shrine, new Vector3(0f, 7.05f, 0f), Vector3.zero, new Vector3(0.45f, 0.35f, 5.2f), materials.wood);
        CreateBox("HipN", shrine, new Vector3(0f, 6.55f, 1.7f), new Vector3(-28f, 0f, 0f), new Vector3(5.6f, 0.16f, 3.4f), materials.wood);
        CreateBox("HipS", shrine, new Vector3(0f, 6.55f, -1.7f), new Vector3(28f, 0f, 0f), new Vector3(5.6f, 0.16f, 3.4f), materials.wood);
        CreateBox("HipE", shrine, new Vector3(1.7f, 6.55f, 0f), new Vector3(0f, 0f, 28f), new Vector3(3.4f, 0.16f, 5.6f), materials.wood);
        CreateBox("HipW", shrine, new Vector3(-1.7f, 6.55f, 0f), new Vector3(0f, 0f, -28f), new Vector3(3.4f, 0.16f, 5.6f), materials.wood);

        CreateCone("HornL", shrine, new Vector3(-1.15f, 7.3f, 0f), new Vector3(0f, 0f, 18f), new Vector3(0.28f, 2.1f, 0.28f), materials.horn);
        CreateCone("HornR", shrine, new Vector3(1.15f, 7.3f, 0f), new Vector3(0f, 0f, -18f), new Vector3(0.28f, 2.1f, 0.28f), materials.horn);
        CreateSphere("ApexSkull", shrine, new Vector3(0f, 7.55f, 0f), new Vector3(0.55f, 0.42f, 0.62f), materials.bone);

        Vector3[] dirs = { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };
        float[] yaws = { 0f, 90f, 180f, 270f };
        for (int i = 0; i < 4; i++)
            BuildMouth(shrine, dirs[i], yaws[i], materials, refs, i);

        Vector3[] corners =
        {
            new Vector3(3.1f, 0.15f, 3.1f),
            new Vector3(-3.1f, 0.15f, 3.1f),
            new Vector3(3.1f, 0.15f, -3.1f),
            new Vector3(-3.1f, 0.15f, -3.1f)
        };
        for (int i = 0; i < corners.Length; i++)
        {
            CreateCylinder("Stump" + i, shrine, corners[i], new Vector3(0.28f, 0.9f, 0.28f), materials.horn);
            CreateBox("Branch" + i, shrine, corners[i] + new Vector3(0.15f, 1.05f, 0.1f), new Vector3(20f, 35f * i, 12f), new Vector3(0.12f, 0.7f, 0.12f), materials.horn);
            CreateSphere("EaveSkull" + i, shrine, new Vector3(corners[i].x * 0.95f, 5.55f, corners[i].z * 0.95f), new Vector3(0.32f, 0.26f, 0.3f), materials.bone);
        }

        return shrine;
    }

    static void BuildMouth(
        Transform shrine,
        Vector3 dir,
        float yaw,
        MalevolentShrineMaterials materials,
        MalevolentShrinePreviewRefs refs,
        int index)
    {
        Transform mouth = New("Mouth" + index, shrine, dir * 2.42f + new Vector3(0f, 3.1f, 0f));
        mouth.localRotation = Quaternion.Euler(0f, yaw, 0f);
        CreateBox("Frame", mouth, Vector3.zero, Vector3.zero, new Vector3(2.35f, 2.05f, 0.35f), materials.wood);
        CreateBox("Interior", mouth, new Vector3(0f, 0f, 0.28f), Vector3.zero, new Vector3(1.85f, 1.55f, 0.08f), materials.fleshLit);
        Transform upper = CreateBox("UpperJaw", mouth, new Vector3(0f, 0.55f, -0.05f), new Vector3(-12f, 0f, 0f), new Vector3(2.05f, 0.28f, 0.7f), materials.flesh);
        Transform lower = CreateBox("LowerJaw", mouth, new Vector3(0f, -0.55f, -0.05f), new Vector3(16f, 0f, 0f), new Vector3(2.05f, 0.28f, 0.7f), materials.flesh);
        refs.upperJaws[index] = upper;
        refs.lowerJaws[index] = lower;
        CreateSphere("Tongue", mouth, new Vector3(0f, -0.18f, 0.18f), new Vector3(0.55f, 0.18f, 0.85f), materials.flesh);
        for (int i = 0; i < 6; i++)
        {
            float x = (i - 2.5f) * 0.28f;
            CreateBox("ToothU" + i, upper, new Vector3(x, -0.22f, -0.18f), Vector3.zero, new Vector3(0.12f, 0.22f, 0.12f), materials.bone);
            CreateBox("ToothL" + i, lower, new Vector3(x, 0.22f, -0.18f), Vector3.zero, new Vector3(0.1f, 0.18f, 0.1f), materials.bone);
        }
    }

    static void ScatterSkulls(Transform shrine, MalevolentShrineMaterials materials, int count, float radius, float y)
    {
        for (int i = 0; i < count; i++)
        {
            float a = i / (float)count * Mathf.PI * 2f + 0.35f;
            float r = radius * (0.55f + (i % 3) * 0.14f);
            Vector3 pos = new Vector3(Mathf.Cos(a) * r, y + (i % 2) * 0.18f, Mathf.Sin(a) * r);
            CreateSphere("Skull" + i, shrine, pos, new Vector3(0.38f, 0.3f, 0.42f), materials.bone);
        }
    }

    static void BuildCaster(Transform root, MalevolentShrineMaterials materials, MalevolentShrinePreviewRefs refs)
    {
        Transform caster = New("Caster", root, new Vector3(0f, 0f, 6.2f));
        refs.caster = caster;
        CreateCapsule("Body", caster, new Vector3(0f, 1.0f, 0f), new Vector3(0.7f, 1.0f, 0.7f), materials.caster);
        CreateSphere("Head", caster, new Vector3(0f, 1.95f, 0.05f), Vector3.one * 0.42f, materials.caster);
        CreateCapsule("ArmL", caster, new Vector3(-0.38f, 1.35f, 0.28f), new Vector3(0.22f, 0.45f, 0.22f), materials.caster);
        CreateCapsule("ArmR", caster, new Vector3(0.38f, 1.35f, 0.28f), new Vector3(0.22f, 0.45f, 0.22f), materials.caster);
        refs.leftHand = CreateSphere("HandL", caster, new Vector3(-0.16f, 1.42f, 0.58f), Vector3.one * 0.16f, materials.flesh);
        refs.rightHand = CreateSphere("HandR", caster, new Vector3(0.16f, 1.42f, 0.58f), Vector3.one * 0.16f, materials.flesh);
        refs.handEnergy = BuildDust(caster, refs.materials.energy, new Vector3(0f, 1.42f, 0.58f), 0.35f);
        ParticleSystem.MainModule handMain = refs.handEnergy.main;
        handMain.loop = true;
        handMain.playOnAwake = false;
        ParticleSystem.EmissionModule handEmission = refs.handEnergy.emission;
        handEmission.rateOverTime = 18f;
    }

    static MalevolentShrineDestructibleBuilding[] BuildBuildings(
        Transform root,
        MalevolentShrinePreviewSettings settings,
        MalevolentShrineMaterials materials)
    {
        Transform group = New("Buildings", root, Vector3.zero);
        MalevolentShrineDestructibleBuilding[] buildings = new MalevolentShrineDestructibleBuilding[settings.buildingCount];
        int placed = 0;
        int guard = 0;
        while (placed < settings.buildingCount && guard < settings.buildingCount * 4)
        {
            guard++;
            float a = (placed + guard) / (float)Mathf.Max(1, settings.buildingCount) * Mathf.PI * 2f + 0.2f;
            if (IsInCameraCorridor(a))
                continue;

            float r = settings.radius * (0.62f + (placed % 3) * 0.08f);
            Vector3 pos = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
            float width = 2.4f + (placed % 3) * 0.35f;
            float depth = 2.1f + (placed % 2) * 0.4f;
            float height = 6.5f + (placed % 4) * 1.6f;
            buildings[placed] = BuildOneBuilding(group, placed, pos, width, depth, height, materials);
            placed++;
        }

        return buildings;
    }

    static MalevolentShrineDestructibleBuilding BuildOneBuilding(
        Transform parent,
        int index,
        Vector3 pos,
        float width,
        float depth,
        float height,
        MalevolentShrineMaterials materials)
    {
        Transform building = New("Building" + index, parent, pos);
        building.localRotation = Quaternion.Euler(0f, index * 17f, 0f);
        Transform solid = CreateBox("Solid", building, new Vector3(0f, height * 0.5f, 0f), Vector3.zero, new Vector3(width, height, depth), materials.building);
        Transform slices = New("Slices", building, Vector3.zero);
        int nx = 2;
        int ny = Mathf.Clamp(Mathf.RoundToInt(height / 1.8f), 4, 6);
        int nz = 2;
        float sx = width / nx;
        float sy = height / ny;
        float sz = depth / nz;
        for (int y = 0; y < ny; y++)
        {
            for (int x = 0; x < nx; x++)
            {
                for (int z = 0; z < nz; z++)
                {
                    Vector3 local = new Vector3(
                        (x + 0.5f) * sx - width * 0.5f,
                        (y + 0.5f) * sy,
                        (z + 0.5f) * sz - depth * 0.5f);
                    Transform piece = CreateBox("P", slices, local, Vector3.zero, new Vector3(sx * 0.96f, sy * 0.94f, sz * 0.96f), materials.slice);
                    if (piece.GetComponent<BoxCollider>() == null)
                        piece.gameObject.AddComponent<BoxCollider>();
                    MalevolentShrineSlicePiece rest = piece.gameObject.AddComponent<MalevolentShrineSlicePiece>();
                    rest.restLocalPosition = piece.localPosition;
                    rest.restLocalRotation = piece.localRotation;
                }
            }
        }

        MalevolentShrineDestructibleBuilding actor = building.gameObject.AddComponent<MalevolentShrineDestructibleBuilding>();
        actor.Prepare(solid, slices);
        return actor;
    }

    static MalevolentShrineHachiTarget[] BuildTargets(
        Transform root,
        MalevolentShrinePreviewSettings settings,
        MalevolentShrineMaterials materials)
    {
        Transform group = New("HachiTargets", root, Vector3.zero);
        MalevolentShrineHachiTarget[] targets = new MalevolentShrineHachiTarget[settings.targetCount];
        int placed = 0;
        int guard = 0;
        while (placed < settings.targetCount && guard < 24)
        {
            guard++;
            float a = (placed * 2.15f + 1.1f + guard * 0.37f);
            if (IsInCameraCorridor(a))
                continue;
            float r = 7.5f + placed * 1.4f;
            Transform body = CreateCapsule("Target" + placed, group, new Vector3(Mathf.Cos(a) * r, 1f, Mathf.Sin(a) * r), new Vector3(0.7f, 1f, 0.7f), materials.flesh);
            MalevolentShrineHachiTarget target = body.gameObject.AddComponent<MalevolentShrineHachiTarget>();
            target.bodyRenderer = body.GetComponent<Renderer>();
            target.ResetState();
            targets[placed] = target;
            placed++;
        }

        return targets;
    }

    static void BuildLights(Transform root, MalevolentShrinePreviewSettings settings, MalevolentShrinePreviewRefs refs)
    {
        GameObject sunGo = new GameObject("Sun");
        sunGo.transform.SetParent(root, false);
        sunGo.transform.rotation = Quaternion.Euler(48f, -18f, 0f);
        Light sun = sunGo.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(0.78f, 0.72f, 0.66f);
        sun.intensity = 1.05f;
        sun.shadows = LightShadows.Soft;
        refs.sun = sun;

        GameObject shrineLightGo = new GameObject("ShrineLight");
        shrineLightGo.transform.SetParent(refs.shrine, false);
        shrineLightGo.transform.localPosition = new Vector3(0f, 3.4f, 0f);
        Light shrineLight = shrineLightGo.AddComponent<Light>();
        shrineLight.type = LightType.Point;
        shrineLight.color = new Color(1f, 0.14f, 0.08f);
        shrineLight.intensity = 0f;
        shrineLight.range = 10f;
        refs.shrineLight = shrineLight;

        GameObject washGo = new GameObject("DomainWash");
        washGo.transform.SetParent(root, false);
        washGo.transform.localPosition = new Vector3(0f, 7.5f, 0f);
        Light wash = washGo.AddComponent<Light>();
        wash.type = LightType.Point;
        wash.color = new Color(1f, 0.12f, 0.06f);
        wash.intensity = 0f;
        wash.range = settings.radius * 0.35f;
        refs.domainWash = wash;
    }

    static void BuildCamera(Transform root, MalevolentShrinePreviewRefs refs)
    {
        GameObject cameraGo = new GameObject("PreviewCamera");
        cameraGo.transform.SetParent(root, false);
        cameraGo.transform.position = new Vector3(6.8f, 3.9f, 11.8f);
        cameraGo.transform.rotation = Quaternion.LookRotation(new Vector3(0.15f, 3.2f, 2.8f) - cameraGo.transform.position);
        Camera camera = cameraGo.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.07f, 0.05f, 0.045f);
        camera.fieldOfView = 36f;
        camera.nearClipPlane = 0.2f;
        camera.farClipPlane = 180f;
        camera.tag = "MainCamera";
        cameraGo.AddComponent<AudioListener>();
        refs.previewCamera = camera;
    }

    static ParticleSystem BuildDust(Transform parent, Material material, Vector3 localPos, float radius)
    {
        GameObject go = new GameObject("Dust");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 1.2f;
        main.startLifetime = 1.6f;
        main.startSpeed = 1.4f;
        main.startSize = 0.35f;
        main.startColor = material != null && material.HasProperty("_TintColor")
            ? material.GetColor("_TintColor")
            : new Color(0.3f, 0.22f, 0.18f, 0.5f);
        main.maxParticles = 80;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 28) });
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius;
        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        return ps;
    }

    static ParticleSystem BuildLoopSmoke(
        Transform parent,
        Material material,
        Vector3 localPos,
        float radius,
        float rate,
        float size,
        float rise)
    {
        GameObject go = new GameObject("Smoke");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.duration = 4f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.4f, 4.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.12f, rise);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.7f, size);
        main.startColor = new Color(0.22f, 0.08f, 0.05f, 0.32f);
        main.maxParticles = 160;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.02f;
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = rate;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = Mathf.Max(0.4f, radius);
        ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
        color.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(new Color(0.28f, 0.08f, 0.05f), 0f), new GradientColorKey(new Color(0.12f, 0.05f, 0.04f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.45f, 0.25f), new GradientAlphaKey(0.2f, 0.75f), new GradientAlphaKey(0f, 1f) });
        color.color = gradient;
        ParticleSystem.SizeOverLifetimeModule sizeLife = ps.sizeOverLifetime;
        sizeLife.enabled = true;
        sizeLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 1.4f));
        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return ps;
    }

    static ParticleSystem BuildAsh(Transform root, Material material, float radius)
    {
        GameObject go = new GameObject("Ash");
        go.transform.SetParent(root, false);
        go.transform.localPosition = new Vector3(0f, 3.5f, 0f);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.duration = 5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(3.5f, 6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.35f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.18f);
        main.startColor = new Color(0.18f, 0.1f, 0.08f, 0.55f);
        main.maxParticles = 140;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.04f;
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 22f;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(radius * 1.4f, 6f, radius * 1.4f);
        ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.15f, 0.2f);
        velocity.y = new ParticleSystem.MinMaxCurve(-0.08f, 0.12f);
        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return ps;
    }

    static ParticleSystem BuildSliceSmoke(Transform root, Material material)
    {
        GameObject go = new GameObject("SliceSmoke");
        go.transform.SetParent(root, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 0.4f;
        main.startLifetime = 1.4f;
        main.startSpeed = 1.1f;
        main.startSize = 1.3f;
        main.startColor = new Color(0.24f, 0.1f, 0.07f, 0.45f);
        main.maxParticles = 40;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 16) });
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = 1.1f;
        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        return ps;
    }

    static bool IsInCameraCorridor(float angleRadians)
    {
        const float CameraAzimuthDegrees = 30f;
        const float HalfWidthDegrees = 52f;
        float buildingDegrees = angleRadians * Mathf.Rad2Deg;
        return Mathf.Abs(Mathf.DeltaAngle(buildingDegrees, CameraAzimuthDegrees)) < HalfWidthDegrees;
    }

    static Transform New(string name, Transform parent, Vector3 localPos)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        return go.transform;
    }

    static Transform CreateBox(string name, Transform parent, Vector3 pos, Vector3 euler, Vector3 scale, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = scale;
        DestroyCollider(go);
        go.GetComponent<Renderer>().sharedMaterial = material;
        return go.transform;
    }

    static Transform CreateSphere(string name, Transform parent, Vector3 pos, Vector3 scale, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        DestroyCollider(go);
        go.GetComponent<Renderer>().sharedMaterial = material;
        return go.transform;
    }

    static Transform CreateCapsule(string name, Transform parent, Vector3 pos, Vector3 scale, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        DestroyCollider(go);
        go.GetComponent<Renderer>().sharedMaterial = material;
        return go.transform;
    }

    static Transform CreateCylinder(string name, Transform parent, Vector3 pos, Vector3 scale, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        DestroyCollider(go);
        go.GetComponent<Renderer>().sharedMaterial = material;
        return go.transform;
    }

    static void CreateCone(string name, Transform parent, Vector3 pos, Vector3 euler, Vector3 scale, Material material)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = Vector3.one;
        MeshFilter filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = MalevolentShrineMeshFactory.CreateCone(10, 0.5f, 1f);
        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        go.transform.localScale = scale;
    }

    static void DestroyCollider(GameObject go)
    {
        Collider collider = go.GetComponent<Collider>();
        if (collider == null)
            return;
        if (Application.isPlaying)
            Object.Destroy(collider);
        else
            Object.DestroyImmediate(collider);
    }
}
