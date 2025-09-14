
using UnityEngine;

public interface IDamageable
{
    public void TakeDamage(float damage){}
}

public interface IKnockable
{
    public void TakeKnock(Vector3 force){}
}
