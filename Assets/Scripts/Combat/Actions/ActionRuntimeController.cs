using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterAnimationController))]
public class ActionRuntimeController : MonoBehaviour, IActionRuntime
{
    [SerializeField] CharacterAnimationController animationController;
    [SerializeField] ActionDefinition defaultAttack;

    ActionDefinition _current;
    bool _isPlaying;
    float _elapsed;

    public bool IsPlaying => _isPlaying;
    public ActionDefinition CurrentAction => _current;
    public ActionDefinition DefaultAttack => defaultAttack;

    void Awake()
    {
        if (animationController == null)
            animationController = GetComponent<CharacterAnimationController>();
    }

    public bool TryStartDefaultAction() => TryPlay(defaultAttack);

    public bool TryPlay(ActionDefinition action)
    {
        if (_isPlaying || action == null || action.AnimationClip == null || animationController == null)
            return false;

        _current = action;
        _isPlaying = true;
        _elapsed = 0f;
        animationController.PlayClip(action.AnimationClip, action.CrossFadeDuration);
        return true;
    }

    public void Tick(float deltaTime)
    {
        if (!_isPlaying || _current == null)
            return;

        _elapsed += deltaTime;
        if (_elapsed >= _current.DurationSeconds)
            Stop();
    }

    public void Stop()
    {
        _isPlaying = false;
        _current = null;
        _elapsed = 0f;
    }
}
