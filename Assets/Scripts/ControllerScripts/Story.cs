using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class Story : MonoBehaviour
{
    private static Story _instance;
    private static bool _introShown;
    private static bool _pendingDeathMessage;
    private static bool _startMenuComplete;

    private string _blackText;
    private string _bottomText;
    private float _bottomUntil;
    private bool _showEnding;
    private bool _showControls;
    private bool _showPauseMenu;
    private float _endingMenuTime;

    public static Story GetOrCreate()
    {
        if (_instance != null)
        {
            return _instance;
        }

        GameObject storyObject = new GameObject("Story");
        _instance = storyObject.AddComponent<Story>();
        return _instance;
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    public IEnumerator PlayIntroIfNeeded()
    {
        yield return ShowStartMenuIfNeeded();

        if (_introShown)
        {
            yield break;
        }

        _introShown = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        yield return ShowBlackText("So they've given me another prisoner I see. - Dungeon Master", 3f);
        yield return ShowBlackText("How unfortunate for you. - Dungeon Master", 3f);
        yield return ShowBlackText("Those who enter my dungeon never leave. - Dungeon Master", 3f);
        yield return ShowBlackText("You'll see. This dungeon is always changing. You'll never find your way out. - Dungeon Master", 3f);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private IEnumerator ShowStartMenuIfNeeded()
    {
        if (_startMenuComplete)
        {
            yield break;
        }

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        while (!_startMenuComplete)
        {
            yield return null;
        }

        Time.timeScale = 1f;
        _showControls = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        GameAudioManager.PlayDungeonMusic();
    }

    public static void QueueDeathMessage()
    {
        _pendingDeathMessage = true;
    }

    public static void ShowBossMessage()
    {
        if (_instance != null)
        {
            _instance.ShowBottom("Ha to think you made it this far. Well. No matter. Your journey ends here prisoner - Dungeon Master", 5f);
        }
    }

    public static void ShowEnding()
    {
        if (_instance != null)
        {
            _instance._showEnding = true;
            _instance._endingMenuTime = Time.unscaledTime + 3f;
            _instance._blackText = "Finally. Ive slain the Dungeon Master. I hope to never step foot in this dungeon again.";
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (_pendingDeathMessage)
        {
            _pendingDeathMessage = false;
            ShowBottom("Ha Ha Ha! Are you trying to escape. How shameful. - Dungeon Master", 5f);
        }
    }

    private void Update()
    {
        if (!_startMenuComplete || _showEnding || !string.IsNullOrEmpty(_blackText))
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            _showPauseMenu = !_showPauseMenu;
            _showControls = false;
            Time.timeScale = _showPauseMenu ? 0f : 1f;
            Cursor.lockState = _showPauseMenu ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = _showPauseMenu;
        }
    }

    private IEnumerator ShowBlackText(string text, float duration)
    {
        _blackText = text.ToUpperInvariant();
        yield return new WaitForSeconds(duration);
        _blackText = null;
    }

    private void ShowBottom(string text, float duration)
    {
        _bottomText = text.ToUpperInvariant();
        _bottomUntil = Time.unscaledTime + duration;
    }

    private void OnGUI()
    {
        GUIStyle centered = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true, fontSize = 24 };
        centered.normal.textColor = Color.white;
        centered.hover.textColor = Color.white;
        centered.active.textColor = Color.white;
        centered.focused.textColor = Color.white;

        if (!_startMenuComplete)
        {
            Time.timeScale = 0f;
            GUI.color = Color.black;
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none);
            GUI.color = Color.white;
            GUI.Label(new Rect(Screen.width * 0.15f, Screen.height * 0.18f, Screen.width * 0.7f, 70f), "SYNERGY", centered);

            if (GUI.Button(new Rect(Screen.width * 0.5f - 80f, Screen.height * 0.42f, 160f, 36f), "Start"))
            {
                _startMenuComplete = true;
            }

            if (GUI.Button(new Rect(Screen.width * 0.5f - 80f, Screen.height * 0.5f, 160f, 36f), "Controls"))
            {
                _showControls = !_showControls;
            }

            if (GUI.Button(new Rect(Screen.width * 0.5f - 80f, Screen.height * 0.58f, 160f, 36f), "Quit"))
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }

            if (_showControls)
            {
                GUI.Label(new Rect(Screen.width * 0.25f, Screen.height * 0.66f, Screen.width * 0.5f, 170f),
                    "WASD: MOVE\nSPACE: JUMP\nE: DODGE\nLEFT MOUSE: ATTACK\nRIGHT MOUSE: BLOCK\nF: OPEN STORE", centered);
            }

            return;
        }

        if (_showPauseMenu)
        {
            GUI.color = Color.black;
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none);
            GUI.color = Color.white;
            GUI.Label(new Rect(Screen.width * 0.15f, Screen.height * 0.16f, Screen.width * 0.7f, 70f), "PAUSED", centered);

            if (GUI.Button(new Rect(Screen.width * 0.5f - 80f, Screen.height * 0.36f, 160f, 36f), "Resume"))
            {
                _showPauseMenu = false;
                _showControls = false;
                Time.timeScale = 1f;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            if (GUI.Button(new Rect(Screen.width * 0.5f - 80f, Screen.height * 0.44f, 160f, 36f), "Restart"))
            {
                _showPauseMenu = false;
                _showControls = false;
                Time.timeScale = 1f;
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }

            if (GUI.Button(new Rect(Screen.width * 0.5f - 80f, Screen.height * 0.52f, 160f, 36f), "Controls"))
            {
                _showControls = !_showControls;
            }

            if (GUI.Button(new Rect(Screen.width * 0.5f - 80f, Screen.height * 0.6f, 160f, 36f), "Quit"))
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }

            if (_showControls)
            {
                GUI.Label(new Rect(Screen.width * 0.25f, Screen.height * 0.68f, Screen.width * 0.5f, 170f),
                    "WASD: MOVE\nSPACE: JUMP\nE: DODGE\nLEFT MOUSE: ATTACK\nRIGHT MOUSE: BLOCK\nF: OPEN STORE", centered);
            }

            return;
        }

        if (!string.IsNullOrEmpty(_blackText))
        {
            GUI.color = Color.black;
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none);
            GUI.color = Color.white;
            GUI.Label(new Rect(Screen.width * 0.15f, Screen.height * 0.62f, Screen.width * 0.7f, Screen.height * 0.25f), _blackText, centered);
        }

        if (!string.IsNullOrEmpty(_bottomText) && Time.unscaledTime < _bottomUntil)
        {
            GUI.Label(new Rect(Screen.width * 0.15f, Screen.height - 110f, Screen.width * 0.7f, 70f), _bottomText, centered);
        }

        if (!_showEnding)
        {
            return;
        }

        if (Time.unscaledTime < _endingMenuTime)
        {
            return;
        }

        Time.timeScale = 0f;

        if (GUI.Button(new Rect(Screen.width * 0.5f - 110f, Screen.height * 0.65f, 100f, 30f), "Play Again"))
        {
            Time.timeScale = 1f;
            _showEnding = false;
            _endingMenuTime = 0f;
            _blackText = null;
            _bottomText = null;
            _introShown = false;
            _startMenuComplete = false;
            GameAudioManager.PlayDungeonMusic();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        if (GUI.Button(new Rect(Screen.width * 0.5f + 10f, Screen.height * 0.65f, 100f, 30f), "Quit"))
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
