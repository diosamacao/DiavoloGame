using UnityEngine;

/// <summary>
/// 伏魔御厨子 6.8 秒技能预览导演。
/// 不接入 ActionSim / 伤害 / 联网，只播开放领域演出。
/// </summary>
[DisallowMultipleComponent]
public sealed class MalevolentShrinePreviewDirector : MonoBehaviour
{
    const int SlashPoolSize = 48;

    MalevolentShrinePreviewSettings _settings;
    MalevolentShrinePreviewRefs _refs;
    SlashSlot[] _slashes;
    float _time;
    bool _playing;
    bool _shrineDustPlayed;
    bool _cameraSnapped;
    Vector3 _cameraVelocity;
    Vector3 _lookVelocity;
    Vector3 _smoothedLook;
    float _fovVelocity;
    float _kaiTimer;
    float _hachiTimer;
    int _nextBuilding;
    GUIStyle _titleStyle;
    GUIStyle _subStyle;
    GUIStyle _hudStyle;
    Color _fogRest;
    float _fogDensityRest;
    bool _fogRestCaptured;

    public float PlaybackTime => _time;
    public bool IsPlaying => _playing;
    public string PhaseName => ResolvePhaseName(_time);

    public void Bind(MalevolentShrinePreviewRefs refs, MalevolentShrinePreviewSettings settings)
    {
        _refs = refs;
        _settings = settings ?? MalevolentShrinePreviewSettings.CreateDefault();
        _slashes = null;
        EnsureSlashPool();
        CaptureFogRest();
    }

    public void Restart()
    {
        if (_refs == null)
            return;

        _time = 0f;
        _playing = true;
        _shrineDustPlayed = false;
        _cameraSnapped = false;
        _cameraVelocity = Vector3.zero;
        _lookVelocity = Vector3.zero;
        _fovVelocity = 0f;
        _kaiTimer = 0f;
        _hachiTimer = 0f;
        _nextBuilding = 0;
        ResetWorld();
        Evaluate(0f);
    }

    public void Stop()
    {
        _playing = false;
    }

    void Update()
    {
        if (_refs == null || _settings == null)
            return;

        if (Input.GetKeyDown(KeyCode.Space))
            Restart();

        if (!_playing)
            return;

        _time += UnityEngine.Time.deltaTime * Mathf.Max(0.01f, _settings.timeScale);
        if (_time >= _settings.duration)
        {
            if (_settings.loop)
            {
                Restart();
                return;
            }

            _time = _settings.duration;
            _playing = false;
        }

        Evaluate(_time);
    }

    void LateUpdate()
    {
        if (_refs == null || _settings == null)
            return;
        UpdateCamera(_time, !_cameraSnapped);
        _cameraSnapped = true;
    }

    void Evaluate(float t)
    {
        UpdateShrine(t);
        UpdateField(t);
        UpdateLighting(t);
        UpdateKai(t);
        UpdateHachi(t);
        UpdateSlashes();
        UpdateDestruction(t);
    }

    void ResetWorld()
    {
        if (_refs.buildings != null)
        {
            for (int i = 0; i < _refs.buildings.Length; i++)
            {
                if (_refs.buildings[i] != null)
                    _refs.buildings[i].ResetState();
            }
        }

        if (_refs.targets != null)
        {
            for (int i = 0; i < _refs.targets.Length; i++)
            {
                if (_refs.targets[i] != null)
                    _refs.targets[i].ResetState();
            }
        }

        if (_slashes != null)
        {
            for (int i = 0; i < _slashes.Length; i++)
                _slashes[i].Hide();
        }

        if (_refs.ring != null)
            _refs.ring.localScale = Vector3.zero;
        if (_refs.groundGlow != null)
            _refs.groundGlow.localScale = Vector3.zero;
        if (_refs.shrine != null)
            _refs.shrine.localPosition = new Vector3(0f, -_settings.shrineHeight - 1.5f, 0f);
        if (_refs.handEnergy != null)
            _refs.handEnergy.Play(true);
        StopLooping(_refs.fieldSmoke);
        StopLooping(_refs.ambientAsh);
        StopLooping(_refs.shrineSmoke);
        RestoreFog();
        if (_refs.previewCamera != null)
        {
            GetCameraPose(0f, out Vector3 pos, out Vector3 look, out float fov);
            _refs.previewCamera.transform.position = pos;
            _refs.previewCamera.transform.rotation = Quaternion.LookRotation(look - pos);
            _refs.previewCamera.fieldOfView = fov;
        }
    }

