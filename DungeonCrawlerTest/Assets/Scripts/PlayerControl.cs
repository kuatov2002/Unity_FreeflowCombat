using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using StarterAssets;

public class PlayerControl : MonoBehaviour
{
    [Space]
    [Header("Components")]
    [SerializeField] private Animator anim;
    [SerializeField] private ThirdPersonController thirdPersonController;
   // [SerializeField] private GameControl gameControl;
 
    [Space]
    [Header("Combat")]
    [SerializeField] private Transform attackPos;
    [Tooltip("Offset Stoping Distance")][SerializeField] private float quickAttackDeltaDistance;
    [Tooltip("Offset Stoping Distance")][SerializeField] private float heavyAttackDeltaDistance;
    [SerializeField] private float knockbackForce = 10f; 
    [SerializeField] private float airknockbackForce = 10f; 
    [SerializeField] private float attackRange = 1f;
    [SerializeField] private float reachTime = 0.3f;
    [SerializeField] private LayerMask enemyLayer;
    bool isAttacking = false;

    [Space]
    [Header("Targeting")]
    [Tooltip("Half-angle (degrees) of forward cone where targets are considered 'in front'")]
    [SerializeField] private float enemyDetectAngle = 80f; // половина угла конуса (±80°)
    [Tooltip("Max distance to search targets if TargetDetectionControl not used")]
    [SerializeField] private float maxTargetSearchDistance = 10f;

    [Space]
    [Header("Hit Pause (Animator)")]
    [Tooltip("На сколько секунд при попадании аниматор будет на паузе (используется WaitForSecondsRealtime).")]
    [SerializeField] private float hitPauseDuration = 0.08f;

    [Space]
    [Header("Debug")]
    [SerializeField] private bool debug;

    private EnemyBase oldTarget;
    private EnemyBase currentTarget;

    // coroutine reference to avoid overlapping pauses
    private Coroutine animatorPauseCoroutine = null;


    private float _forwardDistance;
    private float _moveDuration;
    void Start()
    {
    }

    void Update()
    {
        HandleInput();
    }

