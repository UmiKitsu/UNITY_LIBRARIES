using UnityEngine;

public class GameBootstrap : MonoBehaviour
{
    [SerializeField] PlayerMovement player;
    [SerializeField] EnemySpawner[] spawners;

    private void Awake()
    {
        // Get the Transform that all enemies/spawners need.
        Transform playerTransform = player.transform;

        // Inject the dependency into each spawner.
        for (int i = 0; i < spawners.Length; i++)
        {
            EnemySpawner spawner = spawners[i];
            if (spawner != null)
            {
                // --- This is the root of your dependency injection graph ---
                spawner.Init(playerTransform);
            }
        }
    }
}