    void UpdateShrine(float t)
    {
        if (_refs.shrine == null)
            return;

        float rise = Smooth01(t, _settings.titleEnd, _settings.shrineRiseEnd);
        float settle = 1f - Smooth01(t, _settings.stormEnd, _settings.duration);
        float y = Mathf.Lerp(-_settings.shrineHeight - 1.5f, 0f, rise);
        _refs.shrine.localPosition = new Vector3(0f, y, 0f);
        _refs.shrine.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, rise) * Mathf.Lerp(0.15f, 1f, settle);

        float jaw = rise * (0.55f + 0.45f * (0.5f + 0.5f * Mathf.Sin(t * 3.1f)));
        if (_refs.upperJaws != null)
        {
            for (int i = 0; i < _refs.upperJaws.Length; i++)
            {
                if (_refs.upperJaws[i] != null)
                    _refs.upperJaws[i].localRotation = Quaternion.Euler(-12f - jaw * 14f, 0f, 0f);
                if (_refs.lowerJaws != null && _refs.lowerJaws[i] != null)
                    _refs.lowerJaws[i].localRotation = Quaternion.Euler(16f + jaw * 12f, 0f, 0f);
            }
        }

        if (!_shrineDustPlayed && t >= _settings.titleEnd + 0.05f)
        {
            _shrineDustPlayed = true;
            if (_refs.shrineDust != null)
                _refs.shrineDust.Play(true);
        }

