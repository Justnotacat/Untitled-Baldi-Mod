using UnityEditor;
using UnityEngine;

// ── Save data container ───────────────────────────────────────────────────────
// Serialized to JSON and stored in EditorPrefs so layouts survive restarts.
[System.Serializable]
public class GridPainterSaveData
{
    public int width;
    public int height;
    public int[] cells;   // CellState flattened row-major: index = x * height + y
    public int[] rooms;   // roomGrid flattened the same way, -1 means no room
    public string name;
    public string savedAt;
}

public class GridPainterWindow : EditorWindow
{
    // ── Enums ─────────────────────────────────────────────────────────────────
    enum CellState { Wall, Open, Entrance, Exit }
    enum PaintMode { Open, Wall, Entrance, Exit, Erase, Room }

    // Holds the resolved tile type + rotation index for a single cell
    struct TileInfo { public TileType type; public int index; }

    // ── Grid state ────────────────────────────────────────────────────────────
    CellState[,] grid;      // primary cell data (Wall / Open / Entrance / Exit)
    int[,] roomGrid;  // per-cell room index; -1 = not part of any room
    int gridW = 12;      // live grid width
    int gridH = 12;      // live grid height
    int pendingW = 12;      // width typed in size bar (not yet applied)
    int pendingH = 12;      // height typed in size bar (not yet applied)

    // ── Serialized flat arrays (survive domain reload / recompile) ────────────
    // Unity cannot serialize 2-D arrays, so we mirror grid/roomGrid into these
    // 1-D arrays in OnDisable and restore them in OnEnable.
    [SerializeField] int[] _cellsFlat;
    [SerializeField] int[] _roomsFlat;
    [SerializeField] int _serializedW;
    [SerializeField] int _serializedH;

    // ── Paint state ───────────────────────────────────────────────────────────
    PaintMode paintMode = PaintMode.Open;
    bool isPainting; // true while left mouse button is held
    bool isErasing;  // true while right mouse button is held

    // ── Room drag state ───────────────────────────────────────────────────────
    int activeRoom = 0;      // which room index is currently selected
    bool isDraggingRoom;      // true while user is dragging a room rectangle
    Vector2Int roomDragStart;       // grid cell where the drag began
    Vector2Int roomDragEnd;         // grid cell currently under the mouse

    // ── Scroll positions ──────────────────────────────────────────────────────
    Vector2 gridScroll;  // scroll position inside the grid canvas
    Vector2 mainScroll;  // scroll position of the whole window

    // ── Build settings ────────────────────────────────────────────────────────
    Vector3 buildOrigin = Vector3.zero;
    Material floorMat;
    Material wallMat;
    Material ceilingMat;
    Sprite flagSprite;
    Sprite signSprite;
    Transform buildParent;
    bool containsCeiling = true;
    bool busy;           // guard to prevent double-clicking Build

    // ── Save / Load ───────────────────────────────────────────────────────────
    string saveSlotName = "Layout_1";
    bool showSaveLoad = false;
    Vector2 saveScroll;
    const string PREFS_INDEX_KEY = "GridPainter_SaveIndex";

    // ── Visual / zoom ─────────────────────────────────────────────────────────
    int cellSize = 20; // pixels per grid cell — driven by the zoom slider
    const int PAD = 8;  // pixel padding around the grid canvas

    // ── Cell colours ─────────────────────────────────────────────────────────
    static readonly Color C_WALL = new Color(0.13f, 0.13f, 0.15f, 1f);
    static readonly Color C_OPEN = new Color(0.28f, 0.62f, 0.90f, 1f);
    static readonly Color C_ENTRANCE = new Color(0.20f, 0.85f, 0.45f, 1f);
    static readonly Color C_EXIT = new Color(0.95f, 0.75f, 0.15f, 1f);
    static readonly Color C_GRID = new Color(0.06f, 0.06f, 0.08f, 1f);
    static readonly Color C_HOVER = new Color(1.00f, 1.00f, 1.00f, 0.18f);
    static readonly Color C_ROOM_DRAG = new Color(1.00f, 1.00f, 1.00f, 0.30f);

    // One solid colour per room index (wraps if more than 8 rooms)
    static readonly Color[] ROOM_COLORS =
    {
        new Color(0.85f, 0.35f, 0.85f, 1f),
        new Color(0.95f, 0.55f, 0.15f, 1f),
        new Color(0.15f, 0.85f, 0.75f, 1f),
        new Color(0.85f, 0.85f, 0.20f, 1f),
        new Color(0.20f, 0.60f, 0.20f, 1f),
        new Color(0.85f, 0.20f, 0.30f, 1f),
        new Color(0.40f, 0.40f, 0.90f, 1f),
        new Color(0.90f, 0.60f, 0.70f, 1f),
    };

    // Button highlight colour per paint mode
    static readonly Color[] MODE_COLORS =
    {
        C_OPEN,
        new Color(0.35f, 0.35f, 0.38f, 1f),
        C_ENTRANCE,
        C_EXIT,
        new Color(0.80f, 0.28f, 0.28f, 1f),
        new Color(0.75f, 0.30f, 0.85f, 1f),
    };

    static readonly string[] MODE_LABELS = { "Open", "Wall", "Entrance", "Exit", "Erase", "Room" };
    static readonly string[] MODE_TIPS =
    {
        "Paint walkable/open cells",
        "Paint solid wall cells (skipped at build time)",
        "Mark a cell as an entrance — places sign prefab overlay",
        "Mark a cell as the exit/goal — places flag prefab overlay",
        "Reset cells back to Wall",
        "Drag to fill a rectangular area with a room ID (open cells only)"
    };

