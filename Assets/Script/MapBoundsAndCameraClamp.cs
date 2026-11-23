using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

[DefaultExecutionOrder(1000)]
public class MapBoundsAndCameraClamp : MonoBehaviour
{
    private static MapBoundsAndCameraClamp _instance;

    private bool _hasBounds = false;
    private Bounds _mapBounds; private Vector2 _innerMin, _innerMax;
    private GameObject _wallsRoot;
    private Transform _playerTf;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null) return;
        GameObject go = new GameObject("MapBoundsAndCameraClamp");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<MapBoundsAndCameraClamp>();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
        SetupForScene(SceneManager.GetActiveScene().name);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SetupForScene(scene.name);
    }

    private void SetupForScene(string sceneName)
    {
        _hasBounds = false;
        _playerTf = null;
        if (_wallsRoot != null) Destroy(_wallsRoot);

        bool isGameplay = sceneName.StartsWith("Room", StringComparison.OrdinalIgnoreCase)
                       || sceneName.Equals("BossRoom", StringComparison.OrdinalIgnoreCase);
        if (!isGameplay) return;

        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.backgroundColor = new Color(0.04f, 0.04f, 0.06f, 1f);
        }

        if (ComputeRoomTilemapBounds(out _mapBounds))
        {
            _hasBounds = true;
            BuildSolidPerimeterWalls(_mapBounds);
        }
    }

    private bool ComputeRoomTilemapBounds(out Bounds totalBounds)
    {
        totalBounds = new Bounds();
        bool init = false;

        Tilemap[] tilemaps = FindObjectsCompat<Tilemap>();
        foreach (Tilemap tm in tilemaps)
        {
            if (tm == null || !tm.gameObject.activeInHierarchy) continue;
            tm.CompressBounds();
            BoundsInt cb = tm.cellBounds;
            if (cb.size.x <= 0 || cb.size.y <= 0) continue;

            Vector3 minW = tm.CellToWorld(cb.min);
            Vector3 maxW = tm.CellToWorld(cb.max);
            Bounds b = new Bounds((minW + maxW) * 0.5f, new Vector3(Mathf.Abs(maxW.x - minW.x), Mathf.Abs(maxW.y - minW.y), 1f));

            if (b.size.x < 4f || b.size.y < 4f) continue;

            if (!init)
            {
                totalBounds = b;
                init = true;
            }
            else
            {
                totalBounds.Encapsulate(b);
            }
        }

        if (!init)
        {
            TilemapRenderer[] trs = FindObjectsCompat<TilemapRenderer>();
            foreach (TilemapRenderer tr in trs)
            {
                if (tr == null || !tr.gameObject.activeInHierarchy) continue;
                Bounds b = tr.bounds;
                if (b.size.x < 4f || b.size.y < 4f) continue;
                if (!init) { totalBounds = b; init = true; }
                else totalBounds.Encapsulate(b);
            }
        }

        return init;
    }

    private void BuildSolidPerimeterWalls(Bounds b)
    {
        _wallsRoot = new GameObject("AutoMapBoundary_Walls");
        float thickness = 2.0f;
        string sn = SceneManager.GetActiveScene().name.ToLowerInvariant(); float inset = (sn.Contains("3") || sn.Contains("boss")) ? 1.75f : 1.10f;

        float leftX   = b.min.x + inset;
        float rightX  = b.max.x - inset;
        float bottomY = b.min.y + inset;
        float topY    = b.max.y - inset; _innerMin = new Vector2(leftX + 0.35f, bottomY + 0.45f); _innerMax = new Vector2(rightX - 0.35f, topY - 0.45f);
        float width   = Mathf.Max(2f, rightX - leftX) + thickness * 2f;
        float height  = Mathf.Max(2f, topY - bottomY) + thickness * 2f;
        Vector2 center = new Vector2((leftX + rightX) * 0.5f, (bottomY + topY) * 0.5f);

        CreateWall("AutoWall_Top",    new Vector2(center.x, topY + thickness * 0.5f),    new Vector2(width, thickness));
        CreateWall("AutoWall_Bottom", new Vector2(center.x, bottomY - thickness * 0.5f), new Vector2(width, thickness));
        CreateWall("AutoWall_Left",   new Vector2(leftX - thickness * 0.5f, center.y),   new Vector2(thickness, height));
        CreateWall("AutoWall_Right",  new Vector2(rightX + thickness * 0.5f, center.y),  new Vector2(thickness, height));
    }

    private void CreateWall(string name, Vector2 pos, Vector2 size)
    {
        GameObject w = new GameObject(name);
        w.transform.SetParent(_wallsRoot.transform, false);
        w.transform.position = new Vector3(pos.x, pos.y, 0f);
        BoxCollider2D col = w.AddComponent<BoxCollider2D>();
        col.size = size;
        col.isTrigger = false;
    }

    private void LateUpdate()
    {
        if (!_hasBounds) return;
        Camera cam = Camera.main;
        if (cam == null || !cam.orthographic) return;

        if (_playerTf == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) _playerTf = p.transform;
        }

        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;

        Vector3 pos = cam.transform.position;

        float minX = _mapBounds.min.x + halfW;
        float maxX = _mapBounds.max.x - halfW;
        float minY = _mapBounds.min.y + halfH;
        float maxY = _mapBounds.max.y - halfH;

        pos.x = (minX <= maxX) ? Mathf.Clamp(pos.x, minX, maxX) : _mapBounds.center.x;
        pos.y = (minY <= maxY) ? Mathf.Clamp(pos.y, minY, maxY) : _mapBounds.center.y;

        cam.transform.position = pos; if (_playerTf != null && _innerMin.x < _innerMax.x && _innerMin.y < _innerMax.y) { Vector3 pp = _playerTf.position; pp.x = Mathf.Clamp(pp.x, _innerMin.x, _innerMax.x); pp.y = Mathf.Clamp(pp.y, _innerMin.y, _innerMax.y); _playerTf.position = pp; }
    }

    private T[] FindObjectsCompat<T>() where T : UnityEngine.Object
    {
#if UNITY_6000_0_OR_NEWER
        return UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None);
#else
        return UnityEngine.Object.FindObjectsOfType<T>();
#endif
    }
}
