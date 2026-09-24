using System.Collections;
using UnityEngine;

public class PlayerSpawner : MonoBehaviour
{
    public GameObject playerPrefab;
    public BSPSpawner dungeonSpawner;
    public Vector3 spawnOffset = new Vector3(0f, 1f, 0f);
    public bool useDungeonStartRoom = true;
    public bool fallbackToSpawnerTransform = true;

    IEnumerator Start()
    {
        Story story = Story.GetOrCreate();

        if (playerPrefab == null)
        {
            Debug.LogError("PlayerSpawner requires a playerPrefab assignment.");
            yield break;
        }

        yield return story.PlayIntroIfNeeded();
        yield return null;
        Vector3 spawnPosition = transform.position;
        Quaternion spawnRotation = transform.rotation;

        if (useDungeonStartRoom)
        {
            if (dungeonSpawner == null)
            {
                dungeonSpawner = FindFirstObjectByType<BSPSpawner>();
            }

            if (dungeonSpawner != null && dungeonSpawner.startRoom != null)
            {
                spawnPosition = dungeonSpawner.startRoom.center + spawnOffset;
            }
            else if (!fallbackToSpawnerTransform)
            {
                Debug.LogError("PlayerSpawner could not find a BSPSpawner start room to spawn from.");
                yield break;
            }
        }

        GameObject player = Instantiate(playerPrefab, spawnPosition, spawnRotation);
        EnsurePlayerStats(player);

        PlayerStats playerStats = player.GetComponentInChildren<PlayerStats>();
        if (playerStats != null && playerStats.DeathCount >= 1)
        {
            GameAudioManager.PlayPlayerRespawn(player.transform.position);
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void EnsurePlayerStats(GameObject player)
    {
        if (player == null)
            return;

        PlayerStats existingStats = player.GetComponentInChildren<PlayerStats>();
        if (existingStats == null)
        {
            player.AddComponent<PlayerStats>();
        }

        if (player.GetComponentInChildren<PlayerRoomStatusLogger>() == null)
        {
            player.AddComponent<PlayerRoomStatusLogger>();
        }

        if (player.GetComponentInChildren<PlayerDamageScreenShake>() == null)
        {
            player.AddComponent<PlayerDamageScreenShake>();
        }

        if (player.GetComponentInChildren<DungeonMapUI>() == null)
        {
            player.AddComponent<DungeonMapUI>();
        }

        if (player.GetComponentInChildren<PlayerStatsHUD>() == null)
        {
            player.AddComponent<PlayerStatsHUD>();
        }

        if (player.GetComponentInChildren<PlayerDash>() == null)
        {
            player.AddComponent<PlayerDash>();
        }
    }

}
