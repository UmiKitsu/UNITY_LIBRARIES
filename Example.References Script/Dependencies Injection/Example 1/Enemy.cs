using UnityEngine;

public class Enemy : MonoBehaviour
{
    [SerializeField] float moveSpeed = 5f;

    // This is a dependency — the Enemy needs a reference to the player to know where to move.
    Transform player;

    // --- Dependency Injection happens here ---
    // The player Transform is *injected* from the outside instead of the Enemy finding it itself.
    public void Init(Transform player)
    {
        this.player = player;
    }

    void Update()
    {
        MoveTowardPlayer();
    }

    private void MoveTowardPlayer()
    {
        if (player)
        {
            transform.position = Vector2.MoveTowards(transform.position, player.position, moveSpeed * Time.deltaTime);

            Vector2 direction = player.position - transform.position;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }
    }
}
