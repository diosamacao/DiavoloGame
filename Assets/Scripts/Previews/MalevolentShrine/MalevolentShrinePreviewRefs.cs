using UnityEngine;

/// <summary>预览场景搭建完成后交给导演绑定的引用。</summary>
public sealed class MalevolentShrinePreviewRefs
{
    public Transform root;
    public Transform shrine;
    public Transform[] upperJaws;
    public Transform[] lowerJaws;
    public Transform ring;
    public Camera previewCamera;
    public Transform caster;
    public Transform leftHand;
    public Transform rightHand;
    public MalevolentShrineDestructibleBuilding[] buildings;
    public MalevolentShrineHachiTarget[] targets;
    public Light sun;
    public Light shrineLight;
    public Light domainWash;
    public Transform groundGlow;
    public ParticleSystem shrineDust;
    public ParticleSystem shrineSmoke;
    public ParticleSystem fieldSmoke;
    public ParticleSystem ambientAsh;
    public ParticleSystem sliceSmoke;
    public ParticleSystem handEnergy;
    public MalevolentShrineMaterials materials;
    public Mesh slashMesh;
}
