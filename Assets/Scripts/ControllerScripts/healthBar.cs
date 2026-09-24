using UnityEngine;
using UnityEngine.UI;


public class healthBar : MonoBehaviour
{
    [SerializeField] private Slider slider;

    private void Awake()
    {
        if (slider == null)
        {
            slider = GetComponentInChildren<Slider>(true);
        }
    }

    public void updateHealthBar(float currHealth, float maxHealth)
    {
        if (slider != null)
        {
            slider.maxValue = maxHealth;
            slider.value = currHealth;
        }
    }
}
