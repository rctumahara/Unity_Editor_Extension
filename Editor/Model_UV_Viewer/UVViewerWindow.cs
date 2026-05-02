using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// UV展開ビューワー + 3Dプレビュー + マテリアルプレビュー 統合エディター拡張
/// メニュー: Window > UV & 3D Viewer
/// レイアウト: [サイドパネル] | [UVビュー] | [3Dプレビュー]
/// </summary>
public class UVViewerWindow : EditorWindow
{
    // ================================================================
    //  共通
    // ================================================================
    private GameObject _targetObject;
    private Mesh       _mesh;
    private Material   _previewMaterial;

    // ================================================================
    //  UV ビュー
    // ================================================================
    private Texture2D  _uvTexture;
    private Texture2D  _checkerTexture;
    private int        _uvChannel  = 0;
    private readonly string[] _uvChannelNames = { "UV0", "UV1", "UV2", "UV3" };
    private bool  _showGrid      = true;
    private bool  _showWireframe = true;
    private bool  _showMaterial  = true;
    private Color _wireColor     = new Color(0.2f, 0.9f, 0.5f, 1f);
    private float _wireAlpha     = 0.85f;
    private float _uvZoom        = 1f;
    private Vector2 _uvPan       = Vector2.zero;
    private Vector2 _uvDragStart;
    private bool   _uvIsDragging;
    private Rect   _uvViewRect;
    private int    _uvTexSize    = 512;
    private bool   _uvDirty      = true;

    // ================================================================
    //  3D プレビュー
    // ================================================================
    private PreviewRenderUtility _previewUtil;
    private bool   _3dShowWireframe  = false;
    private bool   _3dShowNormals    = false;
    private bool   _3dAutoRotate     = true;
    private float  _3dRotX           = 20f;
    private float  _3dRotY           = 0f;
    private float  _3dZoom           = 3f;
    private Vector2 _3dDragStart;
    private bool   _3dIsDragging;
    private Rect   _3dViewRect;
    private double _lastTime;
    private Material _wireframeMat;

    // ================================================================
    //  サブメッシュ / レイアウト
    // ================================================================
    private int    _selectedSubMesh = -1;
    private bool   _subMeshFoldout  = true;
    private Vector2 _sideScroll;

    // タブ: 0=両方, 1=UVのみ, 2=3Dのみ
    private int _layoutMode = 0;
    private readonly string[] _layoutNames = { "UV + 3D", "UV Only", "3D Only" };

    // ================================================================
    //  メニュー登録
    // ================================================================
    [MenuItem("Window/UV & 3D Viewer")]
    public static void ShowWindow()
    {
        var w = GetWindow<UVViewerWindow>("UV & 3D Viewer");
        w.minSize = new Vector2(800, 520);
        w.Show();
    }

    // ================================================================
    //  初期化 / 解放
    // ================================================================
    private void OnEnable()
    {
        _previewUtil = new PreviewRenderUtility();
        _previewUtil.camera.backgroundColor = new Color(0.1f, 0.1f, 0.12f, 1f);
        _previewUtil.camera.clearFlags      = CameraClearFlags.SolidColor;
        _previewUtil.camera.nearClipPlane   = 0.01f;
        _previewUtil.camera.farClipPlane    = 1000f;
        _lastTime = EditorApplication.timeSinceStartup;
    }

    private void OnDisable()
    {
        _previewUtil?.Cleanup();
        _previewUtil = null;
        if (_uvTexture      != null) DestroyImmediate(_uvTexture);
        if (_checkerTexture != null) DestroyImmediate(_checkerTexture);
        if (_wireframeMat   != null) DestroyImmediate(_wireframeMat);
    }

    // ================================================================
    //  GUI エントリ
    // ================================================================
    private void OnGUI()
    {
        // 自動回転タイマー
        if (_3dAutoRotate && _layoutMode != 1)
        {
            double now   = EditorApplication.timeSinceStartup;
            float  delta = (float)(now - _lastTime);
            _lastTime    = now;
            _3dRotY     += delta * 30f;
            Repaint();
        }
        else
        {
            _lastTime = EditorApplication.timeSinceStartup;
        }

        DrawToolbar();

        EditorGUILayout.BeginHorizontal();
        DrawSidePanel();
        DrawMainArea();
        EditorGUILayout.EndHorizontal();

        DrawStatusBar();
    }

