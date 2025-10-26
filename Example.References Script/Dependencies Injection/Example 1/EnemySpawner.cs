using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [SerializeField] GameObject enemyPrefab;
    [SerializeField] float spawnDelay = 5f;

    // The spawner also depends on the player's Transform.
    Transform player;

    // --- Dependency Injection happens here ---
    // The player reference is injected externally (from GameBootstrap).
    public void Init(Transform player)
    {
        this.player = player;
    }

    void Start()
    {
        StartCoroutine(SpawnEnemies());
    }

    IEnumerator SpawnEnemies()
    {

        while (player)
        {
            float randomX = Random.Range(-3f, 3f);
            Vector2 spawnPosX = new(randomX, transform.position.y);

            GameObject newEnemy = Instantiate(enemyPrefab, spawnPosX, Quaternion.identity);

            // Each new enemy also receives the player dependency.
            Enemy enemy = newEnemy.GetComponent<Enemy>();
            if (enemy != null)
            {
                // --- Inject the player dependency into the spawned Enemy ---
                enemy.Init(player);
            } 
            yield return new WaitForSeconds(spawnDelay);
        }
    }
}
