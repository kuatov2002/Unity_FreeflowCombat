using UnityEngine;

public class EnemyBase : MonoBehaviour, IDamageable, IKnockable
{
    [SerializeField] private GameObject hitVfx;
  
    public void SpawnHitVfx(Vector3 pos)
    {
        Instantiate(hitVfx, pos, Quaternion.identity);
    }
    
    public void TakeDamage(float damage)
    {
        SpawnHitVfx(transform.position);
    }

    public void TakeKnock(Vector3 force)
    {
        GetComponent<Rigidbody>().AddForce(force, ForceMode.Impulse);
    }
}
