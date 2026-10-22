using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class CurlingCar : MonoBehaviour
{
    public Rigidbody2D Body { get; private set; }

    float stillTimer;
    bool launched;

    public bool IsMoving => Body.linearVelocity.sqrMagnitude > 0.012f || Mathf.Abs(Body.angularVelocity) > 8f;
    public bool HasLaunched => launched;

    public void Setup(Color color, PhysicsMaterial2D ice)
    {
        Body = GetComponent<Rigidbody2D>();
        Body.gravityScale = 0f;
        Body.mass = 1f;
        Body.linearDamping = 1.32f;
        Body.angularDamping = 4.5f;
        Body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        Body.interpolation = RigidbodyInterpolation2D.Interpolate;
        Body.freezeRotation = false;
        Body.sharedMaterial = ice;

        var col = gameObject.AddComponent<CapsuleCollider2D>();
        col.size = new Vector2(0.84f, 0.74f);
        col.sharedMaterial = ice;

        var body = CreateChildSprite("Body", SpriteUtil.Rect(48, 80, color), new Vector3(0.56f, 0.30f, 1f), 10);
        var glass = CreateChildSprite("Glass", SpriteUtil.Rect(32, 22, new Color(0.55f, 0.85f, 1f, 0.95f)), new Vector3(0.40f, 0.24f, 1f), 11);
        glass.localPosition = new Vector3(0f, 0.14f, 0f);
        var stripe = CreateChildSprite("Stripe", SpriteUtil.Rect(40, 10, Color.white), new Vector3(0.44f, 0.18f, 1f), 11);
        stripe.localPosition = new Vector3(0f, -0.1f, 0f);
        _ = body;
    }

    Transform CreateChildSprite(string name, Sprite sprite, Vector3 scale, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localScale = scale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;
        return go.transform;
    }

    public void Launch(Vector2 impulse)
    {
        launched = true;
        stillTimer = 0f;
        Body.AddForce(impulse, ForceMode2D.Impulse);
        float angle = Mathf.Atan2(impulse.y, impulse.x) * Mathf.Rad2Deg - 90f;
        Body.SetRotation(angle);
    }

    public bool IsFullyStopped()
    {
        if (!launched)
            return true;
        if (IsMoving)
        {
            stillTimer = 0f;
            return false;
        }

        stillTimer += Time.deltaTime;
        Body.linearVelocity = Vector2.zero;
        Body.angularVelocity = 0f;
        return stillTimer > 0.35f;
    }

    void FixedUpdate()
    {
        if (!launched || !IsMoving)
            return;

        Vector2 v = Body.linearVelocity;
        float angle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg - 90f;
        Body.MoveRotation(Mathf.LerpAngle(Body.rotation, angle, 12f * Time.fixedDeltaTime));
    }
}
