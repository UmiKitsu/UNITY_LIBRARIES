//This function rotates a 2D object so it faces another transforms position.

//Just replace TRANSFORMTOLOOKAT with your actual target, like:
//playerTransform, enemyTransform, etc.

Void LookAt2D()
{
    Vector2 direction = (TRANSFORMTOLOOKAT).position - transform.position;
    float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
    transform.rotation = Quaternion.Euler(0f, 0f, angle);
}