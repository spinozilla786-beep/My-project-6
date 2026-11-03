using UnityEngine;

/// <summary>
/// Immediately defeats any fighter struck by a falling hazard and removes the hazard.
/// </summary>
public class HazardBall : MonoBehaviour
{
    private void OnCollisionEnter(Collision collision)
    {
        FighterController fighter = collision.collider.GetComponentInParent<FighterController>();
        if (fighter != null)
        {
            fighter.EliminateByMeteor();
        }

        Destroy(gameObject);
    }
}