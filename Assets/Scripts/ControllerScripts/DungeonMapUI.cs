using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public class DungeonMapUI : MonoBehaviour
{
    [SerializeField] private KeyCode toggleKey = KeyCode.M;
    [SerializeField] private Vector2 panelSize = new Vector2(420f, 420f);
    [SerializeField] private float mapPadding = 16f;
    [SerializeField] private float hallwayThickness = 10f;
    [SerializeField] private float currentRoomDotSize = 12f;
    [SerializeField] private float keyMarkerFontSize = 24f;
    [SerializeField] private float roomLabelFontSize = 7f;
    [SerializeField] private Vector2 roomLabelSize = new Vector2(48f, 12f);
    [SerializeField] private Color backdropColor = new Color(0f, 0f, 0f, 0.55f);
    [SerializeField] private Color panelColor = new Color(0.08f, 0.08f, 0.08f, 0.92f);
    [SerializeField] private Color roomColor = new Color(0.82f, 0.82f, 0.82f, 1f);
    [SerializeField] private Color hallwayColor = new Color(0.55f, 0.55f, 0.55f, 1f);
    [SerializeField] private Color currentRoomColor = Color.red;
    [SerializeField] private Color keyMarkerColor = Color.yellow;
    [SerializeField] private Color spawnLabelColor = new Color(0f, 0.45f, 0f, 1f);
    [SerializeField] private Color portalLabelColor = Color.red;

    private BSPSpawner _bspSpawner;
    private Transform _trackedTransform;
    private Canvas _canvas;
    private GameObject _mapOverlay;
    private RectTransform _mapContent;
    private RectTransform _currentRoomDot;
    private Bounds _dungeonBounds;
    private bool _built;

    private void Start()
    {
        _bspSpawner = FindFirstObjectByType<BSPSpawner>();
        ResolveTrackedTransform();
        BuildMapUI();
        SetVisible(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            if (!_built)
            {
                BuildMapUI();
            }

            if (_mapOverlay != null)
            {
                SetVisible(!_mapOverlay.activeSelf);
            }
        }

        if (_mapOverlay != null && _mapOverlay.activeSelf)
        {
            UpdateCurrentRoomDot();
        }

    }

    private void ResolveTrackedTransform()
    {
        controlledPlayerCombat playerCombat = GetComponentInChildren<controlledPlayerCombat>();
        if (playerCombat != null)
        {
            _trackedTransform = playerCombat.transform;
            return;
        }

        CharacterController characterController = GetComponentInChildren<CharacterController>();
        if (characterController != null)
        {
            _trackedTransform = characterController.transform;
            return;
        }

        _trackedTransform = transform;
    }

    private void BuildMapUI()
    {
        if (_built || _bspSpawner == null)
        {
            return;
        }

        List<Bounds> rooms = _bspSpawner.GetLeafRooms();
        if (rooms == null || rooms.Count == 0)
        {
            return;
        }

        EnsureCanvas();

        _mapOverlay = CreateUIObject("DungeonMapBackdrop", _canvas.transform);
        RectTransform backdropRect = _mapOverlay.AddComponent<RectTransform>();
        backdropRect.anchorMin = Vector2.zero;
        backdropRect.anchorMax = Vector2.one;
        backdropRect.offsetMin = Vector2.zero;
        backdropRect.offsetMax = Vector2.zero;
        Image backdropImage = _mapOverlay.AddComponent<Image>();
        backdropImage.color = backdropColor;

        GameObject panel = CreateUIObject("DungeonMapPanel", _mapOverlay.transform);
        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = panelSize;
        panelRect.anchoredPosition = Vector2.zero;
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = panelColor;

        GameObject content = CreateUIObject("DungeonMapContent", panel.transform);
        _mapContent = content.AddComponent<RectTransform>();
        _mapContent.anchorMin = new Vector2(0.5f, 0.5f);
        _mapContent.anchorMax = new Vector2(0.5f, 0.5f);
        _mapContent.pivot = new Vector2(0.5f, 0.5f);
        _mapContent.sizeDelta = panelSize - Vector2.one * (mapPadding * 2f);
        _mapContent.anchoredPosition = Vector2.zero;

        _dungeonBounds = CalculateDungeonBounds(rooms);
        DrawHallways(_dungeonBounds);
        DrawRooms(rooms, _dungeonBounds);
        if (_bspSpawner != null && _bspSpawner.endRoom != null)
        {
            CreateLabel("PortalLabel", "Portal", roomLabelFontSize, portalLabelColor, WorldToMapPosition(_bspSpawner.endRoom.center, _dungeonBounds));
        }

        if (_bspSpawner != null && _bspSpawner.startRoom != null)
        {
            CreateLabel("SpawnLabel", "Spawn", roomLabelFontSize, spawnLabelColor, WorldToMapPosition(_bspSpawner.startRoom.center, _dungeonBounds));
        }

        int keyRoomId = _bspSpawner != null ? _bspSpawner.GetFarthestBranchRoomId() : -1;
        if (keyRoomId >= 0)
        {
            CreateLabel("KeyMarker", "?", keyMarkerFontSize, keyMarkerColor, WorldToMapPosition(_bspSpawner.GetRoomBounds(keyRoomId).center, _dungeonBounds));
        }

        _currentRoomDot = CreateDot("CurrentRoomDot", currentRoomDotSize, currentRoomColor, Vector2.zero);
        UpdateCurrentRoomDot();
        _built = true;
    }

    private void EnsureCanvas()
    {
        GameObject canvasObject = CreateUIObject("DungeonMapCanvas", null);
        _canvas = canvasObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasObject.AddComponent<GraphicRaycaster>();
    }

    private void DrawRooms(List<Bounds> rooms, Bounds dungeonBounds)
    {
        for (int i = 0; i < rooms.Count; i++)
        {
            Bounds room = rooms[i];
            GameObject roomObject = CreateUIObject($"Room_{i}", _mapContent);
            RectTransform roomRect = roomObject.AddComponent<RectTransform>();
            roomRect.anchorMin = new Vector2(0.5f, 0.5f);
            roomRect.anchorMax = new Vector2(0.5f, 0.5f);
            roomRect.pivot = new Vector2(0.5f, 0.5f);
            roomRect.anchoredPosition = WorldToMapPosition(room.center, dungeonBounds);
            roomRect.sizeDelta = WorldSizeToMapSize(room.size, dungeonBounds);

            Image image = roomObject.AddComponent<Image>();
            image.color = roomColor;
        }
    }

    private void DrawHallways(Bounds dungeonBounds)
    {
        List<HallwayConnection> connections = _bspSpawner.GetHallwayConnections();
        for (int i = 0; i < connections.Count; i++)
        {
            HallwayConnection connection = connections[i];
            DrawHallwaySegment(connection.hallStart, new Vector3Int(connection.hallEnd.x, connection.hallStart.y, connection.hallStart.z), dungeonBounds);
            DrawHallwaySegment(new Vector3Int(connection.hallEnd.x, connection.hallStart.y, connection.hallStart.z), connection.hallEnd, dungeonBounds);
        }
    }

    private void DrawHallwaySegment(Vector3Int from, Vector3Int to, Bounds dungeonBounds)
    {
        Vector3 worldFrom = new Vector3(from.x, 0f, from.z);
        Vector3 worldTo = new Vector3(to.x, 0f, to.z);

        Vector2 mapFrom = WorldToMapPosition(worldFrom, dungeonBounds);
        Vector2 mapTo = WorldToMapPosition(worldTo, dungeonBounds);
        Vector2 center = (mapFrom + mapTo) * 0.5f;
        Vector2 delta = mapTo - mapFrom;

        GameObject hallwayObject = CreateUIObject("Hallway", _mapContent);
        RectTransform hallwayRect = hallwayObject.AddComponent<RectTransform>();
        hallwayRect.anchorMin = new Vector2(0.5f, 0.5f);
        hallwayRect.anchorMax = new Vector2(0.5f, 0.5f);
        hallwayRect.pivot = new Vector2(0.5f, 0.5f);
        hallwayRect.anchoredPosition = center;
        hallwayRect.sizeDelta = new Vector2(Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y)) + hallwayThickness, hallwayThickness);
        hallwayRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

        Image image = hallwayObject.AddComponent<Image>();
        image.color = hallwayColor;
    }

    private RectTransform CreateDot(string name, float size, Color color, Vector2 anchoredPosition)
    {
        GameObject dotObject = CreateUIObject(name, _mapContent);
        RectTransform dotRect = dotObject.AddComponent<RectTransform>();
        dotRect.anchorMin = new Vector2(0.5f, 0.5f);
        dotRect.anchorMax = new Vector2(0.5f, 0.5f);
        dotRect.pivot = new Vector2(0.5f, 0.5f);
        dotRect.sizeDelta = new Vector2(size, size);
        dotRect.anchoredPosition = anchoredPosition;
        dotObject.AddComponent<Image>().color = color;
        return dotRect;
    }

    private void CreateLabel(string name, string text, float fontSize, Color color, Vector2 anchoredPosition)
    {
        GameObject labelObject = CreateUIObject(name, _mapContent);
        RectTransform labelRect = labelObject.AddComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.5f, 0.5f);
        labelRect.anchorMax = new Vector2(0.5f, 0.5f);
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.sizeDelta = roomLabelSize;
        labelRect.anchoredPosition = anchoredPosition;

        TextMeshProUGUI textComponent = labelObject.AddComponent<TextMeshProUGUI>();
        textComponent.text = text;
        textComponent.fontSize = fontSize;
        textComponent.color = color;
        textComponent.alignment = TextAlignmentOptions.Center;
        textComponent.textWrappingMode = TextWrappingModes.NoWrap;
    }

    private void UpdateCurrentRoomDot()
    {
        if (_currentRoomDot == null || _trackedTransform == null || _bspSpawner == null)
        {
            return;
        }

        int roomId = _bspSpawner.GetRoomIdForPosition(_trackedTransform.position);
        if (roomId < 0)
        {
            _currentRoomDot.gameObject.SetActive(false);
            return;
        }

        _currentRoomDot.gameObject.SetActive(true);
        Bounds currentRoomBounds = _bspSpawner.GetRoomBounds(roomId);
        _currentRoomDot.anchoredPosition = WorldToMapPosition(currentRoomBounds.center, _dungeonBounds);
    }

    private Bounds CalculateDungeonBounds(List<Bounds> rooms)
    {
        Bounds dungeonBounds = rooms[0];

        for (int i = 1; i < rooms.Count; i++)
        {
            dungeonBounds.Encapsulate(rooms[i]);
        }

        List<HallwayConnection> connections = _bspSpawner.GetHallwayConnections();
        for (int i = 0; i < connections.Count; i++)
        {
            dungeonBounds.Encapsulate(new Vector3(connections[i].hallStart.x, 0f, connections[i].hallStart.z));
            dungeonBounds.Encapsulate(new Vector3(connections[i].hallEnd.x, 0f, connections[i].hallEnd.z));
        }

        return dungeonBounds;
    }

    private Vector2 WorldToMapPosition(Vector3 worldPosition, Bounds dungeonBounds)
    {
        float width = Mathf.Max(1f, dungeonBounds.size.x);
        float height = Mathf.Max(1f, dungeonBounds.size.z);
        float normalizedX = (worldPosition.x - dungeonBounds.min.x) / width;
        float normalizedY = (worldPosition.z - dungeonBounds.min.z) / height;

        return new Vector2(
            (normalizedX - 0.5f) * _mapContent.sizeDelta.x,
            (normalizedY - 0.5f) * _mapContent.sizeDelta.y
        );
    }

    private Vector2 WorldSizeToMapSize(Vector3 worldSize, Bounds dungeonBounds)
    {
        float width = Mathf.Max(1f, dungeonBounds.size.x);
        float height = Mathf.Max(1f, dungeonBounds.size.z);

        return new Vector2(
            Mathf.Max(8f, (worldSize.x / width) * _mapContent.sizeDelta.x),
            Mathf.Max(8f, (worldSize.z / height) * _mapContent.sizeDelta.y)
        );
    }

    private void SetVisible(bool isVisible)
    {
        if (_mapOverlay != null)
        {
            _mapOverlay.SetActive(isVisible);
        }
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject gameObject = new GameObject(name);
        if (parent != null)
        {
            gameObject.transform.SetParent(parent, false);
        }

        return gameObject;
    }
}