        if (_refs.handEnergy != null)
        {
            if (t < _settings.titleEnd && !_refs.handEnergy.isPlaying)
                _refs.handEnergy.Play();
            else if (t >= _settings.titleEnd && _refs.handEnergy.isEmitting)
                _refs.handEnergy.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    void UpdateField(float t)
    {
        float paint = Smooth01(t, _settings.shrineRiseEnd, _settings.radiusPaintEnd);
        float hold = 1f - Smooth01(t, _settings.stormEnd, _settings.duration);
        float scale = _settings.radius * paint * hold;
        if (_refs.ring != null)
            _refs.ring.localScale = new Vector3(scale, 1f, scale);
        if (_refs.groundGlow != null)
            _refs.groundGlow.localScale = new Vector3(scale * 0.96f, 1f, scale * 0.96f);
        if (_refs.materials != null && _refs.materials.ring != null && _refs.materials.ring.HasProperty("_Pulse"))
            _refs.materials.ring.SetFloat("_Pulse", t >= _settings.radiusPaintEnd && t < _settings.stormEnd ? 1f : 0f);

        SetLooping(_refs.shrineSmoke, t >= _settings.titleEnd && t < _settings.stormEnd);
        SetLooping(_refs.fieldSmoke, paint > 0.12f && hold > 0.08f);
        SetLooping(_refs.ambientAsh, paint > 0.2f && hold > 0.08f);
    }

    void UpdateLighting(float t)
    {
        float domain = Smooth01(t, _settings.mudraEnd, _settings.radiusPaintEnd);
        float release = 1f - Smooth01(t, _settings.stormEnd, _settings.duration);
        float weight = domain * release;

        if (_refs.sun != null)
        {
            _refs.sun.intensity = Mathf.Lerp(1.05f, 0.82f, weight);
            _refs.sun.color = Color.Lerp(new Color(0.78f, 0.72f, 0.66f), new Color(0.92f, 0.16f, 0.08f), weight);
        }

        if (_refs.shrineLight != null)
        {
            _refs.shrineLight.intensity = Mathf.Lerp(0f, 5.4f, weight);
            _refs.shrineLight.range = Mathf.Lerp(8f, 14f, weight);
        }

        if (_refs.domainWash != null)
        {
            float paint = Smooth01(t, _settings.shrineRiseEnd, _settings.radiusPaintEnd) * release;
            _refs.domainWash.intensity = Mathf.Lerp(0f, 7.5f, paint);
            _refs.domainWash.range = Mathf.Lerp(_settings.radius * 0.25f, _settings.radius * 1.15f, paint);
            _refs.domainWash.color = new Color(1f, 0.1f, 0.05f);
        }

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = Color.Lerp(_fogRest, new Color(0.22f, 0.04f, 0.03f), weight);
        RenderSettings.fogDensity = Mathf.Lerp(Mathf.Max(0.006f, _fogDensityRest), 0.011f, weight);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = Color.Lerp(new Color(0.16f, 0.13f, 0.12f), new Color(0.28f, 0.04f, 0.03f), weight);
        if (_refs.previewCamera != null)
            _refs.previewCamera.backgroundColor = Color.Lerp(new Color(0.08f, 0.07f, 0.06f), new Color(0.12f, 0.03f, 0.02f), weight);
    }

    void UpdateKai(float t)
    {
        if (t < _settings.radiusPaintEnd || t > _settings.stormEnd)
            return;

        _kaiTimer += UnityEngine.Time.deltaTime * _settings.timeScale;
        if (_kaiTimer < _settings.kaiInterval)
            return;
        _kaiTimer = 0f;

        int count = t < _settings.firstKaiEnd ? 1 : _settings.kaiPerPulse;
        for (int i = 0; i < count; i++)
            SpawnKai();
    }

    void SpawnKai()
    {
        if (_refs.buildings == null || _refs.buildings.Length == 0)
            return;

        MalevolentShrineDestructibleBuilding building = null;
        for (int n = 0; n < _refs.buildings.Length; n++)
        {
            MalevolentShrineDestructibleBuilding candidate = _refs.buildings[(_nextBuilding + n) % _refs.buildings.Length];
            if (candidate != null && !candidate.sliced)
            {
                building = candidate;
                _nextBuilding = (_nextBuilding + n + 1) % _refs.buildings.Length;
                break;
            }
        }

        if (building == null)
            building = _refs.buildings[_nextBuilding++ % _refs.buildings.Length];
        if (building == null)
            return;

        Vector3 center = building.transform.position + Vector3.up * 3.2f;
        if (_settings.groundOnly)
            center.y = Mathf.Min(center.y, 4.5f);

        Vector3 dir = Random.onUnitSphere;
        dir.y *= 0.25f;
        dir.Normalize();
        Vector3 a = center - dir * 7f;
        Vector3 b = center + dir * 7f;
        SpawnSlash(a, b, _refs.materials != null ? _refs.materials.slashKai : null, _settings.kaiSlashWidth);

        if (!building.sliced)
        {
            building.Slice(center, Vector3.Cross(dir, Vector3.up).normalized, _time);
            PlaySliceSmoke(center);
        }
    }

    void UpdateHachi(float t)
    {
        if (_refs.targets == null)
            return;

        for (int i = 0; i < _refs.targets.Length; i++)
        {
            if (_refs.targets[i] != null)
                _refs.targets[i].Tick(UnityEngine.Time.deltaTime);
        }

        if (t < _settings.firstKaiEnd || t > _settings.stormEnd)
            return;

        _hachiTimer += UnityEngine.Time.deltaTime * _settings.timeScale;
        if (_hachiTimer < _settings.hachiInterval)
            return;
        _hachiTimer = 0f;

        for (int i = 0; i < _refs.targets.Length; i++)
        {
            MalevolentShrineHachiTarget target = _refs.targets[i];
            if (target == null)
                continue;
            Vector3 center = target.transform.position;
            for (int n = 0; n < _settings.hachiPerTarget; n++)
            {
                Vector3 dir = Random.onUnitSphere;
                SpawnSlash(center - dir * 2.2f, center + dir * 2.2f, _refs.materials != null ? _refs.materials.slashHachi : null, _settings.hachiSlashWidth);
            }

            target.Hit();
        }
    }

    void UpdateDestruction(float t)
    {
        if (_refs.buildings == null)
            return;

        for (int i = 0; i < _refs.buildings.Length; i++)
        {
            MalevolentShrineDestructibleBuilding building = _refs.buildings[i];
            if (building == null || !building.sliced)
                continue;
            if (!building.collapsed && t >= building.sliceTime + 0.1f)
                building.Collapse();
            if (building.collapsed && !building.faded && t >= building.sliceTime + 1.55f)
            {
                float fade = 1f - Smooth01(t, building.sliceTime + 1.55f, building.sliceTime + 2.3f);
                building.Fade(fade);
            }
        }
    }

    void UpdateCamera(float t, bool snap)
    {
        if (_refs.previewCamera == null)
            return;

        GetCameraPose(t, out Vector3 pos, out Vector3 look, out float fov);
        Transform cam = _refs.previewCamera.transform;
        if (snap)
        {
            cam.position = pos;
            _smoothedLook = look;
            cam.rotation = Quaternion.LookRotation(look - pos);
            _refs.previewCamera.fieldOfView = fov;
            _cameraVelocity = Vector3.zero;
            _lookVelocity = Vector3.zero;
            _fovVelocity = 0f;
            return;
        }

        cam.position = Vector3.SmoothDamp(cam.position, pos, ref _cameraVelocity, 0.48f, 18f);
        _smoothedLook = Vector3.SmoothDamp(_smoothedLook, look, ref _lookVelocity, 0.42f, 22f);
        Vector3 toLook = _smoothedLook - cam.position;
        if (toLook.sqrMagnitude > 0.001f)
            cam.rotation = Quaternion.Slerp(cam.rotation, Quaternion.LookRotation(toLook), 1f - Mathf.Exp(-3.4f * UnityEngine.Time.deltaTime));
        _refs.previewCamera.fieldOfView = Mathf.SmoothDamp(_refs.previewCamera.fieldOfView, fov, ref _fovVelocity, 0.5f);
    }

    void GetCameraPose(float t, out Vector3 pos, out Vector3 look, out float fov)
    {
        Vector3 shrine = new Vector3(0f, 4.6f, 0f);
        Vector3 caster = _refs.caster != null ? _refs.caster.position + new Vector3(0f, 1.55f, 0.15f) : new Vector3(0f, 1.55f, 6.2f);
        Vector3 twoShot = Vector3.Lerp(caster, shrine, 0.55f);

        if (t < _settings.mudraEnd)
        {
            pos = caster + new Vector3(0.7f, 0.28f, 1.35f);
            look = Vector3.Lerp(caster, twoShot, 0.35f);
            fov = 34f;
            return;
        }

        if (t < _settings.shrineRiseEnd)
        {
            float u = Smooth01(t, _settings.mudraEnd, _settings.shrineRiseEnd);
            pos = Vector3.Lerp(caster + new Vector3(0.85f, 0.55f, 1.6f), new Vector3(11.2f, 8.2f, 13.4f), u);
            look = Vector3.Lerp(caster, twoShot, Mathf.Lerp(0.35f, 1f, u));
            fov = Mathf.Lerp(34f, 42f, u);
            return;
        }

        if (t < _settings.firstKaiEnd)
        {
            float u = Smooth01(t, _settings.shrineRiseEnd, _settings.firstKaiEnd);
            pos = Vector3.Lerp(new Vector3(11.2f, 8.2f, 13.4f), new Vector3(16.4f, 10.6f, 5.2f), u);
            look = Vector3.Lerp(twoShot, Vector3.Lerp(twoShot, shrine, 0.35f), u);
            fov = Mathf.Lerp(42f, 44f, u);
            return;
        }

        if (t < _settings.stormEnd)
        {
            float u = Smooth01(t, _settings.firstKaiEnd, _settings.stormEnd);
            float ang = 0.48f + u * 1.15f;
            pos = new Vector3(Mathf.Sin(ang) * 17.5f, 11.2f, Mathf.Cos(ang) * 17.5f);
            look = twoShot;
            fov = 45f;
            return;
        }

        float release = Smooth01(t, _settings.stormEnd, _settings.duration);
        pos = Vector3.Lerp(new Vector3(14.2f, 9.4f, 12.6f), new Vector3(8.4f, 6.2f, 10.8f), release);
        look = twoShot;
        fov = Mathf.Lerp(43f, 38f, release);
    }

    void EnsureSlashPool()
    {
        if (_slashes != null || _refs == null || _refs.root == null)
            return;

        Transform pool = new GameObject("SlashPool").transform;
        pool.SetParent(_refs.root, false);
        _slashes = new SlashSlot[SlashPoolSize];
        Mesh mesh = _refs.slashMesh != null ? _refs.slashMesh : MalevolentShrineMeshFactory.CreateSlashCard();
        for (int i = 0; i < SlashPoolSize; i++)
        {
            GameObject go = new GameObject("Slash" + i);
            go.transform.SetParent(pool, false);
            MeshFilter filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.SetActive(false);
            _slashes[i] = new SlashSlot { transform = go.transform, renderer = renderer };
        }
    }

    void SpawnSlash(Vector3 a, Vector3 b, Material material, float width)
    {
        if (_slashes == null)
            return;

        SlashSlot slot = null;
        for (int i = 0; i < _slashes.Length; i++)
        {
            if (!_slashes[i].alive)
            {
                slot = _slashes[i];
                break;
            }
        }

        if (slot == null)
            return;

        Vector3 mid = (a + b) * 0.5f;
        Vector3 along = (b - a);
        float length = Mathf.Max(0.35f, along.magnitude);
        along.Normalize();
        Vector3 toCam = _refs.previewCamera != null
            ? _refs.previewCamera.transform.position - mid
            : Vector3.up;
        if (toCam.sqrMagnitude < 0.001f)
            toCam = Vector3.up;
        Vector3 up = Vector3.Cross(toCam, along);
        if (up.sqrMagnitude < 0.001f)
            up = Vector3.up;
        slot.transform.gameObject.SetActive(true);
        slot.transform.position = mid;
        slot.transform.rotation = Quaternion.LookRotation(toCam.normalized, up.normalized);
        slot.transform.localScale = new Vector3(length, width, 1f);
        if (slot.renderer != null)
            slot.renderer.sharedMaterial = material;
        slot.alive = true;
        slot.dieAt = _time + _settings.slashLife;
    }

    void UpdateSlashes()
    {
        if (_slashes == null)
            return;
        for (int i = 0; i < _slashes.Length; i++)
        {
            if (_slashes[i].alive && _time >= _slashes[i].dieAt)
                _slashes[i].Hide();
        }
    }

    void OnGUI()
    {
        EnsureGuiStyles();
        if (_settings != null && _settings.showTitleCard && _time >= _settings.mudraEnd && _time <= _settings.shrineRiseEnd)
        {
            float alpha = 1f - Smooth01(_time, _settings.titleEnd + 0.35f, _settings.shrineRiseEnd);
            Color title = new Color(0.86f, 0.8f, 0.74f, alpha);
            _titleStyle.normal.textColor = title;
            _subStyle.normal.textColor = new Color(0.72f, 0.28f, 0.22f, alpha);
            GUI.Label(new Rect(0f, Screen.height * 0.38f, Screen.width, 48f), "领域展开", _titleStyle);
            GUI.Label(new Rect(0f, Screen.height * 0.38f + 48f, Screen.width, 40f), "伏魔御厨子", _subStyle);
        }

        _hudStyle.normal.textColor = new Color(0.8f, 0.76f, 0.7f, 0.75f);
        GUI.Label(new Rect(16f, 12f, 520f, 22f), "伏魔御厨子 预览  " + PhaseName + "  " + _time.ToString("0.00") + "s", _hudStyle);
        GUI.Label(new Rect(16f, 32f, 520f, 20f), "空格重播 · 不接入战斗伤害 / 联网", _hudStyle);
    }

    void EnsureGuiStyles()
    {
        if (_titleStyle != null)
            return;
        Font font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 36);
        _titleStyle = new GUIStyle { alignment = TextAnchor.UpperCenter, fontSize = 36, font = font, fontStyle = FontStyle.Bold };
        _subStyle = new GUIStyle { alignment = TextAnchor.UpperCenter, fontSize = 28, font = font };
        _hudStyle = new GUIStyle { alignment = TextAnchor.UpperLeft, fontSize = 13, font = font };
    }