    // ================================================================
    //  ツールバー
    // ================================================================
    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            GUILayout.Label("UV & 3D Viewer", EditorStyles.boldLabel, GUILayout.Width(110));
            GUILayout.FlexibleSpace();

            // レイアウト切替
            int newLayout = GUILayout.Toolbar(_layoutMode, _layoutNames,
                EditorStyles.toolbarButton, GUILayout.Width(180));
            if (newLayout != _layoutMode) _layoutMode = newLayout;

            GUILayout.Space(10);

            // UV チャンネル（UVビュー表示時のみ）
            if (_layoutMode != 2)
            {
                GUILayout.Label("CH:", EditorStyles.toolbarButton, GUILayout.Width(26));
                int newCh = EditorGUILayout.Popup(_uvChannel, _uvChannelNames,
                    EditorStyles.toolbarPopup, GUILayout.Width(44));
                if (newCh != _uvChannel) { _uvChannel = newCh; _uvDirty = true; }

                int[] res   = { 256, 512, 1024, 2048 };
                string[] rn = { "256", "512", "1K", "2K" };
                int ci = System.Array.IndexOf(res, _uvTexSize); if (ci < 0) ci = 1;
                int ni = EditorGUILayout.Popup(ci, rn, EditorStyles.toolbarPopup, GUILayout.Width(36));
                if (ni != ci) { _uvTexSize = res[ni]; _uvDirty = true; }

                _showGrid      = GUILayout.Toggle(_showGrid,      "Grid", EditorStyles.toolbarButton, GUILayout.Width(36));
                _showWireframe = GUILayout.Toggle(_showWireframe, "Wire", EditorStyles.toolbarButton, GUILayout.Width(32));
                _showMaterial  = GUILayout.Toggle(_showMaterial,  "Mat",  EditorStyles.toolbarButton, GUILayout.Width(30));
                GUILayout.Space(6);
            }

            // 3D オプション（3Dビュー表示時のみ）
            if (_layoutMode != 1)
            {
                _3dAutoRotate    = GUILayout.Toggle(_3dAutoRotate,    "Auto",  EditorStyles.toolbarButton, GUILayout.Width(38));
                _3dShowWireframe = GUILayout.Toggle(_3dShowWireframe, "Wire",  EditorStyles.toolbarButton, GUILayout.Width(32));
                _3dShowNormals   = GUILayout.Toggle(_3dShowNormals,   "Norm",  EditorStyles.toolbarButton, GUILayout.Width(38));
                GUILayout.Space(6);
            }

            if (_layoutMode != 2 &&
                GUILayout.Button("Export UV", EditorStyles.toolbarButton, GUILayout.Width(66)))
                ExportUVTexture();

