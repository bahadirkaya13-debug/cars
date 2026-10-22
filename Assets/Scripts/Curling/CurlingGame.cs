using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public class CurlingGame : MonoBehaviour
{
    const int CarsPerRound = 5;

    static readonly Color[] CarColors =
    {
        new Color(0.92f, 0.28f, 0.28f),
        new Color(0.2f, 0.55f, 0.95f),
        new Color(0.95f, 0.72f, 0.15f),
        new Color(0.25f, 0.78f, 0.42f),
        new Color(0.72f, 0.38f, 0.92f)
    };

    readonly float[] ringRadii = { 0.55f, 1.2f, 1.95f, 2.75f };
    readonly int[] ringScores = { 10, 7, 4, 1 };

    Camera cam;
    Vector2 houseCenter = new Vector2(0f, 5.7f);
    Vector2 spawnPos = new Vector2(0f, -7.15f);
    PhysicsMaterial2D ice;
    readonly List<CurlingCar> cars = new List<CurlingCar>();
    CurlingCar activeCar;
    int nextCarIndex;
    bool dragging;
    bool roundOver;
    LineRenderer aimLine;
    Transform powerDot;
    Text scoreText;
    Text hintText;
    Text resultText;
    Button restartButton;

    // Oyun2 ana giriş noktası car.cs; bu otomatik açılışı kapalı tut.
    static void AutoStart()
    {
        if (FindFirstObjectByType<CurlingGame>() != null)
            return;
        var go = new GameObject("CurlingGame");
        go.AddComponent<CurlingGame>();
    }

    void Awake()
    {
        Physics2D.gravity = Vector2.zero;
        SetupCamera();
        ice = new PhysicsMaterial2D("Buz") { friction = 0.015f, bounciness = 0.12f };
        BuildRink();
        BuildHouse();
        BuildWalls();
        BuildAim();
        BuildUi();
        StartRound();
    }

    void OnDestroy()
    {
        Physics2D.gravity = new Vector2(0f, -9.81f);
    }

    void SetupCamera()
    {
        cam = Camera.main;
        if (cam == null)
        {
            var go = new GameObject("Main Camera");
            cam = go.AddComponent<Camera>();
            go.AddComponent<AudioListener>();
            go.tag = "MainCamera";
        }

        cam.orthographic = true;
        cam.orthographicSize = 10.4f;
        cam.transform.SetPositionAndRotation(new Vector3(0f, 0f, -10f), Quaternion.identity);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.28f, 0.16f, 0.08f);
        cam.nearClipPlane = 0.1f;
        cam.allowMSAA = true;
    }

    void BuildRink()
    {
        CreateSprite("AhsapZemin", SpriteUtil.Rect(32, 32, new Color(0.52f, 0.32f, 0.14f)), Vector3.zero, new Vector3(7.2f, 20.4f, 1f), 0);
        for (int i = 0; i < 7; i++)
        {
            float x = -3.0f + i * 1.0f;
            var plank = CreateSprite($"Tahta{i}", SpriteUtil.Rect(32, 32, new Color(0.58f + (i % 3) * 0.04f, 0.36f, 0.16f)), new Vector3(x, 0f, 0f), new Vector3(0.92f, 20.4f, 1f), 1);
            _ = plank;
        }

        CreateSprite("AtisAlani", SpriteUtil.Rect(32, 32, new Color(0.70f, 0.48f, 0.24f, 0.35f)), new Vector3(0f, -7.15f, 0f), new Vector3(3.4f, 1.6f, 1f), 2);
        CreateSprite("OrtaCizgi", SpriteUtil.Rect(8, 8, new Color(0.30f, 0.16f, 0.06f, 0.35f)), Vector3.zero, new Vector3(0.06f, 18.6f, 1f), 2);
        CreateSprite("HogCizgisi", SpriteUtil.Rect(8, 8, new Color(0.85f, 0.25f, 0.25f, 0.7f)), new Vector3(0f, 2.4f, 0f), new Vector3(6.6f, 0.06f, 1f), 2);
    }

    void BuildHouse()
    {
        Color[] rings =
        {
            new Color(0.15f, 0.42f, 0.82f, 0.95f),
            Color.white,
            new Color(0.82f, 0.18f, 0.2f, 0.95f),
            Color.white
        };

        for (int i = ringRadii.Length - 1; i >= 0; i--)
        {
            float diameter = ringRadii[i] * 2f;
            CreateSprite($"Halka{i}", SpriteUtil.Circle(256, rings[i]), houseCenter, new Vector3(diameter, diameter, 1f), 3 + (ringRadii.Length - i));
        }

        CreateSprite("Bullseye", SpriteUtil.Circle(64, new Color(0.1f, 0.1f, 0.12f)), houseCenter, new Vector3(0.16f, 0.16f, 1f), 8);
        CreateSprite("YatayHedef", SpriteUtil.Rect(8, 8, new Color(0.1f, 0.1f, 0.12f, 0.8f)), houseCenter, new Vector3(5.6f, 0.045f, 1f), 8);
        CreateSprite("DikeyHedef", SpriteUtil.Rect(8, 8, new Color(0.1f, 0.1f, 0.12f, 0.8f)), houseCenter, new Vector3(0.045f, 5.6f, 1f), 8);
    }

    void BuildWalls()
    {
        CreateWall("Sol", new Vector2(-3.7f, 0f), new Vector2(0.25f, 19.5f));
        CreateWall("Sag", new Vector2(3.7f, 0f), new Vector2(0.25f, 19.5f));
        CreateWall("Ust", new Vector2(0f, 9.7f), new Vector2(7.6f, 0.25f));
        CreateWall("Alt", new Vector2(0f, -9.7f), new Vector2(7.6f, 0.25f));
    }

    void CreateWall(string name, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        var col = go.AddComponent<BoxCollider2D>();
        col.size = Vector2.one;
        col.sharedMaterial = ice;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteUtil.Rect(32, 32, new Color(0.18f, 0.3f, 0.42f));
        sr.sortingOrder = 9;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
    }

    void BuildAim()
    {
        var lineGo = new GameObject("Nisangah");
        aimLine = lineGo.AddComponent<LineRenderer>();
        aimLine.positionCount = 2;
        aimLine.startWidth = 0.07f;
        aimLine.endWidth = 0.02f;
        var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Hidden/Internal-Colored");
        aimLine.material = new Material(shader);
        aimLine.useWorldSpace = true;
        aimLine.startColor = new Color(0.1f, 0.1f, 0.12f, 0.9f);
        aimLine.endColor = new Color(0.1f, 0.1f, 0.12f, 0.15f);
        aimLine.sortingOrder = 20;
        aimLine.enabled = false;

        powerDot = CreateSprite("GucNoktasi", SpriteUtil.Circle(64, new Color(1f, 0.85f, 0.2f)), Vector3.zero, new Vector3(0.28f, 0.28f, 1f), 21);
        powerDot.gameObject.SetActive(false);
    }

    void BuildUi()
    {
        var canvasGo = new GameObject("UI");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        if (FindFirstObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }

        scoreText = CreateLabel(canvasGo.transform, "Score", new Vector2(0, -80), 52, FontStyle.Bold);
        hintText = CreateLabel(canvasGo.transform, "Hint", new Vector2(0, -170), 30, FontStyle.Normal);
        resultText = CreateLabel(canvasGo.transform, "Result", new Vector2(0, 220), 44, FontStyle.Bold);
        resultText.gameObject.SetActive(false);

        var buttonGo = new GameObject("Tekrar");
        buttonGo.transform.SetParent(canvasGo.transform, false);
        var rect = buttonGo.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0, 80);
        rect.sizeDelta = new Vector2(420, 110);
        var img = buttonGo.AddComponent<Image>();
        img.color = new Color(0.12f, 0.16f, 0.22f, 0.92f);
        restartButton = buttonGo.AddComponent<Button>();
        restartButton.onClick.AddListener(StartRound);
        var label = CreateLabel(buttonGo.transform, "BtnLabel", Vector2.zero, 36, FontStyle.Bold);
        label.text = "Tekrar oyna";
        label.color = Color.white;
        var labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        buttonGo.SetActive(false);
    }

    Text CreateLabel(Transform parent, string name, Vector2 anchored, int size, FontStyle style)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchored;
        rect.sizeDelta = new Vector2(980, 90);
        var text = go.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (text.font == null)
            text.font = Font.CreateDynamicFontFromOSFont("Arial", size);
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(0.08f, 0.1f, 0.14f);
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    Transform CreateSprite(string name, Sprite sprite, Vector3 pos, Vector3 scale, int order)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        go.transform.localScale = scale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;
        return go.transform;
    }

    void StartRound()
    {
        foreach (var car in cars)
        {
            if (car != null)
                Destroy(car.gameObject);
        }

        cars.Clear();
        nextCarIndex = 0;
        roundOver = false;
        dragging = false;
        aimLine.enabled = false;
        powerDot.gameObject.SetActive(false);
        resultText.gameObject.SetActive(false);
        restartButton.gameObject.SetActive(false);
        SpawnNextCar();
    }

    void SpawnNextCar()
    {
        if (nextCarIndex >= CarsPerRound)
        {
            FinishRound();
            return;
        }

        var go = new GameObject($"Araba_{nextCarIndex + 1}");
        go.transform.position = spawnPos;
        var rb = go.AddComponent<Rigidbody2D>();
        _ = rb;
        var car = go.AddComponent<CurlingCar>();
        car.Setup(CarColors[nextCarIndex % CarColors.Length], ice);
        cars.Add(car);
        activeCar = car;
        nextCarIndex++;
        hintText.text = "Arabayı geri çek, bırak — güç ortada dursun";
        RefreshHud();
    }

    void Update()
    {
        if (roundOver || activeCar == null)
            return;

        HandleSlingshot();

        if (activeCar.HasLaunched && AllCarsSettled())
            SpawnNextCar();
    }

    bool AllCarsSettled()
    {
        for (int i = 0; i < cars.Count; i++)
        {
            if (!cars[i].IsFullyStopped())
                return false;
        }

        return true;
    }

    void HandleSlingshot()
    {
        if (!activeCar.HasLaunched && PointerUtil.WasPressedThisFrame() && !IsPointerOverUi())
        {
            Vector3 world = PointerUtil.WorldOnPlane(cam);
            if (Vector2.Distance(world, activeCar.transform.position) < 2.4f || world.y < -5.2f)
                dragging = true;
        }

        if (dragging && PointerUtil.IsPressed())
        {
            Vector3 world = PointerUtil.WorldOnPlane(cam);
            Vector2 pull = (Vector2)(activeCar.transform.position - world);
            if (pull.sqrMagnitude < 0.0001f)
                pull = Vector2.up;
            float mag = Mathf.Clamp(pull.magnitude, 0f, 3.4f);
            Vector2 dir = pull.normalized;
            Vector3 end = (Vector3)((Vector2)activeCar.transform.position + dir * mag);
            aimLine.enabled = true;
            aimLine.SetPosition(0, activeCar.transform.position);
            aimLine.SetPosition(1, end);
            powerDot.gameObject.SetActive(true);
            powerDot.position = end;
            float t = mag / 3.4f;
            hintText.text = t < 0.55f ? "Az güç — hedefe ulaşmayabilir" : t < 0.78f ? "Güzel aralık — merkeze durdur" : "Fazla güç — hedefi geçersin";
        }

        if (dragging && PointerUtil.WasReleasedThisFrame())
        {
            Vector3 world = PointerUtil.WorldOnPlane(cam);
            Vector2 pull = (Vector2)(activeCar.transform.position - world);
            float mag = Mathf.Min(pull.magnitude, 3.4f);
            dragging = false;
            aimLine.enabled = false;
            powerDot.gameObject.SetActive(false);

            if (mag < 0.35f)
            {
                hintText.text = "Daha uzun çek, sonra bırak";
                return;
            }

            Vector2 impulse = pull.normalized * mag * 11.5f;
            activeCar.Launch(impulse);
            hintText.text = "Kaymasını izle — ustalık ortada durdurmak";
        }
    }

    bool IsPointerOverUi()
    {
        if (EventSystem.current == null)
            return false;
        return EventSystem.current.IsPointerOverGameObject();
    }

    int ScoreForPosition(Vector2 pos)
    {
        float dist = Vector2.Distance(pos, houseCenter);
        for (int i = 0; i < ringRadii.Length; i++)
        {
            if (dist <= ringRadii[i])
                return ringScores[i];
        }

        return 0;
    }

    int CurrentScore()
    {
        int total = 0;
        for (int i = 0; i < cars.Count; i++)
        {
            if (cars[i].HasLaunched)
                total += ScoreForPosition(cars[i].transform.position);
        }

        return total;
    }

    void RefreshHud(int? overrideScore = null)
    {
        int score = overrideScore ?? CurrentScore();
        int remaining = activeCar != null && !activeCar.HasLaunched
            ? CarsPerRound - nextCarIndex + 1
            : CarsPerRound - nextCarIndex;
        scoreText.text = $"Puan {score}   •   Kalan araba {remaining}";
    }

    void FinishRound()
    {
        roundOver = true;
        activeCar = null;
        int total = CurrentScore();
        int best = 0;
        float bestDist = 999f;
        foreach (var car in cars)
        {
            float d = Vector2.Distance(car.transform.position, houseCenter);
            if (d < bestDist)
            {
                bestDist = d;
                best = ScoreForPosition(car.transform.position);
            }
        }

        resultText.gameObject.SetActive(true);
        resultText.text = best >= 10
            ? $"Tur bitti! Toplam {total}\nTam isabet — merkeze kilitledin"
            : $"Tur bitti! Toplam {total}\nEn yakın taş {best} puan";
        restartButton.gameObject.SetActive(true);
        hintText.text = "Tekrar dene, bırakma gücünü milim milim ayarla";
        scoreText.text = $"Puan {total}   •   Tur tamam";
    }
}
