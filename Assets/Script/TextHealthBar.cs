using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;
using TMPro;

public class TextHealthBar : MonoBehaviour
{
    public TextMeshPro textMeshPro;

    public float maxHealth = 100;
    private float currentHealth;

    void Start()
    {
        currentHealth = maxHealth;
        UpdateHealthText();
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        if (currentHealth < 0) currentHealth = 0;
        UpdateHealthText();
    }

    public void Heal(float heelHP)
    {
        currentHealth += heelHP;
        if (currentHealth > maxHealth) currentHealth = maxHealth;
        UpdateHealthText();
    }

    private void UpdateHealthText()
    {
        float health = (float)(currentHealth / maxHealth);

        textMeshPro.text = "";

        while (health > 0)
        {
            health -= 0.1f;
            textMeshPro.text += "I";
        }   
    }
}