            if (GUILayout.Button("Reset", EditorStyles.toolbarButton, GUILayout.Width(44)))
            {
                _uvZoom = 1f; _uvPan = Vector2.zero;
                _3dRotX = 20f; _3dRotY = 0f; _3dZoom = 3f;
            }
        }
    }

    // ================================================================
    //  サイドパネル
    // ================================================================
    private void DrawSidePanel()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(190)))
        {
            _sideScroll = EditorGUILayout.BeginScrollView(_sideScroll);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);

            var newObj = (GameObject)EditorGUILayout.ObjectField(
                _targetObject, typeof(GameObject), true);
            if (newObj != _targetObject) { _targetObject = newObj; RefreshMesh(); }

            if (GUILayout.Button("Use Scene Selection"))
            {
                _targetObject = Selection.activeGameObject;
                RefreshMesh();
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Mesh Info", EditorStyles.boldLabel);
            if (_mesh != null)
            {
                EditorGUILayout.LabelField($"Name      : {_mesh.name}");
                EditorGUILayout.LabelField($"Vertices  : {_mesh.vertexCount}");
                EditorGUILayout.LabelField($"Triangles : {_mesh.triangles.Length / 3}");
                EditorGUILayout.LabelField($"SubMeshes : {_mesh.subMeshCount}");
                EditorGUILayout.LabelField($"Bounds    : {_mesh.bounds.size:F2}");

                _subMeshFoldout = EditorGUILayout.Foldout(_subMeshFoldout, "SubMeshes");
                if (_subMeshFoldout)
                {
                    EditorGUI.indentLevel++;
                    bool allSel = _selectedSubMesh == -1;
                    if (GUILayout.Toggle(allSel, "All", "Button", GUILayout.Height(18)) && !allSel)
                    { _selectedSubMesh = -1; _uvDirty = true; }
                    for (int i = 0; i < _mesh.subMeshCount; i++)
                    {
                        var d   = _mesh.GetSubMesh(i);
                        bool sel = _selectedSubMesh == i;
                        bool ns  = GUILayout.Toggle(sel, $" #{i}  tri:{d.indexCount/3}", "Button", GUILayout.Height(18));
                        if (ns && !sel) { _selectedSubMesh = i; _uvDirty = true; }
                    }
                    EditorGUI.indentLevel--;
                }
            }
            else
            {
                EditorGUILayout.HelpBox("GameObject を選択してください", MessageType.Info);
            }

            // UV 設定
            if (_layoutMode != 2)
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("UV Display", EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                _wireColor = EditorGUILayout.ColorField("Wire Color", _wireColor);
                if (EditorGUI.EndChangeCheck()) _uvDirty = true;
                _wireAlpha = EditorGUILayout.Slider("Wire Alpha", _wireAlpha, 0f, 1f);
            }

            // マテリアル
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Material", EditorStyles.boldLabel);
            var newMat = (Material)EditorGUILayout.ObjectField(
                _previewMaterial, typeof(Material), false);
            if (newMat != _previewMaterial) { _previewMaterial = newMat; _uvDirty = true; }

            // 3D 設定
            if (_layoutMode != 1)
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("3D View", EditorStyles.boldLabel);
                _3dZoom      = EditorGUILayout.Slider("Distance",  _3dZoom, 0.5f, 20f);
                _3dRotX      = EditorGUILayout.Slider("Pitch",     _3dRotX, -89f, 89f);
                _3dRotY      = EditorGUILayout.Slider("Yaw",       _3dRotY, -180f, 180f);
                _3dShowNormals    = EditorGUILayout.Toggle("Show Normals",   _3dShowNormals);
                _3dShowWireframe  = EditorGUILayout.Toggle("Show Wireframe", _3dShowWireframe);
                _3dAutoRotate     = EditorGUILayout.Toggle("Auto Rotate",    _3dAutoRotate);
            }

            EditorGUILayout.EndScrollView();
        }

        var lr = GUILayoutUtility.GetRect(1, position.height, GUILayout.Width(1));
        EditorGUI.DrawRect(lr, new Color(0.1f, 0.1f, 0.1f));
    }

    // ================================================================
    //  メインエリア
    // ================================================================
    private void DrawMainArea()
    {
        switch (_layoutMode)
        {
            case 0:
                DrawUVView();
                var sep = GUILayoutUtility.GetRect(1, position.height, GUILayout.Width(1));
                EditorGUI.DrawRect(sep, new Color(0.1f, 0.1f, 0.1f));
                Draw3DView();
                break;
            case 1: DrawUVView();  break;
            case 2: Draw3DView();  break;
        }
    }

    // ================================================================
    //  UV ビュー
    // ================================================================
    private void DrawUVView()
    {
        _uvViewRect = GUILayoutUtility.GetRect(
            GUIContent.none, GUIStyle.none,
            GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

        if (Event.current.type == EventType.Repaint)
        {
            EditorGUI.DrawRect(_uvViewRect, new Color(0.15f, 0.15f, 0.15f));

            if (_mesh != null)
            {
                if (_uvDirty) RebuildUVTexture();
                Rect uvR = GetUVDisplayRect(_uvViewRect, _uvZoom, _uvPan);

                if (_showMaterial && _previewMaterial != null)
                {
                    Texture mt = _previewMaterial.mainTexture;
                    if (mt != null) EditorGUI.DrawPreviewTexture(uvR, mt);
                    else            DrawCheckerBoard(uvR);
                }
                else DrawCheckerBoard(uvR);

                if (_showGrid) DrawGrid(uvR);

                if (_showWireframe && _uvTexture != null)
                {
                    GUI.color = new Color(1, 1, 1, _wireAlpha);
                    GUI.DrawTexture(uvR, _uvTexture, ScaleMode.StretchToFill,
                        true, 0, _wireColor, 0, 0);
                    GUI.color = Color.white;
                }

                DrawRectOutline(uvR, new Color(0.8f,0.8f,0.8f,0.8f), 1f);

                var ls = new GUIStyle(EditorStyles.miniLabel)
                    { normal = { textColor = new Color(0.7f,0.7f,0.7f) } };
                GUI.Label(new Rect(_uvViewRect.x+6, _uvViewRect.y+4, 120, 18),
                    $"UV {_uvChannelNames[_uvChannel]}", ls);
            }
            else
            {
                DrawEmptyMessage(_uvViewRect, "← GameObject を選択してください");
            }
        }

        HandleUVInput();
    }

    private void HandleUVInput()
    {
        var e = Event.current;
        if (!_uvViewRect.Contains(e.mousePosition)) return;

        if (e.type == EventType.ScrollWheel)
        { _uvZoom = Mathf.Clamp(_uvZoom*(1f-e.delta.y*0.05f),0.1f,10f); Repaint(); e.Use(); }
        if (e.type == EventType.MouseDown && (e.button==1||e.button==2))
        { _uvDragStart = e.mousePosition; _uvIsDragging = true; e.Use(); }
        if (e.type == EventType.MouseDrag && _uvIsDragging)
        { _uvPan += e.mousePosition-_uvDragStart; _uvDragStart=e.mousePosition; Repaint(); e.Use(); }
        if (e.type == EventType.MouseUp) _uvIsDragging = false;
    }

    // ================================================================
    //  3D ビュー
    // ================================================================
    private void Draw3DView()
    {
        _3dViewRect = GUILayoutUtility.GetRect(
            GUIContent.none, GUIStyle.none,
            GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

        if (_previewUtil == null) return;

        Handle3DInput();

        if (Event.current.type == EventType.Repaint)
        {
            EditorGUI.DrawRect(_3dViewRect, new Color(0.1f,0.1f,0.12f));

            if (_mesh != null)
            {
                Render3DPreview(_3dViewRect);

                // ラベル (左上)
                var ls = new GUIStyle(EditorStyles.miniLabel)
                    { normal = { textColor = new Color(0.7f,0.7f,0.7f) } };
                string matName = _previewMaterial != null ? _previewMaterial.name : "No Material";
                GUI.Label(new Rect(_3dViewRect.x+6, _3dViewRect.y+4, 220, 18),
                    $"3D Preview  |  {matName}", ls);

                // 操作ヒント (右下)
                var hs = new GUIStyle(EditorStyles.miniLabel)
                    { normal={textColor=new Color(0.45f,0.45f,0.45f)},
                      alignment=TextAnchor.LowerRight };
                GUI.Label(new Rect(_3dViewRect.x, _3dViewRect.yMax-18,
                    _3dViewRect.width-4, 16),
                    "LMB: rotate  |  Scroll: zoom", hs);
            }
            else
            {
                DrawEmptyMessage(_3dViewRect, "3D プレビューにはメッシュが必要です");
            }
        }
    }

    private void Render3DPreview(Rect rect)
    {
        int w = Mathf.Max(1, (int)rect.width);
        int h = Mathf.Max(1, (int)rect.height);

        _previewUtil.BeginPreview(new Rect(0,0,w,h), GUIStyle.none);

        var cam = _previewUtil.camera;
        cam.fieldOfView = 30f;

        Vector3    center = _mesh.bounds.center;
        Quaternion rot    = Quaternion.Euler(_3dRotX, _3dRotY, 0f);
        float      dist   = _mesh.bounds.extents.magnitude * _3dZoom;
        cam.transform.position = center + rot * new Vector3(0,0,-dist);
        cam.transform.LookAt(center);

        _previewUtil.lights[0].intensity        = 1.2f;
        _previewUtil.lights[0].transform.rotation = Quaternion.Euler(40f,40f,0f);
        if (_previewUtil.lights.Length > 1)
        {
            _previewUtil.lights[1].intensity        = 0.5f;
            _previewUtil.lights[1].transform.rotation = Quaternion.Euler(-30f,-60f,0f);
        }

        // マテリアル (未設定時はデフォルト Standard)
        Material mat = _previewMaterial != null
            ? _previewMaterial
            : new Material(Shader.Find("Standard"));

        int subCount = _mesh.subMeshCount;
        for (int s = 0; s < subCount; s++)
        {
            if (_selectedSubMesh >= 0 && s != _selectedSubMesh) continue;
            _previewUtil.DrawMesh(_mesh, Matrix4x4.identity, mat, s);
        }

        // ワイヤーフレーム重ね描き
        if (_3dShowWireframe)
        {
            if (_wireframeMat == null)
            {
                _wireframeMat = new Material(Shader.Find("Hidden/Internal-Colored"))
                    { hideFlags = HideFlags.HideAndDontSave };
                _wireframeMat.SetColor("_Color", new Color(0.2f,0.9f,0.5f,0.5f));
                _wireframeMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                _wireframeMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                _wireframeMat.SetInt("_Cull",    (int)UnityEngine.Rendering.CullMode.Off);
                _wireframeMat.SetInt("_ZWrite",  0);
                _wireframeMat.SetInt("_ZTest",   (int)UnityEngine.Rendering.CompareFunction.LessEqual);
            }
            for (int s = 0; s < subCount; s++)
            {
                if (_selectedSubMesh >= 0 && s != _selectedSubMesh) continue;
                _previewUtil.DrawMesh(_mesh, Matrix4x4.identity, _wireframeMat, s);
            }
        }

        cam.Render();
        var tex = _previewUtil.EndPreview();
        GUI.DrawTexture(rect, tex, ScaleMode.StretchToFill, false);

        // 法線ギズモ (Handles は PreviewRenderUtility の外でオーバーレイ)
        if (_3dShowNormals && _mesh.normals != null && _mesh.normals.Length > 0)
            DrawNormalsOverlay(rect);
    }

    /// <summary>
    /// 法線を 3D ビュー上に GUI ラインでオーバーレイ表示（簡易投影）
    /// </summary>
    private void DrawNormalsOverlay(Rect rect)
    {
        var verts   = _mesh.vertices;
        var normals = _mesh.normals;

        // カメラ行列を再現
        Quaternion rot  = Quaternion.Euler(_3dRotX, _3dRotY, 0f);
        float      dist = _mesh.bounds.extents.magnitude * _3dZoom;
        Vector3    center = _mesh.bounds.center;
        Vector3    camPos = center + rot * new Vector3(0,0,-dist);
        Matrix4x4  view   = Matrix4x4.LookAt(camPos, center, Vector3.up);
        float      fov    = 30f;
        float      aspect = rect.width / Mathf.Max(1f, rect.height);
        Matrix4x4  proj   = Matrix4x4.Perspective(fov, aspect, 0.01f, 1000f);
        Matrix4x4  vp     = proj * view.inverse;

        float nLen = _mesh.bounds.extents.magnitude * 0.08f;

        Handles.BeginGUI();
        Handles.color = new Color(0.3f, 0.6f, 1f, 0.7f);

        int step = Mathf.Max(1, verts.Length / 300);
        for (int i = 0; i < verts.Length; i += step)
        {
            Vector3 wp0 = verts[i];
            Vector3 wp1 = verts[i] + normals[i] * nLen;

            Vector2 sp0 = WorldToScreen(wp0, vp, rect);
            Vector2 sp1 = WorldToScreen(wp1, vp, rect);
            if (sp0 == Vector2.zero || sp1 == Vector2.zero) continue;

            Handles.DrawLine(sp0, sp1);
        }
        Handles.color = Color.white;
        Handles.EndGUI();
    }

    private Vector2 WorldToScreen(Vector3 world, Matrix4x4 vp, Rect rect)
    {
        Vector4 clip = vp * new Vector4(world.x, world.y, world.z, 1f);
        if (Mathf.Abs(clip.w) < 0.0001f) return Vector2.zero;
        Vector3 ndc = new Vector3(clip.x/clip.w, clip.y/clip.w, clip.z/clip.w);
        if (ndc.z < -1f || ndc.z > 1f) return Vector2.zero;
        float sx = rect.x + (ndc.x * 0.5f + 0.5f) * rect.width;
        float sy = rect.y + (1f - (ndc.y * 0.5f + 0.5f)) * rect.height;
        return new Vector2(sx, sy);
    }

    private void Handle3DInput()
    {
        var e = Event.current;
        if (!_3dViewRect.Contains(e.mousePosition)) return;

        if (e.type == EventType.ScrollWheel)
        {
            _3dZoom = Mathf.Clamp(_3dZoom + e.delta.y * 0.15f, 0.5f, 20f);
            _3dAutoRotate = false;
            Repaint(); e.Use();
        }
        if (e.type == EventType.MouseDown && e.button == 0)
        { _3dDragStart = e.mousePosition; _3dIsDragging = true; _3dAutoRotate = false; e.Use(); }
        if (e.type == EventType.MouseDrag && _3dIsDragging && e.button == 0)
        {
            Vector2 delta = e.mousePosition - _3dDragStart;
            _3dRotY += delta.x * 0.5f;
            _3dRotX  = Mathf.Clamp(_3dRotX - delta.y * 0.5f, -89f, 89f);
            _3dDragStart = e.mousePosition;
            Repaint(); e.Use();
        }
        if (e.type == EventType.MouseUp) _3dIsDragging = false;
    }

    // ================================================================
    //  UV テクスチャ構築
    // ================================================================
    private void RebuildUVTexture()
    {
        _uvDirty = false;
        if (_mesh == null) return;
        Vector2[] uvs = GetUVChannel(_mesh, _uvChannel);
        if (uvs == null || uvs.Length == 0) return;

        int sz     = _uvTexSize;
        var pixels = new Color32[sz * sz];
        Color32 c  = new Color32(
            (byte)(_wireColor.r*255),(byte)(_wireColor.g*255),
            (byte)(_wireColor.b*255),(byte)(_wireAlpha*255));

        var idx = new List<int>();
        if (_selectedSubMesh < 0) idx.AddRange(_mesh.triangles);
        else                       idx.AddRange(_mesh.GetTriangles(_selectedSubMesh));

        for (int i = 0; i < idx.Count; i += 3)
        {
            int i0=idx[i],i1=idx[i+1],i2=idx[i+2];
            if (i0>=uvs.Length||i1>=uvs.Length||i2>=uvs.Length) continue;
            DrawLine(pixels,sz,uvs[i0],uvs[i1],c);
            DrawLine(pixels,sz,uvs[i1],uvs[i2],c);
            DrawLine(pixels,sz,uvs[i2],uvs[i0],c);
        }

        var tex = new Texture2D(sz,sz,TextureFormat.RGBA32,false);
        tex.SetPixels32(pixels); tex.Apply();
        if (_uvTexture != null) DestroyImmediate(_uvTexture);
        _uvTexture = tex;
    }

    private void DrawLine(Color32[] px, int sz, Vector2 a, Vector2 b, Color32 col)
    {
        int x0=Mathf.Clamp((int)(a.x*sz),0,sz-1),y0=Mathf.Clamp((int)(a.y*sz),0,sz-1);
        int x1=Mathf.Clamp((int)(b.x*sz),0,sz-1),y1=Mathf.Clamp((int)(b.y*sz),0,sz-1);
        int dx=Mathf.Abs(x1-x0),dy=Mathf.Abs(y1-y0);
        int sx=x0<x1?1:-1,sy=y0<y1?1:-1,err=dx-dy;
        while(true){px[y0*sz+x0]=col;if(x0==x1&&y0==y1)break;
            int e2=2*err;if(e2>-dy){err-=dy;x0+=sx;}if(e2<dx){err+=dx;y0+=sy;}}
    }

    // ================================================================
    //  ヘルパー描画
    // ================================================================
    private Rect GetUVDisplayRect(Rect view, float zoom, Vector2 pan)
    {
        float sz=Mathf.Min(view.width,view.height)*0.85f*zoom;
        float cx=view.x+view.width*0.5f+pan.x, cy=view.y+view.height*0.5f+pan.y;
        return new Rect(cx-sz*0.5f,cy-sz*0.5f,sz,sz);
    }

    private void DrawCheckerBoard(Rect rect)
    {
        if (_checkerTexture == null)
        {
            _checkerTexture = new Texture2D(2,2,TextureFormat.RGB24,false)
                {wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Point};
            _checkerTexture.SetPixels(new[]{
                new Color(0.25f,0.25f,0.25f),new Color(0.35f,0.35f,0.35f),
                new Color(0.35f,0.35f,0.35f),new Color(0.25f,0.25f,0.25f)});
            _checkerTexture.Apply();
        }
        GUI.DrawTextureWithTexCoords(rect,_checkerTexture,
            new Rect(0,0,rect.width/32f,rect.height/32f));
    }

    private void DrawGrid(Rect rect)
    {
        Handles.BeginGUI();
        Handles.color = new Color(0.5f,0.5f,0.5f,0.2f);
        for(int i=0;i<=8;i++){
            float t=i/8f;
            Handles.DrawLine(new Vector3(rect.x+t*rect.width,rect.y),
                             new Vector3(rect.x+t*rect.width,rect.yMax));
            Handles.DrawLine(new Vector3(rect.x,rect.y+t*rect.height),
                             new Vector3(rect.xMax,rect.y+t*rect.height));
        }
        Handles.color=Color.white; Handles.EndGUI();
    }

    private void DrawRectOutline(Rect r,Color col,float t)
    {
        EditorGUI.DrawRect(new Rect(r.x,r.y,r.width,t),col);
        EditorGUI.DrawRect(new Rect(r.x,r.yMax-t,r.width,t),col);
        EditorGUI.DrawRect(new Rect(r.x,r.y,t,r.height),col);
        EditorGUI.DrawRect(new Rect(r.xMax-t,r.y,t,r.height),col);
    }

    private void DrawEmptyMessage(Rect rect, string msg)
    {
        var s=new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {fontSize=13,normal={textColor=new Color(0.45f,0.45f,0.45f)}};
        GUI.Label(rect,msg,s);
    }

    // ================================================================
    //  ステータスバー
    // ================================================================
    private void DrawStatusBar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (_mesh != null)
            {
                string sm=_selectedSubMesh>=0?$"SubMesh#{_selectedSubMesh}":"All";
                GUILayout.Label($"{_mesh.name}  |  {_uvChannelNames[_uvChannel]}  |  {sm}  |  UV zoom:{_uvZoom:F2}x  |  3D dist:{_3dZoom:F1}");
            }
            else GUILayout.Label("No mesh selected");
            GUILayout.FlexibleSpace();
            GUILayout.Label("[UV] Scroll:zoom  RMB:pan  |  [3D] LMB:rotate  Scroll:zoom");
        }
    }

    // ================================================================
    //  メッシュ / UV チャンネル
    // ================================================================
    private void RefreshMesh()
    {
        _mesh=null; _uvDirty=true;
        if (_targetObject==null) return;
        var mf=_targetObject.GetComponent<MeshFilter>();
        if (mf!=null){_mesh=mf.sharedMesh;return;}
        var smr=_targetObject.GetComponent<SkinnedMeshRenderer>();
        if (smr!=null) _mesh=smr.sharedMesh;
    }

    private Vector2[] GetUVChannel(Mesh mesh, int ch)
    {
        var list=new List<Vector2>();
        mesh.GetUVs(ch,list);
        return list.ToArray();
    }

    // ================================================================
    //  PNG エクスポート
    // ================================================================
    private void ExportUVTexture()
    {
        if (_uvTexture==null){Debug.LogWarning("UV Texture が未生成です");return;}
        string path=EditorUtility.SaveFilePanel("Export UV PNG","","uv_export","png");
        if (string.IsNullOrEmpty(path)) return;
        System.IO.File.WriteAllBytes(path,_uvTexture.EncodeToPNG());
        Debug.Log($"[UV Viewer] Exported: {path}");
    }

    // ================================================================
    //  イベント
    // ================================================================
    private void OnSelectionChange() => Repaint();
    private void OnInspectorUpdate()  => Repaint();
}
