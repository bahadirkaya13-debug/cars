using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

public class car : MonoBehaviour
{
    const float Friction = 1.70f;
    const float MaxPull = 1.50f;
    const float MinPull = 0.22f;
    const float MaxSpeed = 4.60f;
    const float Back = 0.22f;
    const float StopSpeed = 0.08f;
    const float CurlPower = 1.20f;
    const float CarRadius = 0.14f;
    const float CarLen = 0.44f;
    const float CarWide = 0.44f;
    const int PowerSteps = 5;
    const float PhoneAspect = 9f / 16f;
    const int CarsPerRound = 5;
    const int RoundsPerMatch = 3;
    const int DailyGoal = 20;
    const string BestKey = "halka-arabalari-best";
    const string PaintKey = "halka-arabalari-paint";
    const string ModelKey = "halka-arabalari-model";
    static readonly string[] ModelNames = { "Sport GT", "Lüks Sedan", "Limuzin" };
    const string DailyKey = "halka-arabalari-daily";
    const string DailyDayKey = "halka-arabalari-daily-day";

    Vector2 House = new Vector2(0f, 0.35f);
    Vector2 Spawn = new Vector2(0f, -2.55f);
    readonly float[] RingRadius = { 0.98f, 0.72f, 0.50f, 0.18f };
    float laneHalf = 1.38f;
    static readonly int[] RingPoints = { 1, 2, 3, 4 };
    static readonly Color[] RingColors =
    {
        new Color(0.12f, 0.45f, 0.86f),
        new Color(0.72f, 0.46f, 0.22f),
        new Color(0.82f, 0.16f, 0.16f),
        new Color(0.10f, 0.42f, 0.82f),
    };
    static readonly Color[] CarColors =
    {
        new Color(0.86f, 0.18f, 0.18f),
        new Color(0.16f, 0.42f, 0.86f),
        new Color(0.96f, 0.82f, 0.12f),
        new Color(0.18f, 0.62f, 0.16f),
        new Color(0.95f, 0.48f, 0.10f),
        new Color(0.30f, 0.82f, 0.84f),
    };

    enum Phase { Menu, Aim, Slide, Over }
    enum PickupKind { None, Boost, Traction }

    class Stone
    {
        public GameObject view;
        public Vector2 pos;
        public Vector2 vel;
        public Vector2 home;
        public float curl;
        public float angle;
        public bool launched;
        public int round;
        public int paint;
        public int model;
    }

    Camera cam;
    SpriteRenderer board;
    SpriteRenderer houseCover;
    readonly List<SpriteRenderer> houseParts = new List<SpriteRenderer>();
    Texture2D boardTex;
    Texture2D pixel;
    Sprite circleSprite;
    Sprite[] carSprites;
    readonly List<Stone> stones = new List<Stone>();
    readonly List<SpriteRenderer> dots = new List<SpriteRenderer>();
    readonly List<Vector2> preview = new List<Vector2>();
    Stone current;
    Phase phase;
    int thrown;
    int best;
    int paintIndex;
    int modelIndex;
    int matchRound;
    int streak;
    int matchScore;
    readonly int[] roundScores = new int[RoundsPerMatch];
    int dailyScore;
    string dailyDay = "";
    float calm;
    float devamUntil;
    float shotFriction = Friction;
    bool pulling;
    Vector2 pressPos;
    Vector2 pull;
    string reason = "";
    bool matchOver;
    PickupKind pickupKind;
    Vector2 pickupPos;
    bool pickupAlive;
    SpriteRenderer pickupView;
    class ScorePop
    {
        public Vector2 world;
        public string text;
        public float life;
        public float max;
        public Color color;
    }

    class BurstBit
    {
        public Vector2 pos;
        public Vector2 vel;
        public Color color;
        public float life;
        public float max;
    }