    void CaptureFogRest()
    {
        if (_fogRestCaptured)
            return;
        _fogRest = RenderSettings.fogColor;
        _fogDensityRest = RenderSettings.fogDensity;
        _fogRestCaptured = true;
    }

    void RestoreFog()
    {
        if (!_fogRestCaptured)
            return;
        RenderSettings.fogColor = _fogRest;
        RenderSettings.fogDensity = _fogDensityRest;
    }

    static string ResolvePhaseName(float t)
    {
        if (t < 0.45f)
            return "阎魔天印";
        if (t < 0.8f)
            return "领域展开";
        if (t < 1.7f)
            return "御厨子升起";
        if (t < 2.2f)
            return "开放半径";
        if (t < 3.4f)
            return "解 · 切城";
        if (t < 5.5f)
            return "捌 · 必中";
        return "解除";
    }

    void PlaySliceSmoke(Vector3 position)
    {
        if (_refs.sliceSmoke == null)
            return;
        _refs.sliceSmoke.transform.position = position;
        _refs.sliceSmoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        _refs.sliceSmoke.Play(true);
    }

    static void SetLooping(ParticleSystem ps, bool on)
    {
        if (ps == null)
            return;
        if (on && !ps.isPlaying)
            ps.Play(true);
        else if (!on && ps.isPlaying)
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    static void StopLooping(ParticleSystem ps)
    {
        if (ps == null)
            return;
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    static float Smooth01(float t, float a, float b)
    {
        if (b <= a)
            return t >= b ? 1f : 0f;
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, t));
    }

    sealed class SlashSlot
    {
        public Transform transform;
        public MeshRenderer renderer;
        public bool alive;
        public float dieAt;

        public void Hide()
        {
            alive = false;
            if (transform != null)
                transform.gameObject.SetActive(false);
        }
    }
}
