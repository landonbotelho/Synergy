using UnityEngine;

[DisallowMultipleComponent]
public class StoreInteract : MonoBehaviour
{
    [SerializeField] private int cost = 500;
    [SerializeField] private int damageIncrease = 50;
    [SerializeField] private int maxHealthIncrease = 25;

    private PlayerStats _player;
    private bool _open;

    private void Awake()
    {
        SphereCollider trigger = GetComponent<SphereCollider>();
        if (trigger == null)
        {
            trigger = gameObject.AddComponent<SphereCollider>();
            trigger.radius = 2f;
        }

        trigger.isTrigger = true;
    }

    private void Update()
    {
        if (_player == null || !Input.GetKeyDown(KeyCode.F))
        {
            return;
        }

        _open = !_open;
        if (_open)
        {
            GameAudioManager.PlayStoreOpen(transform.position);
        }

        Cursor.lockState = _open ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = _open;
    }

    private void OnTriggerEnter(Collider other) => _player = other.GetComponentInParent<PlayerStats>();

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponentInParent<PlayerStats>() != _player)
        {
            return;
        }

        _player = null;
        _open = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnGUI()
    {
        if (!_open || _player == null)
        {
            return;
        }

        Rect rect = new Rect((Screen.width - 260) * 0.5f, (Screen.height - 150) * 0.5f, 260, 150);
        GUILayout.BeginArea(rect, GUI.skin.box);
        GUILayout.Label($"Store   Money: {_player.Money}");
        if (GUILayout.Button($"Damage +{damageIncrease}   ${cost}") && _player.SpendMoney(cost)) _player.AddStrength(damageIncrease);
        if (GUILayout.Button($"Heal To Full   ${cost}") && _player.SpendMoney(cost)) _player.FillHealthToFull();
        if (GUILayout.Button($"Max Health +{maxHealthIncrease}   ${cost}") && _player.SpendMoney(cost)) _player.AddMaxHealth(maxHealthIncrease);
        GUILayout.Label("Press F to close");
        GUILayout.EndArea();
    }
}
