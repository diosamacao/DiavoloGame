using UnityEngine;

/// <summary>带咒力的假敌，领域中吃「捌」而不是环境用的「解」。</summary>
public sealed class MalevolentShrineHachiTarget : MonoBehaviour
{
    public Renderer bodyRenderer;
    public Color restColor = new Color(0.42f, 0.16f, 0.14f);
    public float flash;

    public void ResetState()
    {
        flash = 0f;
        ApplyColor();
    }

    public void Hit()
    {
        flash = 1f;
    }

    public void Tick(float deltaTime)
    {
        if (flash <= 0f)
            return;
        flash = Mathf.MoveTowards(flash, 0f, deltaTime * 3.5f);
        ApplyColor();
    }

    void ApplyColor()
    {
        if (bodyRenderer == null)
            return;
        Color color = Color.Lerp(restColor, new Color(0.85f, 0.22f, 0.16f), flash);
        Material mat = Application.isPlaying ? bodyRenderer.material : bodyRenderer.sharedMaterial;
        if (mat != null)
            mat.color = color;
    }
}