    // ── Menu item ─────────────────────────────────────────────────────────────
    [MenuItem("CM Tools/Grid Painter")]
    public static void ShowWindow()
    {
        var w = GetWindow<GridPainterWindow>("Grid Painter");
        w.minSize = new Vector2(340, 520);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    // OnEnable fires after every domain reload (recompile) and when the window
    // is first opened. We restore the grid from the serialized flat arrays so
    // room data is never lost just because Unity recompiled.
    void OnEnable()
    {
        Debug.Log("[GridPainter] OnEnable — attempting to restore grid from serialized state.");

        if (_cellsFlat != null
            && _serializedW > 0
            && _serializedH > 0
            && _cellsFlat.Length == _serializedW * _serializedH)
        {
            gridW = _serializedW;
            gridH = _serializedH;
            pendingW = gridW;
            pendingH = gridH;
            grid = new CellState[gridW, gridH];
            roomGrid = new int[gridW, gridH];

            for (int x = 0; x < gridW; x++)
            {
                for (int y = 0; y < gridH; y++)
                {
                    int idx = x * gridH + y;
                    grid[x, y] = (CellState)_cellsFlat[idx];
                    roomGrid[x, y] = (_roomsFlat != null && _roomsFlat.Length == _cellsFlat.Length)
                                     ? _roomsFlat[idx] : -1;
                }
            }

            Debug.Log($"[GridPainter] Restored {gridW}x{gridH} grid from serialized state.");
        }
        else
        {
            Debug.Log("[GridPainter] No valid serialized state found — starting fresh.");
        }
    }

    // OnDisable fires before a domain reload and when the window is closed.
    // We snapshot the live 2-D arrays into the serialized flat arrays here.
    void OnDisable()
    {
        Debug.Log("[GridPainter] OnDisable — serializing grid state.");
        SerializeGridToFlat();
    }

    // Copies grid + roomGrid into the [SerializeField] flat arrays so Unity
    // can persist them across recompiles.
    void SerializeGridToFlat()
    {
        if (grid == null)
        {
            Debug.Log("[GridPainter] SerializeGridToFlat — grid is null, nothing to serialize.");
            return;
        }

        EnsureRoomGrid();

        _serializedW = gridW;
        _serializedH = gridH;
        _cellsFlat = new int[gridW * gridH];
        _roomsFlat = new int[gridW * gridH];

        int roomCellCount = 0;
        for (int x = 0; x < gridW; x++)
        {
            for (int y = 0; y < gridH; y++)
            {
                int idx = x * gridH + y;
                _cellsFlat[idx] = (int)grid[x, y];
                _roomsFlat[idx] = roomGrid[x, y];
                if (roomGrid[x, y] >= 0) roomCellCount++;
            }
        }

        Debug.Log($"[GridPainter] Serialized {gridW}x{gridH} grid. Room cells: {roomCellCount}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Main GUI entry point
    // ─────────────────────────────────────────────────────────────────────────
    void OnGUI()
    {
        AutoFillMaterials();
        mainScroll = EditorGUILayout.BeginScrollView(mainScroll);
        DrawHeader();
        GUILayout.Space(4);
        DrawGridSizeBar();
        GUILayout.Space(4);
        DrawPaintToolbar();
        if (paintMode == PaintMode.Room)
        {
            GUILayout.Space(2);
            DrawRoomToolbar();
        }
        GUILayout.Space(6);
        DrawGrid();
        GUILayout.Space(4);
        DrawStats();
        GUILayout.Space(2);
        DrawBuildSettings();
        GUILayout.Space(4);
        DrawSaveLoad();
        EditorGUILayout.EndScrollView();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Auto-fill material / sprite references from the helper if slots are empty
    // ─────────────────────────────────────────────────────────────────────────
    void AutoFillMaterials()
    {
        if (floorMat == null) floorMat = TheBCMHT_Helper.getSampleFloor(false);
        if (wallMat == null) wallMat = TheBCMHT_Helper.getSampleWall(false);
        if (ceilingMat == null) ceilingMat = TheBCMHT_Helper.getSampleCeiling(false);
        if (flagSprite == null) flagSprite = TheBCMHT_Helper.getCornMazeFlagSampleSprite();
        if (signSprite == null) signSprite = TheBCMHT_Helper.getCornMazeSignSampleSprite();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Header
    // ─────────────────────────────────────────────────────────────────────────
    void DrawHeader()
    {
        GUILayout.Label("Grid Painter", new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 15,
            alignment = TextAnchor.MiddleCenter
        });
        GUILayout.Label(
            "Paint open/wall cells — tile types auto-calculate from neighbors.",
            new GUIStyle(EditorStyles.centeredGreyMiniLabel) { wordWrap = true });
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Grid size bar (W / H fields + New Grid / Fill Open buttons)
    // ─────────────────────────────────────────────────────────────────────────
    void DrawGridSizeBar()
    {
        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.LabelField("W", GUILayout.Width(14));
        pendingW = Mathf.Clamp(EditorGUILayout.IntField(pendingW, GUILayout.Width(36)), 2, 60);
        GUILayout.Space(6);
        EditorGUILayout.LabelField("H", GUILayout.Width(14));
        pendingH = Mathf.Clamp(EditorGUILayout.IntField(pendingH, GUILayout.Width(36)), 2, 60);

        GUILayout.Space(4);

        if (GUILayout.Button("New Grid", GUILayout.Height(22)))
        {
            if (grid == null || EditorUtility.DisplayDialog(
                    "New Grid",
                    $"Replace the current {gridW}x{gridH} grid with a new {pendingW}x{pendingH} grid?\nThis cannot be undone.",
                    "Yes", "Cancel"))
            {
                gridW = pendingW;
                gridH = pendingH;
                InitGrid();
            }
        }

        if (grid != null && GUILayout.Button("Fill Open", GUILayout.Height(22)))
            FillAll(CellState.Open);

        EditorGUILayout.EndHorizontal();

        if (grid != null)
            GUILayout.Label($"Active grid: {gridW} x {gridH}",
                new GUIStyle(EditorStyles.centeredGreyMiniLabel));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Paint mode toolbar
    // ─────────────────────────────────────────────────────────────────────────
    void DrawPaintToolbar()
    {
        EditorGUILayout.BeginHorizontal();
        for (int i = 0; i < MODE_LABELS.Length; i++)
        {
            bool selected = (int)paintMode == i;
            var oldBg = GUI.backgroundColor;
            var oldCol = GUI.contentColor;

            GUI.backgroundColor = selected ? MODE_COLORS[i] : new Color(0.28f, 0.28f, 0.30f, 1f);
            GUI.contentColor = selected ? Color.white : new Color(0.70f, 0.70f, 0.70f, 1f);

            if (GUILayout.Button(new GUIContent(MODE_LABELS[i], MODE_TIPS[i]),
                    new GUIStyle(GUI.skin.button)
                    { fontStyle = selected ? FontStyle.Bold : FontStyle.Normal, fontSize = 11 }))
                paintMode = (PaintMode)i;

            GUI.backgroundColor = oldBg;
            GUI.contentColor = oldCol;
        }
        EditorGUILayout.EndHorizontal();
        GUILayout.Label(MODE_TIPS[(int)paintMode],
            new GUIStyle(EditorStyles.centeredGreyMiniLabel));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Room colour / index toolbar (only visible when Room mode is active)
    // ─────────────────────────────────────────────────────────────────────────
    void DrawRoomToolbar()
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Room Index:", GUILayout.Width(78));

        for (int i = 0; i < ROOM_COLORS.Length; i++)
        {
            bool selected = activeRoom == i;
            var oldBg = GUI.backgroundColor;
            var c = ROOM_COLORS[i];
            GUI.backgroundColor = new Color(c.r, c.g, c.b, selected ? 1f : 0.5f);

            if (GUILayout.Button(i.ToString(),
                    new GUIStyle(GUI.skin.button)
                    { fontStyle = selected ? FontStyle.Bold : FontStyle.Normal, fontSize = 11 },
                    GUILayout.Width(28)))
                activeRoom = i;

            GUI.backgroundColor = oldBg;
        }

        GUILayout.FlexibleSpace();

        if (grid != null && roomGrid != null && GUILayout.Button("Clear Rooms", GUILayout.Height(18)))
        {
            if (EditorUtility.DisplayDialog("Clear Rooms", "Remove all room data?", "Yes", "Cancel"))
                ClearRooms();
        }

        EditorGUILayout.EndHorizontal();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Grid canvas (zoom bar + scrollable cell grid)
    // ─────────────────────────────────────────────────────────────────────────
    void DrawGrid()
    {
        if (grid == null)
        {
            EditorGUILayout.HelpBox("Set a size and click \"New Grid\" to start painting.", MessageType.Info);
            return;
        }

        // ── Zoom controls ─────────────────────────────────────────────────────
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Zoom", GUILayout.Width(36));
        cellSize = (int)GUILayout.HorizontalSlider(cellSize, 8, 48, GUILayout.ExpandWidth(true));
        cellSize = Mathf.Clamp(EditorGUILayout.IntField(cellSize, GUILayout.Width(32)), 8, 48);
        if (GUILayout.Button("-", GUILayout.Width(22), GUILayout.Height(18)))
            cellSize = Mathf.Max(8, cellSize - 4);
        if (GUILayout.Button("+", GUILayout.Width(22), GUILayout.Height(18)))
            cellSize = Mathf.Min(48, cellSize + 4);
        if (GUILayout.Button("Reset", GUILayout.Width(42), GUILayout.Height(18)))
            cellSize = 20;
        EditorGUILayout.EndHorizontal();

        // ── Scrollable canvas ─────────────────────────────────────────────────
        float pixW = gridW * cellSize + PAD * 2;
        float pixH = gridH * cellSize + PAD * 2;

        // Expand the scroll view up to 600 px tall before scrolling kicks in
        float viewH = Mathf.Clamp(pixH + 4f, 120f, 600f);

        gridScroll = EditorGUILayout.BeginScrollView(gridScroll,
            GUILayout.Height(viewH), GUILayout.ExpandWidth(true));

        Rect gridRect = GUILayoutUtility.GetRect(pixW, pixH);
        Event e = Event.current;

        // Draw every cell
        for (int x = 0; x < gridW; x++)
            for (int y = 0; y < gridH; y++)
                DrawCell(CellRect(gridRect, x, y), x, y, e.mousePosition);

        // Draw the room-drag preview rectangle while the user is dragging
        if (isDraggingRoom)
            DrawRoomDragPreview(gridRect);

        HandleMouseEvents(gridRect, e);

        EditorGUILayout.EndScrollView();
    }

    // Semi-transparent white overlay showing the pending room drag rectangle
    void DrawRoomDragPreview(Rect gridRect)
    {
        int x0 = Mathf.Min(roomDragStart.x, roomDragEnd.x);
        int x1 = Mathf.Max(roomDragStart.x, roomDragEnd.x);
        int y0 = Mathf.Min(roomDragStart.y, roomDragEnd.y);
        int y1 = Mathf.Max(roomDragStart.y, roomDragEnd.y);

        for (int x = x0; x <= x1; x++)
            for (int y = y0; y <= y1; y++)
                EditorGUI.DrawRect(CellRect(gridRect, x, y), C_ROOM_DRAG);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Mouse event handling (room drag vs. normal paint)
    // ─────────────────────────────────────────────────────────────────────────
    void HandleMouseEvents(Rect gridRect, Event e)
    {
        if (paintMode == PaintMode.Room)
        {
            // Left mouse down: start a new drag
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                if (TryGetCell(gridRect, e.mousePosition, out int cx, out int cy))
                {
                    isDraggingRoom = true;
                    roomDragStart = new Vector2Int(cx, cy);
                    roomDragEnd = new Vector2Int(cx, cy);
                    Debug.Log($"[GridPainter] Room drag started at ({cx},{cy}), room index {activeRoom}.");
                    e.Use();
                    Repaint();
                }
            }
            // Mouse moved while dragging: update the end corner
            else if (e.type == EventType.MouseDrag && isDraggingRoom)
            {
                if (TryGetCell(gridRect, e.mousePosition, out int cx, out int cy))
                    roomDragEnd = new Vector2Int(cx, cy);
                e.Use();
                Repaint();
            }
            // Mouse released: commit the rectangle
            else if (e.type == EventType.MouseUp && isDraggingRoom)
            {
                CommitRoomDrag();
                isDraggingRoom = false;
                e.Use();
                Repaint();
            }
            // Right mouse down: clear a single cell's room assignment
            else if (e.type == EventType.MouseDown && e.button == 1)
            {
                if (TryGetCell(gridRect, e.mousePosition, out int cx, out int cy))
                {
                    Debug.Log($"[GridPainter] Room erased at ({cx},{cy}).");
                    roomGrid[cx, cy] = -1;
                }
                e.Use();
                Repaint();
            }
            else if (e.type == EventType.MouseMove)
            {
                Repaint();
            }
        }
        else
        {
            // Normal paint / erase
            if (e.type == EventType.MouseDown && (e.button == 0 || e.button == 1))
            {
                isPainting = e.button == 0;
                isErasing = e.button == 1;
                ApplyPaint(gridRect, e.mousePosition);
                e.Use();
                Repaint();
            }
            else if (e.type == EventType.MouseDrag && (isPainting || isErasing))
            {
                ApplyPaint(gridRect, e.mousePosition);
                e.Use();
                Repaint();
            }
            else if (e.type == EventType.MouseUp)
            {
                isPainting = false;
                isErasing = false;
            }
            else if (e.type == EventType.MouseMove)
            {
                Repaint();
            }
        }
    }

    // Write activeRoom into every non-wall cell in the dragged rectangle.
    // Wall cells are skipped — rooms are metadata on open cells only.
    void CommitRoomDrag()
    {
        int x0 = Mathf.Min(roomDragStart.x, roomDragEnd.x);
        int x1 = Mathf.Max(roomDragStart.x, roomDragEnd.x);
        int y0 = Mathf.Min(roomDragStart.y, roomDragEnd.y);
        int y1 = Mathf.Max(roomDragStart.y, roomDragEnd.y);

        int count = 0;
        int skipped = 0;
        for (int x = x0; x <= x1; x++)
        {
            for (int y = y0; y <= y1; y++)
            {
                if (grid[x, y] != CellState.Wall)
                {
                    roomGrid[x, y] = activeRoom;
                    count++;
                }
                else
                {
                    skipped++;
                }
            }
        }

        Debug.Log($"[GridPainter] CommitRoomDrag: assigned room {activeRoom} to {count} open cells, skipped {skipped} wall cells.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Cell rendering
    // ─────────────────────────────────────────────────────────────────────────
    void DrawCell(Rect r, int x, int y, Vector2 mouse)
    {
        // Room cells use their room color as the solid base — fully opaque so
        // they are visually distinct from hallway (blue) cells.
        // Non-room cells use the normal state color.
        Color bg;
        if (roomGrid != null && roomGrid[x, y] >= 0)
        {
            var rc = ROOM_COLORS[roomGrid[x, y] % ROOM_COLORS.Length];
            bg = new Color(rc.r, rc.g, rc.b, 1f);
        }
        else
        {
            bg = grid[x, y] switch
            {
                CellState.Open     => C_OPEN,
                CellState.Entrance => C_ENTRANCE,
                CellState.Exit     => C_EXIT,
                _ => C_WALL
            };
    }

    EditorGUI.DrawRect(r, bg);
        DrawOutline(r, C_GRID, 1);

        // Hover highlight
        if (r.Contains(mouse)) EditorGUI.DrawRect(r, C_HOVER);

        // Tile-type hint label (only when cells are large enough to read)
        if (grid[x, y] != CellState.Wall && cellSize >= 14)
        {
            bool special = grid[x, y] != CellState.Open;
    string lbl = grid[x, y] == CellState.Entrance ? "IN"
                   : grid[x, y] == CellState.Exit ? "OUT"
                   : TileHint(x, y);

    GUI.Label(r, lbl, new GUIStyle(EditorStyles.miniLabel)
    {
        fontSize = Mathf.Clamp(cellSize / 3, 6, 9),
                alignment = special ? TextAnchor.MiddleCenter : TextAnchor.LowerRight,
                fontStyle = special ? FontStyle.Bold : FontStyle.Normal,
                normal = { textColor = new Color(0f, 0f, 0f, special ? 0.55f : 0.38f) }
    });
        }

        // Room index label in the top-left corner (dark text on solid room color)
        if (roomGrid != null && roomGrid[x, y] >= 0 && cellSize >= 14)
        {
            GUI.Label(r, $"R{roomGrid[x, y]}", new GUIStyle(EditorStyles.miniLabel)
{
    fontSize = Mathf.Clamp(cellSize / 3, 6, 9),
                alignment = TextAnchor.UpperLeft,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0f, 0f, 0f, 0.50f) }
});
        }
    }

    // Apply the currently selected paint mode to the cell under the mouse
    void ApplyPaint(Rect gridRect, Vector2 mouse)
{
    if (!TryGetCell(gridRect, mouse, out int x, out int y)) return;

    grid[x, y] = isErasing ? CellState.Wall : paintMode switch
        {
            PaintMode.Open     => CellState.Open,
            PaintMode.Wall     => CellState.Wall,
            PaintMode.Entrance => CellState.Entrance,
            PaintMode.Exit     => CellState.Exit,
            _ => CellState.Wall
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Stats bar
    // ─────────────────────────────────────────────────────────────────────────
    void DrawStats()
{
    if (grid == null) return;

    int cOpen = 0, cWall = 0, cEnt = 0, cExit = 0, cRoom = 0;
    for (int x = 0; x < gridW; x++)
        for (int y = 0; y < gridH; y++)
        {
            switch (grid[x, y])
            {
                case CellState.Open: cOpen++; break;
                case CellState.Wall: cWall++; break;
                case CellState.Entrance: cEnt++; break;
                case CellState.Exit: cExit++; break;
            }
            if (roomGrid != null && roomGrid[x, y] >= 0) cRoom++;
        }

    GUILayout.Label(
        $"Open: {cOpen}   Wall: {cWall}   Entrance: {cEnt}   Exit: {cExit}" +
        $"   Room cells: {cRoom}   Tiles to build: {cOpen + cEnt + cExit}",
        new GUIStyle(EditorStyles.miniLabel)
        { alignment = TextAnchor.MiddleCenter, wordWrap = true });
}

// ─────────────────────────────────────────────────────────────────────────
// Build settings panel
// ─────────────────────────────────────────────────────────────────────────
void DrawBuildSettings()
{
    EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
    EditorGUILayout.LabelField("Build Settings", EditorStyles.boldLabel);

    buildOrigin = EditorGUILayout.Vector3Field("Origin:", buildOrigin);
    floorMat = (Material)EditorGUILayout.ObjectField("Floor:", floorMat, typeof(Material), false);
    wallMat = (Material)EditorGUILayout.ObjectField("Wall:", wallMat, typeof(Material), false);
    ceilingMat = (Material)EditorGUILayout.ObjectField("Ceiling Mat:", ceilingMat, typeof(Material), false);
    flagSprite = DrawCompactSprite("Flag Sprite:", flagSprite);
    signSprite = DrawCompactSprite("Sign Sprite:", signSprite);
    containsCeiling = EditorGUILayout.Toggle("Include Ceiling:", containsCeiling);
    buildParent = (Transform)EditorGUILayout.ObjectField("Parent:", buildParent, typeof(Transform), true);

    GUILayout.Space(4);

    GUI.enabled = grid != null && !busy && HasTiles();
    if (GUILayout.Button("Build Layout",
            new GUIStyle(GUI.skin.button) { fontSize = 13, fontStyle = FontStyle.Bold },
            GUILayout.Height(32)))
        Build();
    GUI.enabled = true;

    if (grid != null && !HasTiles())
        EditorGUILayout.HelpBox("Paint some open cells before building.", MessageType.Info);
}

Sprite DrawCompactSprite(string label, Sprite current)
{
    var rect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);
    return (Sprite)EditorGUI.ObjectField(rect, label, current, typeof(Sprite), false);
}

// ─────────────────────────────────────────────────────────────────────────
// Save / Load panel
// ─────────────────────────────────────────────────────────────────────────
void DrawSaveLoad()
{
    EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

    // Collapsible header
    EditorGUILayout.BeginHorizontal();
    EditorGUILayout.LabelField("Save / Load", EditorStyles.boldLabel, GUILayout.Width(90));
    if (GUILayout.Button(showSaveLoad ? "Hide" : "Show", GUILayout.Width(72), GUILayout.Height(18)))
        showSaveLoad = !showSaveLoad;
    EditorGUILayout.EndHorizontal();

    if (!showSaveLoad) return;

    // ── Save row ──────────────────────────────────────────────────────────
    EditorGUILayout.BeginHorizontal();
    GUILayout.Label("Slot name:", GUILayout.Width(70));
    saveSlotName = EditorGUILayout.TextField(saveSlotName);
    GUI.enabled = grid != null;
    if (GUILayout.Button("Save", GUILayout.Width(54), GUILayout.Height(20)))
        SaveLayout(saveSlotName);
    GUI.enabled = true;
    EditorGUILayout.EndHorizontal();

    GUILayout.Space(4);

    // ── Slot list ─────────────────────────────────────────────────────────
    var slots = GetSavedSlotNames();
    if (slots.Count == 0)
    {
        EditorGUILayout.HelpBox("No saved layouts yet.", MessageType.None);
        return;
    }

    saveScroll = EditorGUILayout.BeginScrollView(
        saveScroll, GUILayout.Height(Mathf.Min(slots.Count * 26 + 8, 160)));

    foreach (var slot in slots)
    {
        EditorGUILayout.BeginHorizontal();

        var data = LoadDataFromPrefs(slot);
        string lbl = data != null
            ? $"{slot}  ({data.width}x{data.height})  {data.savedAt}"
            : slot;
        GUILayout.Label(lbl, new GUIStyle(EditorStyles.miniLabel) { wordWrap = false });

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Load", GUILayout.Width(46), GUILayout.Height(18)))
        {
            if (grid == null || EditorUtility.DisplayDialog("Load Layout",
                    $"Replace current grid with \"{slot}\"?", "Yes", "Cancel"))
                LoadLayout(slot);
        }

        var oldBg = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.75f, 0.25f, 0.25f, 1f);
        if (GUILayout.Button("X", GUILayout.Width(22), GUILayout.Height(18)))
        {
            if (EditorUtility.DisplayDialog("Delete", $"Delete \"{slot}\"?", "Yes", "Cancel"))
                DeleteLayout(slot);
        }
        GUI.backgroundColor = oldBg;

        EditorGUILayout.EndHorizontal();
    }

    EditorGUILayout.EndScrollView();
}

// Serialize current grid + roomGrid to JSON and store in EditorPrefs
void SaveLayout(string slotName)
{
    if (string.IsNullOrWhiteSpace(slotName)) slotName = "Layout_1";
    slotName = slotName.Trim();

    EnsureRoomGrid();

    var data = new GridPainterSaveData
    {
        name = slotName,
        width = gridW,
        height = gridH,
        cells = new int[gridW * gridH],
        rooms = new int[gridW * gridH],
        savedAt = System.DateTime.Now.ToString("MM/dd HH:mm")
    };

    int roomCellCount = 0;
    for (int x = 0; x < gridW; x++)
    {
        for (int y = 0; y < gridH; y++)
        {
            int idx = x * gridH + y;
            data.cells[idx] = (int)grid[x, y];
            data.rooms[idx] = roomGrid[x, y];
            if (roomGrid[x, y] >= 0) roomCellCount++;
        }
    }

    string json = JsonUtility.ToJson(data);
    EditorPrefs.SetString("GridPainter_Slot_" + slotName, json);

    var names = GetSavedSlotNames();
    if (!names.Contains(slotName)) names.Add(slotName);
    EditorPrefs.SetString(PREFS_INDEX_KEY, string.Join("|", names));

    Debug.Log($"[GridPainter] SaveLayout: saved \"{slotName}\" ({gridW}x{gridH}), room cells: {roomCellCount}.");
    Repaint();
}

// Deserialize a slot from EditorPrefs and restore the grid
void LoadLayout(string slotName)
{
    var data = LoadDataFromPrefs(slotName);
    if (data == null)
    {
        Debug.LogWarning($"[GridPainter] LoadLayout: slot \"{slotName}\" not found.");
        return;
    }

    gridW = data.width;
    gridH = data.height;
    pendingW = gridW;
    pendingH = gridH;
    grid = new CellState[gridW, gridH];
    roomGrid = new int[gridW, gridH];

    int roomCellCount = 0;
    for (int x = 0; x < gridW; x++)
    {
        for (int y = 0; y < gridH; y++)
        {
            int idx = x * gridH + y;
            grid[x, y] = (CellState)data.cells[idx];
            roomGrid[x, y] = (data.rooms != null && data.rooms.Length == data.cells.Length)
                              ? data.rooms[idx] : -1;
            if (roomGrid[x, y] >= 0) roomCellCount++;
        }
    }

    // Auto-open wall cells that have a room assignment.
    // Legacy saves painted rooms over walls — this repairs that data on load.
    int autoOpened = 0;
    for (int x = 0; x < gridW; x++)
    {
        for (int y = 0; y < gridH; y++)
        {
            if (roomGrid[x, y] >= 0 && grid[x, y] == CellState.Wall)
            {
                grid[x, y] = CellState.Open;
                autoOpened++;
            }
        }
    }

    if (autoOpened > 0)
        Debug.Log($"[GridPainter] LoadLayout: auto-opened {autoOpened} wall cells that had room assignments.");

    Debug.Log($"[GridPainter] LoadLayout: loaded \"{slotName}\" ({gridW}x{gridH}), room cells: {roomCellCount}.");

    // Immediately sync serialized flat arrays so a recompile doesn't
    // overwrite this freshly loaded data with stale state
    SerializeGridToFlat();

    Repaint();
}

void DeleteLayout(string slotName)
{
    EditorPrefs.DeleteKey("GridPainter_Slot_" + slotName);
    var names = GetSavedSlotNames();
    names.Remove(slotName);
    EditorPrefs.SetString(PREFS_INDEX_KEY, string.Join("|", names));
    Debug.Log($"[GridPainter] DeleteLayout: deleted \"{slotName}\".");
    Repaint();
}

GridPainterSaveData LoadDataFromPrefs(string slotName)
{
    string key = "GridPainter_Slot_" + slotName;
    if (!EditorPrefs.HasKey(key)) return null;
    return JsonUtility.FromJson<GridPainterSaveData>(EditorPrefs.GetString(key));
}

System.Collections.Generic.List<string> GetSavedSlotNames()
{
    string raw = EditorPrefs.GetString(PREFS_INDEX_KEY, "");
    var list = new System.Collections.Generic.List<string>();
    if (!string.IsNullOrEmpty(raw))
        foreach (var s in raw.Split('|'))
            if (!string.IsNullOrEmpty(s)) list.Add(s);
    return list;
}

// ─────────────────────────────────────────────────────────────────────────
// Tile type resolution
// ─────────────────────────────────────────────────────────────────────────

// Determines which prefab variant to use for a cell based on its
// open/wall neighbours (N/E/S/W only — no diagonals).
TileInfo GetTileInfo(int x, int y)
{
    bool n = IsPassage(x, y + 1);
    bool e = IsPassage(x + 1, y);
    bool s = IsPassage(x, y - 1);
    bool w = IsPassage(x - 1, y);

    int count = (n ? 1 : 0) + (e ? 1 : 0) + (s ? 1 : 0) + (w ? 1 : 0);

    switch (count)
    {
        case 4: return new TileInfo { type = TileType.Open };

        case 3:
            int blocked = !n ? 0 : !e ? 1 : !s ? 2 : 3;
            return new TileInfo { type = TileType.Single, index = blocked };

        case 2:
            if (n && s) return new TileInfo { type = TileType.Straight, index = 0 };
            if (e && w) return new TileInfo { type = TileType.Straight, index = 1 };
            if (n && e) return new TileInfo { type = TileType.Corner, index = 0 };
            if (s && e) return new TileInfo { type = TileType.Corner, index = 1 };
            if (s && w) return new TileInfo { type = TileType.Corner, index = 2 };
            if (n && w) return new TileInfo { type = TileType.Corner, index = 3 };
            break;

        case 1:
            int openDir = s ? 0 : w ? 1 : n ? 2 : 3;
            return new TileInfo { type = TileType.End, index = openDir };

        case 0: return new TileInfo { type = TileType.Full };
    }

    return new TileInfo { type = TileType.Open };
}

// Returns true if (x,y) is in bounds AND is not a wall
bool IsPassage(int x, int y)
{
    if (x < 0 || x >= gridW || y < 0 || y >= gridH) return false;
    if (grid[x, y] == CellState.Wall) return false;
    // Room cells are physically enclosed — treat them as solid from hallway perspective
    if (roomGrid[x, y] >= 0) return false;
    return true;
}

// Short label displayed inside a cell when zoom is large enough
string TileHint(int x, int y)
{
    var t = GetTileInfo(x, y);
    return t.type switch
        {
            TileType.Open     => "O",
            TileType.Single   => $"S{t.index}",
            TileType.Straight => $"St{t.index}",
            TileType.Corner   => $"C{t.index}",
            TileType.End      => $"E{t.index}",
            TileType.Full     => "F",
            _ => "?"
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // EnsureRoomGrid
    // ─────────────────────────────────────────────────────────────────────────
    // Guarantees roomGrid is non-null and matches the current grid dimensions.
    // If stale/null, re-allocates and carries over data that still fits.
    void EnsureRoomGrid()
{
    if (roomGrid != null
        && roomGrid.GetLength(0) == gridW
        && roomGrid.GetLength(1) == gridH)
        return; // already valid

    Debug.LogWarning($"[GridPainter] EnsureRoomGrid: roomGrid was " +
        $"{(roomGrid == null ? "null" : $"{roomGrid.GetLength(0)}x{roomGrid.GetLength(1)}")} " +
        $"vs expected {gridW}x{gridH}. Re-allocating.");

    var old = roomGrid;
    roomGrid = new int[gridW, gridH];
    int recovered = 0;

    for (int x = 0; x < gridW; x++)
    {
        for (int y = 0; y < gridH; y++)
        {
            if (old != null && x < old.GetLength(0) && y < old.GetLength(1))
            {
                roomGrid[x, y] = old[x, y];
                if (old[x, y] >= 0) recovered++;
            }
            else
            {
                roomGrid[x, y] = -1;
            }
        }
    }

    Debug.Log($"[GridPainter] EnsureRoomGrid: recovered {recovered} room-cell assignments.");
}

// ─────────────────────────────────────────────────────────────────────────
// Build
// ─────────────────────────────────────────────────────────────────────────
void Build()
{
    busy = true;
    SerializeGridToFlat();
    EnsureRoomGrid();

    // Pre-build census
    int preRoomCount = 0;
    for (int x = 0; x < gridW; x++)
        for (int y = 0; y < gridH; y++)
            if (roomGrid[x, y] >= 0) preRoomCount++;
    Debug.Log($"[GridPainter] Build starting — {preRoomCount} room-cell assignments.");

    var root = new GameObject("GridPainter_Build");
    root.transform.parent = buildParent;

    // ── Detect separate contiguous room blobs (flood-fill) ────────────────
    // A single room index may be painted in multiple disconnected rectangles.
    // Each contiguous blob becomes its own Room_{idx}_#{blobNumber} group.
    var visited = new bool[gridW, gridH];
    var roomBlobs = new System.Collections.Generic.List<(int roomIdx, int x0, int y0, int x1, int y1)>();

    for (int sx = 0; sx < gridW; sx++)
    {
        for (int sy = 0; sy < gridH; sy++)
        {
            int r = roomGrid[sx, sy];
            if (r < 0 || visited[sx, sy]) continue;

            // BFS flood-fill to find all connected cells of this room index
            int bx0 = sx, by0 = sy, bx1 = sx, by1 = sy;
            var queue = new System.Collections.Generic.Queue<Vector2Int>();
            queue.Enqueue(new Vector2Int(sx, sy));
            visited[sx, sy] = true;

            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                bx0 = Mathf.Min(bx0, cell.x); by0 = Mathf.Min(by0, cell.y);
                bx1 = Mathf.Max(bx1, cell.x); by1 = Mathf.Max(by1, cell.y);

                Vector2Int[] neighbours = {
                    new Vector2Int(cell.x + 1, cell.y),
                    new Vector2Int(cell.x - 1, cell.y),
                    new Vector2Int(cell.x, cell.y + 1),
                    new Vector2Int(cell.x, cell.y - 1)
                };
                foreach (var n in neighbours)
                {
                    if (n.x < 0 || n.x >= gridW || n.y < 0 || n.y >= gridH) continue;
                    if (visited[n.x, n.y]) continue;
                    if (roomGrid[n.x, n.y] != r) continue;
                    visited[n.x, n.y] = true;
                    queue.Enqueue(n);
                }
            }

            roomBlobs.Add((r, bx0, by0, bx1, by1));
            Debug.Log($"[GridPainter] Detected blob: Room_{r} at ({bx0},{by0})-({bx1},{by1}), " +
                      $"size {bx1 - bx0 + 1}x{by1 - by0 + 1}.");
        }
    }

    // ── Build each blob as a proper enclosed room ─────────────────────────
    var roomBlobCount = new System.Collections.Generic.Dictionary<int, int>();
    foreach (var blob in roomBlobs)
    {
        int roomIdx = blob.roomIdx;
        roomBlobCount.TryGetValue(roomIdx, out int blobNum);
        roomBlobCount[roomIdx] = blobNum + 1;

        int roomW = blob.x1 - blob.x0 + 1;
        int roomH = blob.y1 - blob.y0 + 1;
        Vector3 roomOrigin = buildOrigin + new Vector3(blob.x0 * 10f, 0f, blob.y0 * 10f);

        BuildRoomZone(roomIdx, blobNum, roomW, roomH, roomOrigin, root.transform);
    }

    // ── Build hallway tiles (cells with no room assignment) ───────────────
    Transform hallwaysParent = null;
    int placed = 0;

    for (int x = 0; x < gridW; x++)
    {
        for (int y = 0; y < gridH; y++)
        {
            // Skip room cells — already handled by BuildRoomZone above
            if (roomGrid[x, y] >= 0) continue;

            CellState state = grid[x, y];
            if (state == CellState.Wall) continue;

            TileInfo info = GetTileInfo(x, y);
            string path = TilePath(info);
            Vector3 pos = buildOrigin + new Vector3(x * 10f, 0f, y * 10f);

            var prefab = Resources.Load<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning($"[GridPainter] Missing prefab: Resources/{path} " +
                                 $"cell ({x},{y}) type={info.type} index={info.index}. Skipped.");
                continue;
            }

            if (hallwaysParent == null)
            {
                var hgo = new GameObject("Hallways");
                hgo.transform.parent = root.transform;
                Undo.RegisterCreatedObjectUndo(hgo, "Grid Painter Build");
                hallwaysParent = hgo.transform;
                Debug.Log("[GridPainter] Created Hallways group.");
            }

            var tile = UnityEngine.Object.Instantiate(prefab, pos, Quaternion.identity, hallwaysParent);
            tile.name = $"Tile_{x}_{y}_{info.type}{info.index}";

            if (!containsCeiling) RemoveCeiling(tile);
            ApplyMaterials(tile);

            if (state == CellState.Entrance)
            {
                var signPrefab = TheBCMHT_Helper.getCornMazeSign();
                if (signPrefab != null)
                {
                    var sign = UnityEngine.Object.Instantiate(signPrefab, pos, Quaternion.identity, hallwaysParent);
                    sign.name = $"Entrance_{x}_{y}";
                    var sr = sign.GetComponentInChildren<SpriteRenderer>();
                    if (sr != null && signSprite != null) sr.sprite = signSprite;
                }
            }

            if (state == CellState.Exit)
            {
                var flagPrefab = TheBCMHT_Helper.getCornMazeFlag();
                if (flagPrefab != null)
                {
                    var flag = UnityEngine.Object.Instantiate(flagPrefab, pos, Quaternion.identity, hallwaysParent);
                    flag.name = $"Exit_{x}_{y}";
                    var sr = flag.GetComponentInChildren<SpriteRenderer>();
                    if (sr != null && flagSprite != null) sr.sprite = flagSprite;
                }
            }

            placed++;
        }
    }

    Undo.RegisterCreatedObjectUndo(root, "Grid Painter Build");
    Debug.Log($"[GridPainter] Build complete — {roomBlobs.Count} room blob(s) + " +
              $"{placed} hallway tiles -> '{root.name}'.");
    busy = false;
}

void BuildRoomZone(int roomIdx, int blobNum, int roomW, int roomH, Vector3 origin, Transform root)
{
    if (roomW < 2 || roomH < 2)
    {
        Debug.LogWarning($"[GridPainter] BuildRoomZone: Room_{roomIdx}#{blobNum} is {roomW}x{roomH} " +
                         $"— minimum is 2x2. Skipped.");
        return;
    }

    RoomChildDat[,] dats = TheBCMHT_Helper.createRoomData(roomW, roomH);
    if (dats == null)
    {
        Debug.LogWarning($"[GridPainter] BuildRoomZone: createRoomData returned null for Room_{roomIdx}#{blobNum}.");
        return;
    }

    // Name: Room_0 if only one blob, Room_0_1 / Room_0_2 etc. if multiple
    string goName = blobNum == 0 ? $"Room_{roomIdx}" : $"Room_{roomIdx}_{blobNum}";
    var rgo = new GameObject(goName);
    rgo.transform.parent = root;
    Undo.RegisterCreatedObjectUndo(rgo, "Grid Painter Build");

    for (int x = 0; x < dats.GetLength(0); x++)
    {
        for (int y = 0; y < dats.GetLength(1); y++)
        {
            string path = dats[x, y].tileType switch
            {
                TileType.Corner => $"Tiles/Corner_{dats[x, y].tileIndex}",
                TileType.Single => $"Tiles/Single_{dats[x, y].tileIndex}",
                TileType.Open   => "Tiles/Open",
                _ => "Tiles/Open"
            };

        var prefab = Resources.Load<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogWarning($"[GridPainter] BuildRoomZone: missing prefab {path} " +
                             $"{goName} tile ({x},{y}). Skipped.");
            continue;
        }

        Vector3 pos = origin + new Vector3(x * 10f, 0f, y * 10f);
        var tile = UnityEngine.Object.Instantiate(prefab, pos, Quaternion.identity, rgo.transform);
        tile.name = $"{goName}_Tile_{x}_{y}_{dats[x, y].tileType}{dats[x, y].tileIndex}";

        if (!containsCeiling) RemoveCeiling(tile);
        ApplyMaterials(tile);
    }
}

Debug.Log($"[GridPainter] BuildRoomZone: {goName} built at {origin}, {roomW}x{roomH}.");
}

// Maps a TileInfo to a Resources/ path
string TilePath(TileInfo info) => info.type switch
    {
        TileType.Open     => "Tiles/Open",
        TileType.Full     => "Tiles/Full",
        TileType.Single   => $"Tiles/Single_{info.index}",
        TileType.Straight => $"Tiles/Straight_{info.index}",
        TileType.Corner   => $"Tiles/Corner_{info.index}",
        TileType.End      => $"Tiles/End_{info.index}",
        _                 => "Tiles/Open"
    };

    // Destroys any child named "*Ceiling*"
    void RemoveCeiling(GameObject tile)
{
    foreach (Transform t in tile.GetComponentsInChildren<Transform>())
        if (t != null && t.gameObject.name.Contains("Ceiling"))
            UnityEngine.Object.DestroyImmediate(t.gameObject);
}

// Assigns floor / wall / ceiling materials by matching child object names
void ApplyMaterials(GameObject tile)
{
    foreach (MeshRenderer mr in tile.GetComponentsInChildren<MeshRenderer>())
    {
        string n = mr.gameObject.name;
        if (n.Contains("Floor")) mr.sharedMaterial = floorMat;
        else if (n.Contains("Wall")) mr.sharedMaterial = wallMat;
        else if (n.Contains("Ceiling")) mr.sharedMaterial = ceilingMat;
    }
}

// ─────────────────────────────────────────────────────────────────────────
// Coordinate helpers
// ─────────────────────────────────────────────────────────────────────────

// Returns the screen-space Rect for grid cell (x, y).
// Y is flipped so row 0 sits at the bottom of the canvas.
Rect CellRect(Rect gridRect, int x, int y)
{
    float px = gridRect.x + PAD + x * cellSize;
    float py = gridRect.y + PAD + (gridH - 1 - y) * cellSize;
    return new Rect(px, py, cellSize, cellSize);
}

// Converts a screen-space mouse position to grid cell coordinates.
// Returns false if the mouse is outside the grid area.
bool TryGetCell(Rect gridRect, Vector2 mouse, out int cx, out int cy)
{
    cx = Mathf.FloorToInt((mouse.x - gridRect.x - PAD) / cellSize);
    cy = gridH - 1 - Mathf.FloorToInt((mouse.y - gridRect.y - PAD) / cellSize);
    return cx >= 0 && cx < gridW && cy >= 0 && cy < gridH;
}

// ─────────────────────────────────────────────────────────────────────────
// Grid initialisation / utility
// ─────────────────────────────────────────────────────────────────────────

// Creates fresh grid and roomGrid arrays (all walls / all -1)
void InitGrid()
{
    grid = new CellState[gridW, gridH]; // defaults to CellState.Wall (0)
    roomGrid = new int[gridW, gridH];
    for (int x = 0; x < gridW; x++)
        for (int y = 0; y < gridH; y++)
            roomGrid[x, y] = -1;
    Debug.Log($"[GridPainter] InitGrid: created {gridW}x{gridH} grid.");
    Repaint();
}

// Resets every cell's room assignment to -1
void ClearRooms()
{
    for (int x = 0; x < gridW; x++)
        for (int y = 0; y < gridH; y++)
            roomGrid[x, y] = -1;
    Debug.Log("[GridPainter] ClearRooms: all room assignments cleared.");
    Repaint();
}

// Sets every cell to the given state
void FillAll(CellState state)
{
    for (int x = 0; x < gridW; x++)
        for (int y = 0; y < gridH; y++)
            grid[x, y] = state;
    Repaint();
}

// Returns true if at least one non-wall cell exists (Build requires this)
bool HasTiles()
{
    for (int x = 0; x < gridW; x++)
        for (int y = 0; y < gridH; y++)
            if (grid[x, y] != CellState.Wall) return true;
    return false;
}

// Draws a 1-pixel border around a rect using four thin DrawRect calls
static void DrawOutline(Rect r, Color c, float t)
{
    EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, t), c);
    EditorGUI.DrawRect(new Rect(r.x, r.yMax - t, r.width, t), c);
    EditorGUI.DrawRect(new Rect(r.x, r.y, t, r.height), c);
    EditorGUI.DrawRect(new Rect(r.xMax - t, r.y, t, r.height), c);
}
}