    void HandleInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Attack(0);
        }

        if (Input.GetMouseButtonDown(1))
        {
            Attack(1);
        }

        if (Input.GetKeyDown(KeyCode.J))
        {
            Attack(0);
        }

        if (Input.GetKeyDown(KeyCode.K))
        {
            Attack(1);
        }
    }

    #region Attack, PerformAttack, Reset Attack, Change Target

    public void Attack(int attackState)
    {
        if (isAttacking)
        {
            return;
        }
        
        thirdPersonController.canMove = false;
        RandomAttackAnim(attackState);
    }

    private void RandomAttackAnim(int attackState)
    {
        switch (attackState) 
        {
            case 0: //Quick Attack
                QuickAttack();
                break;

            case 1:
                HeavyAttack();
                break;
        }
    }

    void QuickAttack()
    {
        int attackIndex = Random.Range(1, 4);
        if (debug)
        {
            Debug.Log(attackIndex + " attack index");
        }

        // fallbackPoint — вперёд от игрока
        Vector3 fallbackPoint = transform.position + transform.forward * (quickAttackDeltaDistance + 0.5f);

        switch (attackIndex)
        {
            case 1: //punch
            {
                Vector3 facePoint = fallbackPoint;
                MoveTowardsTarget(facePoint, quickAttackDeltaDistance, "punch");
                isAttacking = true;
            }
                break;

            case 2: //kick
            {
                Vector3 facePoint = fallbackPoint;
                MoveTowardsTarget(facePoint, quickAttackDeltaDistance, "kick");
                isAttacking = true;
            }
                break;

            case 3: //mmakick
            {
                Vector3 facePoint = fallbackPoint;
                MoveTowardsTarget(facePoint, quickAttackDeltaDistance, "mmakick");
                isAttacking = true;
            }
                break;
        }
    }

    void HeavyAttack()
    {
        int attackIndex = Random.Range(1, 3);
        if (debug)
        {
            Debug.Log(attackIndex + " attack index");
        }

        Vector3 fallbackPoint = transform.position + transform.forward * (heavyAttackDeltaDistance + 0.5f);
        Vector3 facePoint = fallbackPoint;

        switch (attackIndex)
        {
            case 1: //heavyAttack1
            {
                FaceThis(facePoint);
                anim.SetBool("heavyAttack1", true);
                isAttacking = true;
                _forwardDistance = 3f;
                _moveDuration = 0.25f;
            }
                break;

            case 2: //heavyAttack2
            {
                FaceThis(facePoint);
                anim.SetBool("heavyAttack2", true);
                isAttacking = true;
                _forwardDistance = 1.2f;
                _moveDuration = 0.2f;
            } 
                break;
        }
    }

    public void ResetAttack() // Animation Event ---- for Reset Attack
    {
        anim.SetBool("punch", false);
        anim.SetBool("kick", false);
        anim.SetBool("mmakick", false);
        anim.SetBool("heavyAttack1", false);
        anim.SetBool("heavyAttack2", false);
        thirdPersonController.canMove = true;
        isAttacking = false;
    }

    public void PerformAttack() // Animation Event ---- for Attacking Targets
    {
        Collider[] hitEnemies = Physics.OverlapSphere(attackPos.position, attackRange, enemyLayer);
        bool anyHit = false;

        foreach (Collider enemy in hitEnemies)
        {
            if (enemy == null) continue;
            Rigidbody enemyRb = enemy.GetComponent<Rigidbody>();
            EnemyBase enemyBase = enemy.GetComponent<EnemyBase>();
            if (enemyRb != null)
            {
                Vector3 knockbackDirection = enemy.transform.position - transform.position;
                // если хотим, чтобы вертикальная составляющая была фиксирована:
                knockbackDirection.y = 0f;
                Vector3 finalKnock = knockbackDirection.normalized * knockbackForce + Vector3.up * airknockbackForce;
                enemyRb.AddForce(finalKnock, ForceMode.Impulse);
                anyHit = true;
            }

            if (enemyBase != null)
            {
                enemyBase.SpawnHitVfx(enemyBase.transform.position);
                anyHit = true;
            }
        }

        // Если задели кого-то — делаем небольшую паузу в аниматоре, чтобы усилить эффект удара
        if (anyHit)
        {
            TryPauseAnimator();
        }
    }

    // Запускает/перезапускает корутину паузы аниматора
    private void TryPauseAnimator()
    {
        if (anim == null) return;

        if (animatorPauseCoroutine != null)
        {
            StopCoroutine(animatorPauseCoroutine);
        }
        animatorPauseCoroutine = StartCoroutine(AnimatorPauseRoutine());
    }

    private IEnumerator AnimatorPauseRoutine()
    {
        float prevSpeed = anim.speed;
        anim.speed = 0f;
        // используем реальное время, чтобы пауза работала даже при изменении Time.timeScale
        yield return new WaitForSecondsRealtime(hitPauseDuration);
        anim.speed = prevSpeed;
        animatorPauseCoroutine = null;
    }

    #endregion


    #region MoveTowards, Target Offset and FaceThis
    public void MoveTowardsTarget(Vector3 target_, float deltaDistance, string animationName_)
    {
        PerformAttackAnimation(animationName_);
        FaceThis(target_);
        Vector3 finalPos = TargetOffset(target_, deltaDistance);
        finalPos.y = 0;
        transform.DOMove(finalPos, reachTime);
    }

    // Замените старый метод GetClose() этим вариантом:
    public void GetClose() // Animation Event ---- for Moving Close to Target
    {
        Vector3 finalPos = transform.position + transform.forward * _forwardDistance;
        // Сохраняем текущую высоту (или можно принудительно установить 0)
        finalPos.y = transform.position.y;
        transform.DOMove(finalPos, _moveDuration);
    }

    void PerformAttackAnimation(string animationName_)
    {
        anim.SetBool(animationName_, true);
    }

    public Vector3 TargetOffset(Vector3 target, float deltaDistance)
    {
        Vector3 position = target;
        return Vector3.MoveTowards(position, transform.position, deltaDistance);
    }

    public void FaceThis(Vector3 target)
    {
        Vector3 target_ = new Vector3(target.x, target.y, target.z);
        Quaternion lookAtRotation = Quaternion.LookRotation(target_ - transform.position);
        lookAtRotation.x = 0;
        lookAtRotation.z = 0;
        transform.DOLocalRotateQuaternion(lookAtRotation, 0.2f);
    }
    #endregion

    void OnDrawGizmosSelected()
    {
        if (attackPos == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPos.position, attackRange); // Visualize the attack range

        // Отобразим конус обнаружения (приблизительно)
#if UNITY_EDITOR
        UnityEditor.Handles.color = new Color(0, 1, 0, 0.1f);
        UnityEditor.Handles.DrawSolidArc(transform.position, Vector3.up, transform.forward, enemyDetectAngle, maxTargetSearchDistance);
        UnityEditor.Handles.DrawSolidArc(transform.position, Vector3.up, transform.forward, -enemyDetectAngle, maxTargetSearchDistance);
#endif
    }
}
