using System;
using UnityEngine;
using System.Collections;

public class KillZone : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.gameObject.CompareTag("Player")) return;

        PlayerClass targetClass = other.gameObject.GetComponent<PlayerClass>();
        // PlayerController playerController = other.gameObject.GetComponent<PlayerController>();

        if (targetClass != null) targetClass.TakeDamage(300);
    }
}
