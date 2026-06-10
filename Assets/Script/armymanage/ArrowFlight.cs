using UnityEngine;

public class ArrowFlight : MonoBehaviour
{
    [HideInInspector]
    public ArrowRainVolley myPool;

    private Rigidbody rb;
    private bool hasHit = false;
    private TrailRenderer trail;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        trail = GetComponent<TrailRenderer>();
    }

    void OnEnable()
    {
        if (trail != null) trail.Clear();

        hasHit = false;

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        Invoke("Recycle", Random.Range(4f, 7f));
    }

    void OnDisable()
    {
        CancelInvoke("Recycle");
    }

    void Recycle()
    {
        if (myPool != null)
        {
            myPool.ReturnArrowToPool(this.gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Update()
    {
        if (!hasHit && rb != null && rb.velocity.sqrMagnitude > 0.1f)
        {
            transform.rotation = Quaternion.LookRotation(rb.velocity);
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (hasHit) return;

        hasHit = true;

        if (rb != null)
        {
            rb.isKinematic = true;
        }
    }
}
