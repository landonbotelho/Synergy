using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class SimpleSwordCombatController : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private Animator animator;

    [Header("Base Layer State Names")]
    [SerializeField]
    private string locomotionStateName = "Base Layer.Locomotion.LocomotionBlendTree";

    [SerializeField]
    private string lightAttackStateName = "Base Layer.A_Attack_LightCombo01A_Sword";

    [SerializeField]
    private string blockBeginStateName = "Base Layer.A_Block_Begin_Sword";

    [SerializeField]
    private string blockLoopStateName = "Base Layer.A_Block_Loop_Sword";

    [SerializeField]
    private string blockEndStateName = "Base Layer.A_Block_End_Sword";

    [Header("Combat Clips")]
    [SerializeField]
    private AnimationClip lightAttackClip;

    [SerializeField]
    private AnimationClip blockBeginClip;

    [SerializeField]
    private AnimationClip blockEndClip;

    [Header("Layer Weights")]
    [SerializeField]
    private string swordArmLayerName = "SwordArm";

    [SerializeField]
    private string swordHandLayerName = "SwordHand";

    [SerializeField]
    [Range(0f, 1f)]
    private float swordArmLocomotionWeight = 0.7f;

    [SerializeField]
    [Range(0f, 1f)]
    private float swordHandLocomotionWeight = 1f;

    [SerializeField]
    private float transitionDuration = 0.08f;

    [Header("Attack Timing")]
    [SerializeField]
    [Range(0.1f, 1.25f)]
    private float attackExitTimeMultiplier = 0.9f;

    private Coroutine _activeRoutine;
    private int _swordArmLayerIndex = -1;
    private int _swordHandLayerIndex = -1;
    private bool _isBlocking;
    private bool _isBusy;

    public bool IsBlocking => _isBlocking;

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (animator == null)
        {
            Debug.LogError("SimpleSwordCombatController requires an Animator reference.");
            enabled = false;
            return;
        }

        _swordArmLayerIndex = animator.GetLayerIndex(swordArmLayerName);
        _swordHandLayerIndex = animator.GetLayerIndex(swordHandLayerName);

        RestoreLocomotionSwordLayers();
    }

    private void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return;
        }

        if (!_isBusy && !_isBlocking && mouse.leftButton.wasPressedThisFrame)
        {
            StartRoutine(DoLightAttack());
            return;
        }

        if (!_isBusy && !_isBlocking && mouse.rightButton.wasPressedThisFrame)
        {
            StartRoutine(BeginBlock());
            return;
        }

        if (_isBlocking && mouse.rightButton.wasReleasedThisFrame)
        {
            StartRoutine(EndBlock());
        }
    }

    private void StartRoutine(IEnumerator routine)
    {
        if (_activeRoutine != null)
        {
            StopCoroutine(_activeRoutine);
            RestoreLocomotionSwordLayers();
            _isBusy = false;
            _isBlocking = false;
        }

        _activeRoutine = StartCoroutine(routine);
    }

    private IEnumerator DoLightAttack()
    {
        _isBusy = true;
        SetCombatSwordLayersActive(false);

        animator.CrossFadeInFixedTime(lightAttackStateName, transitionDuration, 0);
        GameAudioManager.PlaySwordSwing(transform.position);

        float waitTime = GetClipLength(lightAttackClip, 0.6f) * attackExitTimeMultiplier;
        yield return new WaitForSeconds(waitTime);

        ForceReturnToLocomotion();

        RestoreLocomotionSwordLayers();
        _isBusy = false;
        _activeRoutine = null;
    }

    private IEnumerator BeginBlock()
    {
        _isBusy = true;
        _isBlocking = true;
        SetCombatSwordLayersActive(false);

        animator.CrossFadeInFixedTime(blockBeginStateName, transitionDuration, 0);
        yield return new WaitForSeconds(GetClipLength(blockBeginClip, 0.25f));

        if (_isBlocking)
        {
            animator.CrossFadeInFixedTime(blockLoopStateName, transitionDuration, 0);
        }

        _isBusy = false;
        _activeRoutine = null;
    }

    private IEnumerator EndBlock()
    {
        _isBusy = true;
        _isBlocking = false;

        animator.CrossFadeInFixedTime(blockEndStateName, transitionDuration, 0);
        yield return new WaitForSeconds(GetClipLength(blockEndClip, 0.25f));

        ForceReturnToLocomotion();
        RestoreLocomotionSwordLayers();

        _isBusy = false;
        _activeRoutine = null;
    }

    private void ForceReturnToLocomotion()
    {
        animator.Play(locomotionStateName, 0, 0f);
        animator.Update(0f);
    }

    private void SetCombatSwordLayersActive(bool active)
    {
        if (_swordArmLayerIndex >= 0)
        {
            animator.SetLayerWeight(_swordArmLayerIndex, active ? swordArmLocomotionWeight : 0f);
        }

        if (_swordHandLayerIndex >= 0)
        {
            animator.SetLayerWeight(_swordHandLayerIndex, active ? swordHandLocomotionWeight : 0f);
        }
    }

    private void RestoreLocomotionSwordLayers()
    {
        SetCombatSwordLayersActive(true);
    }

    private static float GetClipLength(AnimationClip clip, float fallback)
    {
        return clip != null && clip.length > 0f ? clip.length : fallback;
    }
}
