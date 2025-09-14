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
    public Transform target;
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

    void Start()
    {
    }

    void Update()
    {
        HandleInput();
    }

    private void FixedUpdate()
    {
        if(target == null)
        {
            return;
        }

        if((Vector3.Distance(transform.position, target.position) >= TargetDetectionControl.instance.detectionRange))
        {
            NoTarget();
        }
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

        // Если текущая цель за спиной — попробуем выбрать нормальную цель впереди
        if (target != null && !IsInFront(target.position, enemyDetectAngle))
        {
            Transform newT = SelectBestTargetInFront(enemyDetectAngle);
            if (newT != null)
            {
                ChangeTarget(newT);
            }
            else
            {
                // оставляем target = null чтобы не бить по заднему врагу
                ChangeTarget(null);
            }
        }

        thirdPersonController.canMove = false;
        TargetDetectionControl.instance.canChangeTarget = false;
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
                    Vector3 facePoint = (target != null && IsInFront(target.position, enemyDetectAngle)) ? target.position : fallbackPoint;
                    MoveTowardsTarget(facePoint, quickAttackDeltaDistance, "punch");
                    isAttacking = true;
                }
                break;

            case 2: //kick
                {
                    Vector3 facePoint = (target != null && IsInFront(target.position, enemyDetectAngle)) ? target.position : fallbackPoint;
                    MoveTowardsTarget(facePoint, quickAttackDeltaDistance, "kick");
                    isAttacking = true;
                }
                break;

            case 3: //mmakick
                {
                    Vector3 facePoint = (target != null && IsInFront(target.position, enemyDetectAngle)) ? target.position : fallbackPoint;
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
        Vector3 facePoint = (target != null && IsInFront(target.position, enemyDetectAngle)) ? target.position : fallbackPoint;

        switch (attackIndex)
        {
            case 1: //heavyAttack1
                {
                    FaceThis(facePoint);
                    anim.SetBool("heavyAttack1", true);
                    isAttacking = true;
                }
                break;

            case 2: //heavyAttack2
                {
                    FaceThis(facePoint);
                    anim.SetBool("heavyAttack2", true);
                    isAttacking = true;
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
        TargetDetectionControl.instance.canChangeTarget = true;
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

    public void ChangeTarget(Transform target_)
    {
        // отключаем старую отметку
        if (currentTarget != null)
        {
            try { currentTarget.ActiveTarget(false); } catch { }
        }

        if (target_ == null)
        {
            // явно очищаем цель
            target = null;
            currentTarget = null;
            oldTarget = null;
            return;
        }

        // Если цель не в передней зоне — попробуем найти лучшую спереди
        if (!IsInFront(target_.position, enemyDetectAngle))
        {
            Transform best = SelectBestTargetInFront(enemyDetectAngle);
            if (best != null)
            {
                target_ = best;
            }
            else
            {
                // нет подходящей цели впереди — не назначаем цель
                target = null;
                currentTarget = null;
                oldTarget = null;
                return;
            }
        }

        target = target_;
        oldTarget = target_.GetComponent<EnemyBase>();
        currentTarget = target_.GetComponent<EnemyBase>();
        if (currentTarget != null) currentTarget.ActiveTarget(true);
    }

    private void NoTarget() // When player gets out of range of current Target
    {
        if (currentTarget != null)
        {
            currentTarget.ActiveTarget(false);
        }
        currentTarget = null;
        oldTarget = null;
        target = null;
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

    public void GetClose() // Animation Event ---- for Moving Close to Target
    {
        Vector3 getCloseTarget;
        if (target == null)
        {
            if (oldTarget != null)
                getCloseTarget = oldTarget.transform.position;
            else
                getCloseTarget = transform.position + transform.forward * 1.4f;
        }
        else
        {
            getCloseTarget = target.position;
        }
        FaceThis(getCloseTarget);
        Vector3 finalPos = TargetOffset(getCloseTarget, 1.4f);
        finalPos.y = 0;
        transform.DOMove(finalPos, 0.2f);
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

    #region Target selection helpers

    private bool IsInFront(Vector3 worldPos, float halfAngleDeg)
    {
        Vector3 dir = (worldPos - transform.position).normalized;
        float dot = Vector3.Dot(transform.forward, dir);
        float cosThreshold = Mathf.Cos(halfAngleDeg * Mathf.Deg2Rad);
        return dot >= cosThreshold;
    }

    private Transform SelectBestTargetInFront(float halfAngleDeg)
    {
        float searchRadius = maxTargetSearchDistance;
        if (TargetDetectionControl.instance != null)
        {
            searchRadius = TargetDetectionControl.instance.detectionRange;
        }

        Collider[] hits = Physics.OverlapSphere(transform.position, searchRadius, enemyLayer);
        Transform best = null;
        float bestDist = float.MaxValue;
        float cosThreshold = Mathf.Cos(halfAngleDeg * Mathf.Deg2Rad);

        foreach (Collider c in hits)
        {
            if (c == null) continue;
            Vector3 dir = (c.transform.position - transform.position).normalized;
            float dot = Vector3.Dot(transform.forward, dir);
            if (dot < cosThreshold) continue; // не впереди

            float d = Vector3.SqrMagnitude(c.transform.position - transform.position);
            if (d < bestDist)
            {
                bestDist = d;
                best = c.transform;
            }
        }

        if (debug && best != null)
        {
            Debug.Log("Selected best front target: " + best.name);
        }

        return best;
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
