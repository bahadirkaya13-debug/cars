using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class car : MonoBehaviour
{
    // Web (index.html) ile aynı ölçek: 1 birim ≈ 100 px
    const float Friction = 1.60f;
    const float MaxPull = 1.50f;
    const float MinPull = 0.22f;
    const float MaxSpeed = 7.40f;
    const float Back = 0.22f;
    const float StopSpeed = 0.08f;
    const float CurlPower = 0.70f;
    const float CarRadius = 0.26f;
    const float CarLen = 0.82f;
    const float CarWide = 0.96f;
    const int PowerSteps = 5;
    const float PhoneAspect = 9f / 16f;
    const int CarsPerSide = 4;
    const string BestKey = "halka-arabalari-best";

    Vector2 House = new Vector2(0f, 0.35f);
    Vector2 Spawn = new Vector2(0f, -2.55f);
    readonly float[] RingRadius = { 0.98f, 0.72f, 0.50f, 0.18f };
    float laneHalf = 1.38f;
    // Dış → iç: 50, 100, 200, 400
    static readonly int[] RingPoints = { 50, 100, 200, 400 };
    static readonly Color[] RingColors =
    {
        new Color(0.12f, 0.45f, 0.86f),
        new Color(0.72f, 0.46f, 0.22f),
        new Color(0.82f, 0.16f, 0.16f),
        new Color(0.10f, 0.42f, 0.82f),
    };
    static readonly Color[] PlayerColors =
    {
        new Color(0.86f, 0.18f, 0.18f),
        new Color(0.95f, 0.48f, 0.10f),
        new Color(0.96f, 0.82f, 0.12f),
        new Color(0.90f, 0.30f, 0.45f),
    };
    static readonly Color[] BotColors =
    {
        new Color(0.16f, 0.42f, 0.86f),
        new Color(0.30f, 0.82f, 0.84f),
        new Color(0.35f, 0.55f, 0.95f),
        new Color(0.18f, 0.62f, 0.80f),
    };

    enum Phase { Aim, Slide, Over }

    class Stone
    {
        public GameObject view;
        public Vector2 pos;
        public Vector2 vel;
        public Vector2 home;
        public float curl;
        public float angle;
        public bool launched;
        public bool bot;
    }

    Camera cam;
    SpriteRenderer board;
    SpriteRenderer houseCover;
    readonly List<SpriteRenderer> houseParts = new List<SpriteRenderer>();
    Texture2D boardTex;
    Texture2D pixel;
    Sprite circleSprite;
    Sprite[] playerSprites;
    Sprite[] botSprites;
    readonly List<Stone> stones = new List<Stone>();
    readonly List<SpriteRenderer> dots = new List<SpriteRenderer>();
    readonly List<Vector2> preview = new List<Vector2>();
    Stone current;
    Phase phase;
    int playerThrown;
    int botThrown;
    bool botTurn;
    int best;
    float calm;
    float botThinkAt;
    bool pulling;
    Vector2 pressPos;
    Vector2 pull;
    string reason = "";
    GUIStyle label;
    GUIStyle hintStyle;
    GUIStyle hudStyle;
    bool stylesReady;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (FindAnyObjectByType<car>() != null) return;
        var go = new GameObject("Oyun");
        go.AddComponent<car>();
    }

    void Awake()
    {
        best = PlayerPrefs.GetInt(BestKey, 0);
        pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        pixel.SetPixel(0, 0, Color.white);
        pixel.Apply();
        QualitySettings.SetQualityLevel(QualitySettings.names.Length - 1, true);
        QualitySettings.globalTextureMipmapLimit = 0;
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
        QualitySettings.antiAliasing = 8;
        QualitySettings.vSyncCount = 1;
        Screen.orientation = ScreenOrientation.Portrait;
        circleSprite = MakeCircleSprite();
        playerSprites = new Sprite[PlayerColors.Length];
        for (int i = 0; i < PlayerColors.Length; i++)
            playerSprites[i] = MakeCarSprite(PlayerColors[i]);
        botSprites = new Sprite[BotColors.Length];
        for (int i = 0; i < BotColors.Length; i++)
            botSprites[i] = MakeCarSprite(BotColors[i]);
        LoadTable();
        SetupCamera();
        BuildRink();
        StartRound();
    }

    void SetupCamera()
    {
        cam = Camera.main;
        if (cam == null)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            cam = go.AddComponent<Camera>();
            go.AddComponent<AudioListener>();
        }

        cam.orthographic = true;
        cam.allowMSAA = true;
        cam.allowHDR = true;
        cam.backgroundColor = new Color(0.28f, 0.16f, 0.08f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        FitCamera();
    }

    void FitCamera()
    {
        if (cam == null) return;
        float screenAspect = Screen.width / (float)Mathf.Max(1, Screen.height);
        if (screenAspect > PhoneAspect)
        {
            float w = PhoneAspect / screenAspect;
            cam.rect = new Rect((1f - w) * 0.5f, 0f, w, 1f);
        }
        else
        {
            float h = screenAspect / PhoneAspect;
            cam.rect = new Rect(0f, (1f - h) * 0.5f, 1f, h);
        }

        cam.orthographicSize = 3.2f;
        float midY = 0f;
        cam.transform.position = new Vector3(0f, midY, -10f);
        float viewH = cam.orthographicSize * 2f;
        float viewW = viewH * PhoneAspect;
        if (board != null)
        {
            board.transform.position = new Vector3(0f, midY, 0f);
            board.transform.localScale = Vector3.one;
        }

        House = new Vector2(0f, midY + 0.34f * viewH);
        Spawn = new Vector2(0f, midY - 0.36f * viewH);
        float outer = 0.090f * viewH;
        RingRadius[0] = outer;
        RingRadius[1] = outer * 0.73f;
        RingRadius[2] = outer * 0.51f;
        RingRadius[3] = outer * 0.19f;
        laneHalf = 0.34f * viewW;
        LayoutHouse(viewH);
    }

    void LoadTable()
    {
        // Ahşap zemin: yüksek çözünürlüklü tahta dokusu.
        boardTex = MakeWoodTexture(1280, 2280);
        boardTex.filterMode = FilterMode.Trilinear;
        boardTex.wrapMode = TextureWrapMode.Clamp;
        boardTex.anisoLevel = 8;
    }

    void BuildRink()
    {
        if (boardTex == null) boardTex = MakeWoodTexture(1024, 2048);
        float ppu = boardTex.height / 6.4f;
        var sprite = Sprite.Create(
            boardTex,
            new Rect(0f, 0f, boardTex.width, boardTex.height),
            new Vector2(0.5f, 0.5f),
            ppu);
        board = MakeSprite("Masa", sprite, Color.white, -20);
        houseCover = MakeSprite("EskiHedefKapat", circleSprite, new Color(0.62f, 0.38f, 0.18f), -5);
        for (int i = 0; i < RingRadius.Length; i++)
        {
            var ring = MakeSprite("Halka", circleSprite, RingColors[i], i);
            houseParts.Add(ring);
        }

        var bar = MakeSquareSprite();
        var crossH = MakeSprite("HatYatay", bar, new Color(1f, 1f, 1f, 0.9f), 6);
        houseParts.Add(crossH);
        var crossV = MakeSprite("HatDikey", bar, new Color(1f, 1f, 1f, 0.9f), 6);
        houseParts.Add(crossV);
        FitCamera();
    }

    void LayoutHouse(float viewH)
    {
        if (houseCover != null)
        {
            Vector2 oldHouse = new Vector2(0f, (0.548f - 0.5f) * viewH);
            houseCover.transform.position = oldHouse;
            float cover = 0.22f * viewH;
            houseCover.transform.localScale = new Vector3(cover, cover, 1f);
        }

        for (int i = 0; i < 4 && i < houseParts.Count; i++)
        {
            houseParts[i].transform.position = House;
            houseParts[i].transform.localScale = Vector3.one * (RingRadius[i] * 2f);
        }

        if (houseParts.Count >= 6)
        {
            houseParts[4].transform.position = House;
            houseParts[4].transform.localScale = new Vector3(RingRadius[0] * 2.2f, 0.03f, 1f);
            houseParts[5].transform.position = House;
            houseParts[5].transform.localScale = new Vector3(0.03f, RingRadius[0] * 2.2f, 1f);
        }
    }

    void StartRound()
    {
        for (int i = 0; i < stones.Count; i++)
        {
            if (stones[i].view != null) Destroy(stones[i].view);
        }

        stones.Clear();
        playerThrown = 0;
        botThrown = 0;
        botTurn = false;
        calm = 0f;
        pulling = false;
        pull = Vector2.zero;
        botThinkAt = 0f;
        reason = "";
        phase = Phase.Aim;
        HideDots();
        SpawnStone(false);
    }

    void SpawnStone(bool forBot)
    {
        float x = Spawn.x;
        for (int i = 0; i < 7; i++)
        {
            float tx = Spawn.x + (i == 0 ? 0f : (i % 2 == 0 ? 1f : -1f) * Mathf.Ceil(i / 2f) * (CarRadius * 2.2f));
            bool blocked = false;
            for (int s = 0; s < stones.Count; s++)
            {
                if (Vector2.Distance(stones[s].pos, new Vector2(tx, Spawn.y)) < CarRadius * 2f + 0.08f)
                {
                    blocked = true;
                    break;
                }
            }

            if (!blocked)
            {
                x = tx;
                break;
            }
        }

        int idx = forBot ? botThrown : playerThrown;
        var sprites = forBot ? botSprites : playerSprites;
        var stone = new Stone
        {
            pos = new Vector2(x, Spawn.y),
            home = new Vector2(x, Spawn.y),
            angle = Mathf.PI * 0.5f,
            launched = false,
            bot = forBot,
        };
        stone.view = new GameObject(forBot ? "BotAraba" : "Araba");
        stone.view.transform.SetParent(transform, false);
        var body = stone.view.AddComponent<SpriteRenderer>();
        body.sprite = sprites[idx % sprites.Length];
        body.color = Color.white;
        body.sortingOrder = 12;
        stones.Add(stone);
        current = stone;
        botTurn = forBot;
        phase = Phase.Aim;
        if (forBot) botThinkAt = Time.time + 0.75f;
    }

    void Update()
    {
        FitCamera();
        float dt = Mathf.Min(Time.deltaTime, 0.033f);
        if (phase == Phase.Over && Clicked(AgainRect()))
        {
            StartRound();
            return;
        }

        if (phase == Phase.Aim && botTurn && current != null && Time.time >= botThinkAt)
            BotShoot();
        else if (!botTurn)
            ReadMouse();

        if (phase == Phase.Aim && current != null && !botTurn) AimStep();
        else if (phase == Phase.Slide) SlideStep(dt);

        SyncStones();
        SyncDots();
    }

    void ReadMouse()
    {
        if (phase != Phase.Aim || current == null || botTurn) return;
        if (!ReadPointer(out bool down, out bool held, out bool up, out Vector2 screen)) return;

        if (down)
        {
            pulling = true;
            pressPos = ScreenToWorld(screen);
            pull = Vector2.zero;
        }

        if (pulling && held)
            pull = ScreenToWorld(screen) - pressPos;

        if (pulling && up)
            Release();
    }

    bool ReadPointer(out bool down, out bool held, out bool up, out Vector2 screen)
    {
        down = held = up = false;
        screen = Vector2.zero;
        var touch = Touchscreen.current;
        if (touch != null)
        {
            var press = touch.primaryTouch.press;
            if (press.isPressed || press.wasReleasedThisFrame)
            {
                down = press.wasPressedThisFrame;
                held = press.isPressed;
                up = press.wasReleasedThisFrame;
                screen = touch.primaryTouch.position.ReadValue();
                return true;
            }
        }

        var mouse = Mouse.current;
        if (mouse == null) return false;
        var button = mouse.leftButton;
        if (!button.isPressed && !button.wasReleasedThisFrame) return false;
        down = button.wasPressedThisFrame;
        held = button.isPressed;
        up = button.wasReleasedThisFrame;
        screen = mouse.position.ReadValue();
        return true;
    }

    void Release()
    {
        float len = pull.magnitude;
        pulling = false;
        if (current == null || phase != Phase.Aim) return;
        if (len < MinPull)
        {
            pull = Vector2.zero;
            current.pos = current.home;
            current.angle = Mathf.PI * 0.5f;
            return;
        }

        ApplyShot(current, pull);
        current.launched = true;
        pull = Vector2.zero;
        phase = Phase.Slide;
        calm = 0f;
        preview.Clear();
    }

    void AimStep()
    {
        float len = pull.magnitude;
        if (pulling && len > 0.001f)
        {
            float back = Mathf.Min(len, MaxPull) * Back;
            current.pos = current.home + pull / len * back;
            Vector2 launch = -pull;
            current.angle = Mathf.Atan2(launch.y, launch.x);
            preview.Clear();
        }
        else
        {
            current.pos = current.home;
            current.angle = Mathf.PI * 0.5f;
            preview.Clear();
        }
    }

    void ApplyShot(Stone stone, Vector2 drag)
    {
        float len = drag.magnitude;
        if (len < 0.0001f) len = 0.0001f;
        // Sürekli güç: ince ayar kolay, 5 kademe sadece HUD.
        float t = Mathf.Clamp01(len / MaxPull);
        float speed = t * MaxSpeed;
        Vector2 dir = -drag / len;
        stone.vel = dir * speed;
        stone.curl = Mathf.Clamp(-drag.x / MaxPull, -1f, 1f) * CurlPower;
        stone.angle = Mathf.Atan2(stone.vel.y, stone.vel.x);
    }

    static int PowerBand(float len)
    {
        if (len < MinPull) return 0;
        float t = Mathf.Clamp01(len / MaxPull);
        return Mathf.Clamp(Mathf.CeilToInt(t * PowerSteps), 1, PowerSteps);
    }

    void SlideStep(float dt)
    {
        for (int i = 0; i < stones.Count; i++)
        {
            if (!stones[i].launched) continue;
            Glide(stones[i], dt);
        }

        for (int n = 0; n < 5; n++)
        {
            for (int i = 0; i < stones.Count; i++)
            {
                for (int j = i + 1; j < stones.Count; j++)
                {
                    if (!stones[i].launched || !stones[j].launched) continue;
                    Resolve(stones[i], stones[j]);
                }
            }
        }

        for (int i = 0; i < stones.Count; i++)
        {
            if (stones[i].launched) StopAtBoard(stones[i]);
        }

        bool moving = false;
        for (int i = 0; i < stones.Count; i++)
        {
            if (stones[i].launched && stones[i].vel.magnitude > StopSpeed)
            {
                moving = true;
                break;
            }
        }

        if (moving) calm = 0f;
        else
        {
            calm += dt;
            if (calm >= 0.18f) Settle();
        }
    }

    bool Glide(Stone body, float dt)
    {
        float sp = body.vel.magnitude;
        if (sp <= StopSpeed)
        {
            body.vel = Vector2.zero;
            return false;
        }

        body.angle = Mathf.Atan2(body.vel.y, body.vel.x);
        float drop = Friction * dt;
        if (drop >= sp)
        {
            body.vel = Vector2.zero;
            return false;
        }

        body.vel -= body.vel / sp * drop;
        float sp2 = body.vel.magnitude;
        if (sp2 > 0f && Mathf.Abs(body.curl) > 0.0001f)
        {
            float slow = 1f - Mathf.Min(1f, sp2 / MaxSpeed);
            float omega = (body.curl / 0.70f) * slow * slow;
            float ang = Mathf.Atan2(body.vel.y, body.vel.x) + omega * dt;
            body.vel = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * sp2;
        }

        body.pos += body.vel * dt;
        return !StopAtBoard(body) && body.vel.magnitude > StopSpeed;
    }

    bool StopAtBoard(Stone body)
    {
        float minX = -laneHalf + CarRadius;
        float maxX = laneHalf - CarRadius;
        float minY = Spawn.y - 0.4f;
        float maxY = House.y + RingRadius[0] + 0.55f;
        bool blocked = false;
        if (body.pos.x < minX) { body.pos.x = minX; body.vel.x = 0f; blocked = true; }
        if (body.pos.x > maxX) { body.pos.x = maxX; body.vel.x = 0f; blocked = true; }
        if (body.pos.y < minY) { body.pos.y = minY; body.vel.y = 0f; blocked = true; }
        if (body.pos.y > maxY) { body.pos.y = maxY; body.vel.y = 0f; blocked = true; }
        return blocked;
    }

    void Resolve(Stone a, Stone b)
    {
        Vector2 delta = b.pos - a.pos;
        float dist = delta.magnitude;
        float min = CarRadius * 2f;
        if (dist <= 0.0001f)
        {
            delta = Vector2.right;
            dist = 1f;
        }

        if (dist >= min) return;
        Vector2 n = delta / dist;
        float overlap = min - dist;
        a.pos -= n * overlap * 0.5f;
        b.pos += n * overlap * 0.5f;
        float rv = Vector2.Dot(b.vel - a.vel, n);
        if (rv >= 0f) return;
        float j = -(1f + 0.96f) * rv * 0.5f;
        a.vel -= j * n;
        b.vel += j * n;
        a.curl *= 0.35f;
        b.curl *= 0.35f;
    }

    void Settle()
    {
        int knocked = RemoveOutCars();
        if (current != null && current.bot) botThrown += 1;
        else playerThrown += 1;

        if (playerThrown >= CarsPerSide && botThrown >= CarsPerSide)
        {
            int you = TeamScore(false);
            int bot = TeamScore(true);
            if (you > bot) End("Sen kazandın!  " + you + " - " + bot);
            else if (bot > you) End("Bot kazandı.  " + you + " - " + bot);
            else End("Berabere.  " + you + " - " + bot);
            return;
        }

        bool nextBot = !botTurn;
        if (nextBot && botThrown >= CarsPerSide) nextBot = false;
        if (!nextBot && playerThrown >= CarsPerSide) nextBot = true;
        SpawnStone(nextBot);
    }

    int RemoveOutCars()
    {
        int removed = 0;
        for (int i = stones.Count - 1; i >= 0; i--)
        {
            Stone s = stones[i];
            if (!s.launched) continue;
            if (s.vel.magnitude > StopSpeed) continue;
            if (PointsAt(s.pos) > 0) continue;
            if (s.view != null) Destroy(s.view);
            stones.RemoveAt(i);
            removed++;
        }

        return removed;
    }

    void BotShoot()
    {
        if (current == null || phase != Phase.Aim || !botTurn) return;
        Vector2 target = House + Random.insideUnitCircle * 0.18f;
        Stone hit = null;
        float bestD = 999f;
        for (int i = 0; i < stones.Count; i++)
        {
            Stone s = stones[i];
            if (s.bot || !s.launched || s == current) continue;
            float d = Vector2.Distance(s.pos, House);
            if (d <= RingRadius[0] && d < bestD)
            {
                bestD = d;
                hit = s;
            }
        }

        if (hit != null && Random.value < 0.7f)
        {
            Vector2 outDir = (hit.pos - House).normalized;
            if (outDir.sqrMagnitude < 0.0001f) outDir = Vector2.right;
            target = hit.pos + outDir * (CarRadius * 0.35f);
        }

        Vector2 delta = target - current.home;
        float dist = Mathf.Max(0.4f, delta.magnitude);
        float need = Mathf.Sqrt(2f * Friction * dist) * Random.Range(0.92f, 1.12f);
        float t = Mathf.Clamp(need / MaxSpeed, 0.38f, 0.98f);
        Vector2 dir = delta.normalized;
        Vector2 drag = -dir * (t * MaxPull);
        drag.x += Random.Range(-0.10f, 0.10f) * MaxPull;
        ApplyShot(current, drag);
        current.launched = true;
        phase = Phase.Slide;
        calm = 0f;
        preview.Clear();
        pull = Vector2.zero;
        pulling = false;
    }

    void End(string why)
    {
        phase = Phase.Over;
        pulling = false;
        botTurn = false;
        reason = why;
        int score = TeamScore(false);
        if (score > best)
        {
            best = score;
            PlayerPrefs.SetInt(BestKey, best);
        }
    }

    int PointsAt(Vector2 p)
    {
        float d = Vector2.Distance(p, House);
        for (int i = RingRadius.Length - 1; i >= 0; i--)
        {
            if (d <= RingRadius[i]) return RingPoints[i];
        }

        return 0;
    }

    int TeamScore(bool bot)
    {
        int sum = 0;
        for (int i = 0; i < stones.Count; i++)
        {
            Stone stone = stones[i];
            if (!stone.launched || stone.bot != bot) continue;
            if (stone.vel.magnitude > StopSpeed) continue;
            sum += PointsAt(stone.pos);
        }

        return sum;
    }

    int Score() => TeamScore(false);

    void SyncStones()
    {
        for (int i = 0; i < stones.Count; i++)
        {
            Stone stone = stones[i];
            if (stone.view == null) continue;
            stone.view.transform.position = stone.pos;
            stone.view.transform.rotation = Quaternion.Euler(0f, 0f, stone.angle * Mathf.Rad2Deg);
            stone.view.transform.localScale = new Vector3(CarLen, CarWide, 1f);
        }
    }

    void SyncDots()
    {
        while (dots.Count < preview.Count)
        {
            var dot = MakeSprite("Nokta", circleSprite, Color.white, 6);
            dots.Add(dot);
        }

        for (int i = 0; i < dots.Count; i++)
        {
            bool on = phase == Phase.Aim && i < preview.Count;
            dots[i].enabled = on;
            if (!on) continue;
            bool end = i == preview.Count - 1;
            dots[i].transform.position = preview[i];
            dots[i].transform.localScale = Vector3.one * (end ? 0.36f : 0.09f);
            if (end)
            {
                int pts = PointsAt(preview[i]);
                dots[i].color = pts > 0
                    ? new Color(0.50f, 0.93f, 0.60f, 0.9f)
                    : new Color(1f, 0.45f, 0.56f, 0.9f);
            }
            else
            {
                dots[i].color = new Color(1f, 1f, 1f, 0.85f);
            }
        }
    }

    void HideDots()
    {
        for (int i = 0; i < dots.Count; i++) dots[i].enabled = false;
        preview.Clear();
    }

    Vector2 ScreenToWorld(Vector2 screen)
    {
        float dist = Mathf.Abs(cam.transform.position.z);
        Vector3 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, dist));
        return new Vector2(world.x, world.y);
    }

    bool Clicked(Rect guiRect)
    {
        if (!ReadPointer(out bool down, out _, out _, out Vector2 screen) || !down) return false;
        screen.y = Screen.height - screen.y;
        return guiRect.Contains(screen);
    }

    Rect SafeScreen()
    {
        Rect game;
        if (cam != null)
        {
            Rect r = cam.pixelRect;
            game = new Rect(r.x, Screen.height - r.yMax, r.width, r.height);
        }
        else
        {
            game = new Rect(0f, 0f, Screen.width, Screen.height);
        }

        Rect sa = Screen.safeArea;
        Rect saGui = new Rect(sa.x, Screen.height - sa.yMax, sa.width, sa.height);
        float xMin = Mathf.Max(game.xMin, saGui.xMin);
        float yMin = Mathf.Max(game.yMin, saGui.yMin);
        float xMax = Mathf.Min(game.xMax, saGui.xMax);
        float yMax = Mathf.Min(game.yMax, saGui.yMax);
        if (xMax <= xMin || yMax <= yMin) return game;
        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    Rect AgainRect()
    {
        Rect card = EndCard();
        float w = Mathf.Min(180f, card.width - 32f);
        return new Rect(card.x + (card.width - w) * 0.5f, card.yMax - 58f, w, 42f);
    }

    Rect EndCard()
    {
        Rect safe = SafeScreen();
        float w = Mathf.Min(420f, safe.width - 24f);
        float h = 260f;
        return new Rect(safe.x + (safe.width - w) * 0.5f, safe.y + (safe.height - h) * 0.5f, w, h);
    }

    void OnGUI()
    {
        EnsureStyles();
        DrawHud();
        if (phase == Phase.Over) DrawEnd();
        else DrawBar();
        DrawWorldTexts();
    }

    void DrawHud()
    {
        Rect safe = SafeScreen();
        int turn = Mathf.Clamp(Mathf.Max(playerThrown, botThrown) + (phase == Phase.Over ? 0 : 1), 1, CarsPerSide);
        float gap = 6f;
        float pad = 8f;
        float h = 26f;
        float y = safe.y + 8f;
        float inner = safe.width - pad * 2f - gap * 2f;
        float w = inner / 3f;
        float x = safe.x + pad;
        DrawPill(new Rect(x, y, w, h), "Sen " + TeamScore(false));
        DrawPill(new Rect(x + w + gap, y, w, h), "Bot " + TeamScore(true));
        DrawPill(new Rect(x + (w + gap) * 2f, y, w, h), "Tur " + turn + "/" + CarsPerSide);
        string turnHint = botTurn ? "Bot oynuyor…" : "Senin sıran";
        if (phase == Phase.Slide) turnHint = "Kayıyor…";
        if (phase != Phase.Over)
            DrawPill(new Rect(safe.x + (safe.width - 140f) * 0.5f, y + h + 6f, 140f, h), turnHint);
    }

    void DrawBar()
    {
        if (botTurn)
        {
            Rect safe = SafeScreen();
            GUI.Label(new Rect(safe.x, safe.yMax - 48f, safe.width, 22f), "Bot nişan alıyor…", hintStyle);
            return;
        }

        float len = pull.magnitude;
        int band = phase == Phase.Aim && pulling ? PowerBand(len) : 0;
        string hint = "Geri çek, bırak";
        if (phase == Phase.Slide) hint = "Kayıyor…";
        else if (band == 1) hint = "Hafif";
        else if (band == 2) hint = "Az";
        else if (band == 3) hint = "Orta";
        else if (band == 4) hint = "Güçlü";
        else if (band == 5) hint = "Tam güç";

        Rect safe = SafeScreen();
        float w = Mathf.Min(260f, safe.width - 28f);
        float x = safe.x + (safe.width - w) * 0.5f;
        float y = safe.yMax - 54f;
        float gap = 4f;
        float cell = (w - gap * (PowerSteps - 1)) / PowerSteps;
        for (int i = 0; i < PowerSteps; i++)
        {
            Rect r = new Rect(x + i * (cell + gap), y, cell, 16f);
            GUI.color = new Color(0.12f, 0.07f, 0.04f, 0.9f);
            GUI.DrawTexture(r, pixel);
            if (i < band)
            {
                float t = (i + 1) / (float)PowerSteps;
                GUI.color = Color.Lerp(new Color(0.35f, 0.82f, 0.45f), new Color(0.95f, 0.35f, 0.22f), t);
                GUI.DrawTexture(r, pixel);
            }
        }

        GUI.color = Color.white;
        GUI.Label(new Rect(x, y - 22f, w, 22f), hint, hintStyle);
    }

    void DrawEnd()
    {
        Rect card = EndCard();
        GUI.color = new Color(0.06f, 0.09f, 0.13f, 0.94f);
        GUI.DrawTexture(card, pixel);
        GUI.color = Color.white;
        GUI.Label(new Rect(card.x, card.y + 16f, card.width, 34f), "Tur bitti", label);
        GUI.Label(new Rect(card.x + 16f, card.y + 52f, card.width - 32f, 28f), reason, hintStyle);
        GUI.Label(new Rect(card.x, card.y + 88f, card.width, 28f), "Sen " + TeamScore(false) + "   Bot " + TeamScore(true), label);
        GUI.Label(new Rect(card.x, card.y + 120f, card.width, 24f), "Rekor " + best, hintStyle);
        Rect button = AgainRect();
        GUI.color = new Color(0.941f, 0.635f, 0.008f, 1f);
        GUI.DrawTexture(button, pixel);
        GUI.color = new Color(0.10f, 0.07f, 0.01f, 1f);
        GUI.Label(button, "Tekrar", label);
        GUI.color = Color.white;
    }

    void DrawWorldTexts()
    {
        var rects = new List<Rect>();
        for (int i = 0; i < stones.Count; i++)
        {
            Stone stone = stones[i];
            if (!stone.launched || stone.vel.magnitude > StopSpeed) continue;
            PlaceWorldLabel(stone.pos + new Vector2(0f, 0.36f), PointsAt(stone.pos).ToString(), rects);
        }
    }

    void PlaceWorldLabel(Vector2 world, string text, List<Rect> used)
    {
        if (cam == null) return;
        Vector3 screen = cam.WorldToScreenPoint(world);
        if (screen.z < 0f) return;
        Rect safe = SafeScreen();
        float width = 28f;
        float height = 22f;
        float x = screen.x - width * 0.5f;
        float y = Screen.height - screen.y - height;
        x = Mathf.Clamp(x, safe.x + 4f, safe.xMax - width - 4f);
        y = Mathf.Clamp(y, safe.y + 40f, safe.yMax - 70f);
        var rect = new Rect(x, y, width, height);
        for (int n = 0; n < 8; n++)
        {
            bool hit = false;
            for (int i = 0; i < used.Count; i++)
            {
                if (rect.Overlaps(used[i]))
                {
                    rect.y = used[i].yMin - height - 4f;
                    hit = true;
                    break;
                }
            }

            if (!hit) break;
        }

        used.Add(rect);
        DrawPill(rect, text);
    }

    void DrawPill(Rect rect, string text)
    {
        GUI.color = new Color(0.08f, 0.05f, 0.03f, 0.82f);
        GUI.DrawTexture(rect, pixel);
        GUI.color = Color.white;
        GUI.Label(rect, text, hudStyle != null ? hudStyle : label);
    }

    void EnsureStyles()
    {
        int size = Mathf.Clamp(Mathf.RoundToInt(SafeScreen().width / 22f), 12, 16);
        if (stylesReady && hudStyle != null && hudStyle.fontSize == size) return;
        label = new GUIStyle(GUI.skin.label);
        label.fontSize = size + 2;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.normal.textColor = Color.white;
        label.wordWrap = false;
        label.clipping = TextClipping.Clip;
        hudStyle = new GUIStyle(label);
        hudStyle.fontSize = size;
        hintStyle = new GUIStyle(label);
        hintStyle.fontSize = size;
        stylesReady = true;
    }

    SpriteRenderer MakeSprite(string name, Sprite sprite, Color color, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = order;
        return renderer;
    }

    static Sprite MakeCircleSprite()
    {
        const int n = 512;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, true);
        tex.filterMode = FilterMode.Trilinear;
        tex.anisoLevel = 8;
        float radius = n * 0.5f - 1.5f;
        var center = new Vector2(n * 0.5f, n * 0.5f);
        var pixels = new Color[n * n];
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                float edge = Mathf.Clamp01(radius + 1.25f - d);
                pixels[y * n + x] = new Color(1f, 1f, 1f, edge);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply(true);
        return Sprite.Create(tex, new Rect(0f, 0f, n, n), new Vector2(0.5f, 0.5f), n);
    }

    static Sprite MakeSquareSprite()
    {
        const int n = 16;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        var pixels = new Color[n * n];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, n, n), new Vector2(0.5f, 0.5f), n);
    }

    static Texture2D MakeWoodTexture(int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
        tex.filterMode = FilterMode.Trilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.anisoLevel = 8;
        var pixels = new Color[w * h];
        int planks = 7;
        float plankW = w / (float)planks;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int plank = Mathf.Clamp(Mathf.FloorToInt(x / plankW), 0, planks - 1);
                float local = (x - plank * plankW) / plankW;
                float seam = Mathf.SmoothStep(0f, 1f, Mathf.Min(local, 1f - local) * 18f);
                float grain = Mathf.Sin((y + plank * 37f) * 0.085f + Mathf.Sin(y * 0.021f + plank) * 2.4f);
                grain = 0.55f + 0.45f * grain;
                float wave = 0.92f + 0.08f * Mathf.Sin(y * 0.011f + plank * 1.7f);
                float shade = 0.78f + 0.22f * seam;
                float tone = (0.70f + 0.08f * (plank % 3)) * grain * wave * shade;
                float r = Mathf.Clamp01(0.52f * tone + 0.10f);
                float g = Mathf.Clamp01(0.32f * tone + 0.05f);
                float b = Mathf.Clamp01(0.14f * tone + 0.02f);
                if (local < 0.018f || local > 0.982f)
                {
                    r *= 0.62f;
                    g *= 0.62f;
                    b *= 0.62f;
                }

                pixels[y * w + x] = new Color(r, g, b, 1f);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply(true);
        return tex;
    }

    // Üstten görünüm: uzun gövde, orta kalınlık; tekerlek yok.
    static Sprite MakeCarSprite(Color paint)
    {
        const int w = 256;
        const int h = 128;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
        tex.filterMode = FilterMode.Trilinear;
        tex.anisoLevel = 8;
        var pixels = new Color[w * h];
        Color glass = new Color(0.62f, 0.86f, 0.98f, 1f);
        Color lamp = new Color(1f, 0.95f, 0.62f, 1f);
        Color roof = Color.Lerp(paint, Color.white, 0.14f);
        Color trim = Color.Lerp(paint, Color.black, 0.28f);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float nx = (x + 0.5f) / w * 2f - 1f;
                float ny = (y + 0.5f) / h * 2f - 1f;
                Color c = Color.clear;
                bool body = Rounded(nx, ny, 0.90f, 0.72f, 0.20f);
                bool cabin = Rounded(nx + 0.06f, ny, 0.28f, 0.42f, 0.10f);
                bool window = Rounded(nx + 0.18f, ny, 0.14f, 0.30f, 0.08f);
                bool lightL = Ellipse(nx + 0.76f, ny - 0.28f, 0.07f, 0.12f);
                bool lightR = Ellipse(nx + 0.76f, ny + 0.28f, 0.07f, 0.12f);
                bool bumper = Rounded(nx + 0.82f, ny, 0.08f, 0.52f, 0.07f);
                if (window) c = glass;
                else if (lightL || lightR) c = lamp;
                else if (cabin) c = roof;
                else if (bumper) c = trim;
                else if (body)
                {
                    float shade = 0.86f + 0.14f * (1f - Mathf.Abs(ny));
                    c = new Color(paint.r * shade, paint.g * shade, paint.b * shade, 1f);
                }

                pixels[y * w + x] = c;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply(true);
        return Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), w);
    }

    static bool Ellipse(float x, float y, float rx, float ry)
    {
        float nx = x / rx;
        float ny = y / ry;
        return nx * nx + ny * ny <= 1f;
    }

    static bool Rounded(float x, float y, float hx, float hy, float r)
    {
        float ax = Mathf.Abs(x) - hx + r;
        float ay = Mathf.Abs(y) - hy + r;
        if (ax <= 0f && Mathf.Abs(y) <= hy) return true;
        if (ay <= 0f && Mathf.Abs(x) <= hx) return true;
        if (ax > 0f && ay > 0f) return ax * ax + ay * ay <= r * r;
        return false;
    }
}