    readonly List<ScorePop> pops = new List<ScorePop>();
    readonly List<BurstBit> bits = new List<BurstBit>();
    GUIStyle popStyle;
    AudioSource speaker;
    AudioSource music;
    AudioClip sndPull;
    AudioClip sndShot;
    AudioClip sndHit;
    AudioClip sndIn;
    AudioClip sndOut;
    AudioClip sndPick;
    int lastBand;
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
        QualitySettings.antiAliasing = 4;
        Screen.orientation = ScreenOrientation.Portrait;
        circleSprite = MakeCircleSprite();
        carSprites = new Sprite[9];
        for (int m = 0; m < 3; m++)
            for (int p = 0; p < 3; p++)
                carSprites[m * 3 + p] = MakeCarSprite(CarColors[p], m);
        LoadTable();
        SetupCamera();
        BuildRink();
        SetupAudio();
        paintIndex = Mathf.Clamp(PlayerPrefs.GetInt(PaintKey, 0), 0, 2);
        modelIndex = Mathf.Clamp(PlayerPrefs.GetInt(ModelKey, 1), 0, 2);
        LoadDaily();
        phase = Phase.Menu;
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
        cam.backgroundColor = new Color(0.32f, 0.18f, 0.08f);
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
        laneHalf = 0.30f * viewW;
        LayoutHouse(viewH);
    }

    void LoadTable()
    {
        string path = Path.Combine(Application.streamingAssetsPath, "masa.png");
        if (!File.Exists(path))
            path = Path.Combine(Application.streamingAssetsPath, "masa.jpg");
        if (!File.Exists(path)) return;
        byte[] bytes = File.ReadAllBytes(path);
        boardTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        boardTex.LoadImage(bytes);
        boardTex.filterMode = FilterMode.Bilinear;
        boardTex.wrapMode = TextureWrapMode.Clamp;
        boardTex.anisoLevel = 4;
    }

    void BuildRink()
    {
        if (boardTex == null) return;
        float ppu = boardTex.height / 6.4f;
        var sprite = Sprite.Create(
            boardTex,
            new Rect(0f, 0f, boardTex.width, boardTex.height),
            new Vector2(0.5f, 0.5f),
            ppu);
        board = MakeSprite("Masa", sprite, Color.white, -20);
        houseCover = MakeSprite("EskiHedefKapat", circleSprite, new Color(0.70f, 0.44f, 0.20f), -5);
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

    void StartMatch()
    {
        for (int i = 0; i < stones.Count; i++)
        {
            if (stones[i].view != null) Destroy(stones[i].view);
        }

        stones.Clear();
        matchRound = 1;
        matchScore = 0;
        streak = 0;
        matchOver = false;
        for (int i = 0; i < roundScores.Length; i++) roundScores[i] = 0;
        StartRound();
    }

    void StartRound()
    {
        thrown = 0;
        calm = 0f;
        pulling = false;
        pull = Vector2.zero;
        devamUntil = 0f;
        reason = "";
        matchOver = false;
        shotFriction = Friction;
        phase = Phase.Aim;
        HideDots();
        PlacePickup();
        SpawnStone();
    }

    void SpawnStone()
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

        var stone = new Stone
        {
            pos = new Vector2(x, Spawn.y),
            home = new Vector2(x, Spawn.y),
            angle = Mathf.PI * 0.5f,
            launched = false,
            round = matchRound,
            paint = paintIndex,
            model = modelIndex,
        };
        stone.view = new GameObject("Araba");
        stone.view.transform.SetParent(transform, false);
        var body = stone.view.AddComponent<SpriteRenderer>();
        body.sprite = carSprites[modelIndex * 3 + paintIndex];
        body.color = Color.white;
        body.sortingOrder = 12;
        stones.Add(stone);
        current = stone;
        phase = Phase.Aim;
    }

    void Update()
    {
        FitCamera();
        float dt = Mathf.Min(Time.deltaTime, 0.033f);
        if (phase == Phase.Menu)
        {
            HandleMenuClicks();
            return;
        }

        if (phase == Phase.Over && Clicked(AgainRect()))
        {
            if (matchOver) StartMatch();
            else StartRound();
            return;
        }

        ReadMouse();
        if (phase == Phase.Aim && current != null) AimStep();
        else if (phase == Phase.Slide) SlideStep(dt);

        TickFx(dt);
        SyncStones();
        SyncDots();
    }

    void ReadMouse()
    {
        if (phase != Phase.Aim || current == null) return;
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
        lastBand = 0;
        preview.Clear();
        Play(sndShot);
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
            int band = PowerBand(len);
            if (band != lastBand)
            {
                lastBand = band;
                if (band > 0) Play(sndPull);
            }
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
        float speed = PowerBand(len) / (float)PowerSteps * MaxSpeed;
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

        TryCollectPickup();

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
        float drop = ((current != null && body == current) ? shotFriction : Friction) * dt;
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
        float j = -(1f + 0.88f) * rv * 0.5f;
        a.vel -= j * n;
        b.vel += j * n;
        a.curl *= 0.35f;
        b.curl *= 0.35f;
        if (rv < -0.35f)
        {
            Play(sndHit);
            Buzz();
        }
    }

    void Settle()
    {
        bool inside = PointsAt(current.pos) > 0;
        thrown += 1;
        if (!inside)
        {
            streak = 0;
            Play(sndOut);
            EndRound("Araba halkanın dışında durdu.");
            return;
        }

        streak += 1;
        int got = PointsAt(current.pos);
        BurstScore(current.pos, got);
        Play(sndIn);
        Buzz();
        if (thrown >= CarsPerRound)
        {
            EndRound("Beş araba da halkada.");
            return;
        }

        shotFriction = Friction;
        devamUntil = Time.time + 0.9f;
        SpawnStone();
    }

    void EndRound(string why)
    {
        int now = ScoreThisRound();
        roundScores[matchRound - 1] = now;
        matchScore += now;
        AddDaily(now);
        if (matchScore > best)
        {
            best = matchScore;
            PlayerPrefs.SetInt(BestKey, best);
        }

        phase = Phase.Over;
        pulling = false;
        HidePickup();
        if (matchRound >= RoundsPerMatch)
        {
            matchOver = true;
            reason = why + " Maç bitti.";
        }
        else
        {
            matchOver = false;
            matchRound += 1;
            reason = why;
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

    int ScoreThisRound()
    {
        int round = matchRound;
        int sum = 0;
        for (int i = 0; i < stones.Count; i++)
        {
            Stone stone = stones[i];
            if (!stone.launched || stone.round != round) continue;
            if (stone.vel.magnitude > StopSpeed) continue;
            sum += PointsAt(stone.pos);
        }

        return sum;
    }

    int LiveScore()
    {
        return matchScore + (phase == Phase.Over ? 0 : ScoreThisRound());
    }

    void SyncStones()
    {
        for (int i = 0; i < stones.Count; i++)
        {
            Stone stone = stones[i];
            if (stone.view == null) continue;
            stone.view.transform.position = stone.pos;
            stone.view.transform.rotation = Quaternion.Euler(0f, 0f, stone.angle * Mathf.Rad2Deg);
            float len = CarLen * (stone.model == 2 ? 1.28f : stone.model == 0 ? 1.08f : 1f);
            float wide = CarWide * (stone.model == 0 ? 0.86f : 1f);
            stone.view.transform.localScale = new Vector3(len, wide, 1f);
            var body = stone.view.GetComponent<SpriteRenderer>();
            if (body != null)
            {
                Color c = Color.white;
                c.a = stone.round < matchRound ? 0.55f : 1f;
                body.color = c;
            }
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
        float h = 248f;
        return new Rect(safe.x + (safe.width - w) * 0.5f, safe.y + (safe.height - h) * 0.5f, w, h);
    }

    void OnGUI()
    {
        EnsureStyles();
        if (phase == Phase.Menu)
        {
            DrawMenu();
            return;
        }

        DrawHud();
        if (phase == Phase.Over) DrawEnd();
        else DrawBar();
        DrawWorldTexts();
        DrawFx();
    }

    void DrawHud()
    {
        Rect safe = SafeScreen();
        int n = phase == Phase.Over ? thrown : thrown + 1;
        n = Mathf.Clamp(n, 1, CarsPerRound);
        float gap = 6f;
        float pad = 10f;
        float h = 26f;
        float y = safe.y + 8f;
        float inner = safe.width - pad * 2f;
        float x = safe.x + pad;
        float col = (inner - gap) * 0.5f;
        int hudRound = (phase == Phase.Over && !matchOver) ? matchRound - 1 : matchRound;
        hudRound = Mathf.Clamp(hudRound, 1, RoundsPerMatch);
        DrawPill(new Rect(x, y, col, h), "Tur " + hudRound + "/" + RoundsPerMatch);
        DrawPill(new Rect(x + col + gap, y, col, h), "Araba " + n + "/" + CarsPerRound);
        y += h + gap;
        DrawPill(new Rect(x, y, col, h), "Skor " + LiveScore());
        DrawPill(new Rect(x + col + gap, y, col, h), "Bugün " + dailyScore + "/" + DailyGoal);
        if (streak > 1)
            DrawPill(new Rect(safe.x + (safe.width - 88f) * 0.5f, y + h + 6f, 88f, h), "Seri " + streak);
        else if (Time.time < devamUntil)
            DrawPill(new Rect(safe.x + (safe.width - 90f) * 0.5f, y + h + 6f, 90f, h), "Devam");
    }

    void DrawBar()
    {
        float len = pull.magnitude;
        int band = phase == Phase.Aim && pulling ? PowerBand(len) : 0;
        string hint = thrown == 0 && matchRound == 1 && !pulling ? "Üst halkaya at" : "Geri çek, bırak";
        if (phase == Phase.Slide) hint = "Kayıyor…";
        else if (shotFriction < Friction * 0.9f) hint = "Boost";
        else if (shotFriction > Friction * 1.1f) hint = "Tutuş";
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
        GUI.Label(new Rect(card.x, card.y + 10f, card.width, 30f), matchOver ? "Maç bitti" : "Tur bitti", label);
        GUI.Label(new Rect(card.x + 12f, card.y + 40f, card.width - 24f, 36f), reason, hintStyle);
        GUI.Label(new Rect(card.x, card.y + 78f, card.width, 26f), "Maç " + matchScore + "  En iyi " + best, hintStyle);
        GUI.Label(new Rect(card.x, card.y + 102f, card.width, 22f), "Bugün " + dailyScore + "/" + DailyGoal, hintStyle);
        Rect button = AgainRect();
        GUI.color = new Color(0.941f, 0.635f, 0.008f, 1f);
        GUI.DrawTexture(button, pixel);
        GUI.color = new Color(0.10f, 0.07f, 0.01f, 1f);
        GUI.Label(button, matchOver ? "Tekrar" : "Sonraki tur", label);
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
        popStyle = new GUIStyle(label);
        popStyle.fontSize = size + 10;
        popStyle.fontStyle = FontStyle.Bold;
        stylesReady = true;
    }

    void HandleMenuClicks()
    {
        for (int i = 0; i < 3; i++)
        {
            if (Clicked(ModelRect(i)))
            {
                modelIndex = i;
                PlayerPrefs.SetInt(ModelKey, modelIndex);
                Play(sndPull);
                return;
            }

            if (Clicked(PaintRect(i)))
            {
                paintIndex = i;
                PlayerPrefs.SetInt(PaintKey, paintIndex);
                Play(sndPull);
                return;
            }
        }

        if (Clicked(StartRect()))
            StartMatch();
    }

    void DrawMenu()
    {
        Rect safe = SafeScreen();
        Rect card = new Rect(safe.x + 16f, safe.y + safe.height * 0.12f, safe.width - 32f, safe.height * 0.72f);
        GUI.color = new Color(0.08f, 0.05f, 0.03f, 0.92f);
        GUI.DrawTexture(card, pixel);
        GUI.color = Color.white;
        GUI.Label(new Rect(card.x, card.y + 12f, card.width, 32f), "Halka Arabaları", label);
        GUI.Label(new Rect(card.x, card.y + 44f, card.width, 20f), "En iyi " + best + "   Bugün " + dailyScore + "/" + DailyGoal, hintStyle);
        GUI.Label(new Rect(card.x, card.y + 68f, card.width, 20f), ModelNames[modelIndex], hintStyle);
        for (int i = 0; i < 3; i++)
        {
            Rect mr = ModelRect(i);
            GUI.color = i == modelIndex ? new Color(0.94f, 0.64f, 0.02f, 1f) : new Color(0.22f, 0.12f, 0.07f, 1f);
            GUI.DrawTexture(mr, pixel);
            GUI.color = Color.white;
            GUI.Label(mr, ModelNames[i].Replace(" ", "\n"), hintStyle);
            Rect r = PaintRect(i);
            GUI.color = CarColors[i];
            GUI.DrawTexture(r, pixel);
            if (i == paintIndex)
            {
                GUI.color = Color.white;
                GUI.Box(r, "");
            }
        }

        GUI.color = new Color(0.941f, 0.635f, 0.008f, 1f);
        GUI.DrawTexture(StartRect(), pixel);
        GUI.color = new Color(0.10f, 0.07f, 0.01f, 1f);
        GUI.Label(StartRect(), "Başla", label);
        GUI.color = Color.white;
    }

    Rect PaintRect(int i)
    {
        Rect safe = SafeScreen();
        float size = 44f;
        float gap = 14f;
        float total = size * 3f + gap * 2f;
        float x = safe.x + (safe.width - total) * 0.5f + i * (size + gap);
        return new Rect(x, safe.y + safe.height * 0.52f, size, size);
    }

    Rect ModelRect(int i)
    {
        Rect safe = SafeScreen();
        float w = Mathf.Min(92f, (safe.width - 48f) / 3f);
        float gap = 8f;
        float total = w * 3f + gap * 2f;
        float x = safe.x + (safe.width - total) * 0.5f + i * (w + gap);
        return new Rect(x, safe.y + safe.height * 0.34f, w, 48f);
    }

    Rect StartRect()
    {
        Rect safe = SafeScreen();
        float w = Mathf.Min(180f, safe.width - 48f);
        return new Rect(safe.x + (safe.width - w) * 0.5f, safe.y + safe.height * 0.68f, w, 44f);
    }

    void LoadDaily()
    {
        string today = System.DateTime.Now.ToString("yyyyMMdd");
        dailyDay = PlayerPrefs.GetString(DailyDayKey, "");
        if (dailyDay != today)
        {
            dailyDay = today;
            dailyScore = 0;
            PlayerPrefs.SetString(DailyDayKey, dailyDay);
            PlayerPrefs.SetInt(DailyKey, 0);
        }
        else dailyScore = PlayerPrefs.GetInt(DailyKey, 0);
    }

    void AddDaily(int points)
    {
        LoadDaily();
        dailyScore += points;
        PlayerPrefs.SetInt(DailyKey, dailyScore);
    }

    void PlacePickup()
    {
        HidePickup();
        if (Random.value > 0.28f) return;
        pickupKind = Random.value < 0.5f ? PickupKind.Boost : PickupKind.Traction;
        pickupPos = new Vector2(Random.Range(-laneHalf * 0.45f, laneHalf * 0.45f), Mathf.Lerp(Spawn.y + 0.9f, House.y - 0.4f, Random.Range(0.25f, 0.7f)));
        pickupAlive = true;
        Color col = pickupKind == PickupKind.Boost ? new Color(0.25f, 0.85f, 0.45f, 0.95f) : new Color(0.95f, 0.55f, 0.15f, 0.95f);
        pickupView = MakeSprite("Pickup", circleSprite, col, 8);
        pickupView.transform.position = pickupPos;
        pickupView.transform.localScale = Vector3.one * 0.28f;
    }

    void HidePickup()
    {
        pickupAlive = false;
        pickupKind = PickupKind.None;
        if (pickupView != null)
        {
            Destroy(pickupView.gameObject);
            pickupView = null;
        }
    }

    void TryCollectPickup()
    {
        if (!pickupAlive || current == null || !current.launched) return;
        if (Vector2.Distance(current.pos, pickupPos) > CarRadius + 0.16f) return;
        if (pickupKind == PickupKind.Boost) shotFriction = Friction * 0.55f;
        else shotFriction = Friction * 1.85f;
        Play(sndPick);
        Buzz();
        HidePickup();
    }

    void SetupAudio()
    {
        speaker = gameObject.AddComponent<AudioSource>();
        speaker.playOnAwake = false;
        sndPull = MakeTone(210f, 0.06f);
        sndShot = MakeTone(170f, 0.12f);
        sndHit = MakeTone(380f, 0.05f);
        sndIn = MakeTone(560f, 0.16f);
        sndOut = MakeTone(120f, 0.18f);
        sndPick = MakeTone(440f, 0.10f);
        music = gameObject.AddComponent<AudioSource>();
        music.playOnAwake = false;
        music.loop = true;
        music.volume = 0.18f;
        music.clip = MakeLoungeLoop();
        music.Play();
    }

    AudioClip MakeTone(float freq, float seconds)
    {
        int rate = 44100;
        int n = Mathf.Max(32, (int)(rate * seconds));
        var clip = AudioClip.Create("ton", n, 1, rate, false);
        var data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float env = 1f - t / seconds;
            data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.11f * env;
        }

        clip.SetData(data, 0);
        return clip;
    }

    AudioClip MakeLoungeLoop()
    {
        int rate = 22050;
        float bpm = 96f;
        float beat = 60f / bpm;
        int bars = 4;
        int samples = (int)(rate * beat * 4f * bars);
        var data = new float[samples];
        float[] melody = { 261.63f, 329.63f, 392.00f, 523.25f, 392.00f, 329.63f, 349.23f, 261.63f };
        float[] bass = { 130.81f, 130.81f, 196.00f, 174.61f };
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)rate;
            int eighth = Mathf.FloorToInt(t / (beat * 0.5f)) % melody.Length;
            int beatI = Mathf.FloorToInt(t / beat) % bass.Length;
            float envM = 1f - (t / (beat * 0.5f) % 1f);
            float envB = 1f - (t / beat % 1f);
            float m = Mathf.Sin(2f * Mathf.PI * melody[eighth] * t) * 0.045f * envM;
            float b = Mathf.Sin(2f * Mathf.PI * bass[beatI] * t) * 0.05f * envB;
            data[i] = (m + b) * (0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * t / 8f));
        }

        var clip = AudioClip.Create("lounge", samples, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    void BurstScore(Vector2 world, int pts)
    {
        Color gold = pts >= 4 ? new Color(1f, 0.86f, 0.25f) : new Color(1f, 0.95f, 0.55f);
        pops.Add(new ScorePop { world = world, text = "+" + pts, life = 0.9f, max = 0.9f, color = gold });
        int n = 8 + pts * 7;
        for (int i = 0; i < n; i++)
        {
            float ang = (i / (float)n) * Mathf.PI * 2f + Random.Range(-0.2f, 0.2f);
            float sp = Random.Range(1.2f, 3.4f + pts);
            bits.Add(new BurstBit
            {
                pos = world,
                vel = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * sp,
                color = i % 2 == 0 ? gold : new Color(1f, 0.45f, 0.12f),
                life = Random.Range(0.35f, 0.7f)
            });
            bits[bits.Count - 1].max = bits[bits.Count - 1].life;
        }
    }

    void TickFx(float dt)
    {
        for (int i = pops.Count - 1; i >= 0; i--)
        {
            pops[i].life -= dt;
            pops[i].world += Vector2.up * (1.1f * dt);
            if (pops[i].life <= 0f) pops.RemoveAt(i);
        }

        for (int i = bits.Count - 1; i >= 0; i--)
        {
            bits[i].life -= dt;
            bits[i].vel *= 0.92f;
            bits[i].pos += bits[i].vel * dt;
            if (bits[i].life <= 0f) bits.RemoveAt(i);
        }
    }

    void DrawFx()
    {
        if (cam == null) return;
        for (int i = 0; i < bits.Count; i++)
        {
            BurstBit b = bits[i];
            Vector3 s = cam.WorldToScreenPoint(b.pos);
            if (s.z < 0f) continue;
            float t = Mathf.Clamp01(b.life / b.max);
            float sz = 6f + 8f * t;
            var r = new Rect(s.x - sz * 0.5f, Screen.height - s.y - sz * 0.5f, sz, sz);
            Color c = b.color;
            c.a = t;
            GUI.color = c;
            GUI.DrawTexture(r, pixel);
        }

        for (int i = 0; i < pops.Count; i++)
        {
            ScorePop p = pops[i];
            Vector3 s = cam.WorldToScreenPoint(p.world);
            if (s.z < 0f) continue;
            float t = Mathf.Clamp01(p.life / p.max);
            float grow = 1f + (1f - t) * 1.35f;
            float w = 70f * grow;
            float h = 36f * grow;
            var r = new Rect(s.x - w * 0.5f, Screen.height - s.y - h * 0.5f, w, h);
            Color c = p.color;
            c.a = t;
            GUI.color = c;
            GUI.Label(r, p.text, popStyle != null ? popStyle : label);
        }

        GUI.color = Color.white;
    }

    void Play(AudioClip clip)
    {
        if (speaker == null || clip == null) return;
        speaker.PlayOneShot(clip);
    }

    void Buzz()
    {
        if (Application.isMobilePlatform) Handheld.Vibrate();
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
        const int n = 256;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float radius = n * 0.5f - 1f;
        var center = new Vector2(n * 0.5f, n * 0.5f);
        var pixels = new Color[n * n];
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center);
                pixels[y * n + x] = d <= radius ? Color.white : Color.clear;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, n, n), new Vector2(0.5f, 0.5f), n);
    }

    static Sprite MakeSquareSprite()
    {
        const int n = 8;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        var pixels = new Color[n * n];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, n, n), new Vector2(0.5f, 0.5f), n);
    }

    static Sprite MakeCarSprite(Color paint, int model)
    {
        const int w = 160;
        const int h = 72;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        var pixels = new Color[w * h];
        Color tire = new Color(0.10f, 0.10f, 0.10f, 1f);
        Color chrome = new Color(0.82f, 0.84f, 0.88f, 1f);
        Color glass = new Color(0.45f, 0.72f, 0.92f, 1f);
        Color lamp = new Color(1f, 0.94f, 0.55f, 1f);
        Color gold = new Color(0.92f, 0.75f, 0.28f, 1f);
        Color roof = Color.Lerp(paint, Color.white, 0.12f);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float nx = (x + 0.5f) / w * 2f - 1f;
                float ny = (y + 0.5f) / h * 2f - 1f;
                Color c = Color.clear;
                bool leftTire = Rounded(nx - (model == 2 ? -0.12f : 0f), ny + 0.78f, model == 2 ? 0.72f : 0.58f, 0.15f, 0.07f);
                bool rightTire = Rounded(nx - (model == 2 ? -0.12f : 0f), ny - 0.78f, model == 2 ? 0.72f : 0.58f, 0.15f, 0.07f);
                bool body = false, cabin = false, window = false, extra = false, stripe = false;
                if (model == 0)
                {
                    body = Rounded(nx, ny, 0.88f, 0.46f, 0.22f) && nx < 0.92f;
                    cabin = Rounded(nx + 0.02f, ny, 0.22f, 0.30f, 0.08f);
                    window = Rounded(nx + 0.16f, ny, 0.16f, 0.22f, 0.06f);
                    extra = nx > 0.55f && Mathf.Abs(ny) < 0.22f && nx < 0.95f;
                }
                else if (model == 2)
                {
                    body = Rounded(nx, ny, 0.94f, 0.52f, 0.12f);
                    cabin = Rounded(nx, ny, 0.42f, 0.34f, 0.08f);
                    window = Rounded(nx + 0.22f, ny, 0.12f, 0.24f, 0.05f) || Rounded(nx + 0.02f, ny, 0.12f, 0.24f, 0.05f) || Rounded(nx - 0.20f, ny, 0.12f, 0.24f, 0.05f);
                    stripe = Mathf.Abs(ny) < 0.06f && Mathf.Abs(nx) < 0.88f;
                }
                else
                {
                    body = Rounded(nx, ny, 0.84f, 0.55f, 0.16f);
                    cabin = Rounded(nx + 0.06f, ny, 0.30f, 0.34f, 0.10f);
                    window = Rounded(nx + 0.18f, ny, 0.14f, 0.26f, 0.07f);
                    stripe = Mathf.Abs(ny) < 0.045f && nx > -0.2f && nx < 0.7f;
                }

                bool lightL = Ellipse(nx + 0.78f, ny - 0.20f, 0.07f, 0.10f);
                bool lightR = Ellipse(nx + 0.78f, ny + 0.20f, 0.07f, 0.10f);
                if (leftTire || rightTire) c = tire;
                else if (window) c = glass;
                else if (lightL || lightR) c = lamp;
                else if (stripe) c = model == 2 ? gold : chrome;
                else if (extra) c = chrome;
                else if (cabin) c = roof;
                else if (body) c = paint;
                pixels[y * w + x] = c;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
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
