using System.Collections.Generic;
using UnityEngine;

public class HoneyTrap : MonoBehaviour
{
    [Header("Detonacion")]
    [Tooltip("Radio dentro del cual el enemigo activa la trampa.")]
    [SerializeField] private float activateRadius = 2.5f;

    [Header("Atraccion")]
    [Tooltip("Radio en unidades de mundo dentro del cual los enemigos persiguen el honeypot.")]
    [SerializeField] private float attractRadius = 30f;
    [Tooltip("Segundos entre escaneos de enemigos cercanos.")]
    [SerializeField] private float scanInterval = 0.2f;

    private readonly HashSet<EnemyTemplate> luredEnemies = new HashSet<EnemyTemplate>();
    private EnemyTemplate armedEnemy;
    private float nextScanTime;

    private void Update()
    {
        if (Time.time < nextScanTime)
        {
            return;
        }
        nextScanTime = Time.time + scanInterval;
        ScanEnemies();
        CheckDetonation();
    }

    private void ScanEnemies()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, attractRadius);
        HashSet<EnemyTemplate> now = new HashSet<EnemyTemplate>();

        foreach (Collider hit in hits)
        {
            EnemyTemplate enemy = hit.GetComponentInParent<EnemyTemplate>();
            if (enemy == null || enemy.CurrentState == EnemyState.Death)
            {
                continue;
            }

            if (enemy == armedEnemy)
            {
                continue;
            }

            now.Add(enemy);
            if (luredEnemies.Add(enemy))
            {
                Debug.Log($"[HoneyTrap] {enemy.name} atraído al honeypot.", this);
            }
            enemy.SetLurePosition(transform.position);
        }

        foreach (EnemyTemplate enemy in new List<EnemyTemplate>(luredEnemies))
        {
            if (!now.Contains(enemy))
            {
                enemy.ClearLure();
                luredEnemies.Remove(enemy);
            }
        }
    }

    private void CheckDetonation()
    {
        if (armedEnemy == null)
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, activateRadius);
            foreach (Collider hit in hits)
            {
                EnemyTemplate enemy = hit.GetComponentInParent<EnemyTemplate>();
                if (enemy != null && enemy.CurrentState != EnemyState.Death)
                {
                    armedEnemy = enemy;
                    enemy.ClearLure();
                    luredEnemies.Remove(enemy);
                    Debug.Log($"[HoneyTrap] Activada por {enemy.name}.", this);
                    break;
                }
            }
            return;
        }

        if (armedEnemy.CurrentState == EnemyState.Death)
        {
            armedEnemy = null;
            return;
        }

        float distance = Vector3.Distance(transform.position, armedEnemy.transform.position);
        if (distance > activateRadius * 1.6f)
        {
            Explode(armedEnemy);
        }
    }

    private void Explode(EnemyTemplate enemy)
    {
        Debug.Log($"[HoneyTrap] Explota: {enemy.name} muere al salir de la trampa.", this);

        foreach (EnemyTemplate lured in luredEnemies)
        {
            lured.ClearLure();
        }
        luredEnemies.Clear();

        enemy.TakeDamage(99999f);
        Destroy(gameObject);
    }
}
