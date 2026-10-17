using UnityEngine;
using UnityEngine.AI;

// A soi cat (แมวจร). Wanders between favourite spots (under the table, by the spirit house, in front of the
// chicken-rice stall...), sits, grooms, naps (longer at night and in the midday heat), meows now and then, and can
// be petted when you're close. The SM_Cat model comes in parts; at start they are rigged into a little hierarchy
// (pivot at the shoulders > body > head, tail, legs) so everything stays attached while the body sits up or lies down.
[RequireComponent(typeof(NavMeshAgent))]
public class StrayCat : MonoBehaviour, IInteractable
{
    public Transform[] spots;
    public Color fur = new Color(0.92f, 0.55f, 0.22f);
    public float walkSpeed = 0.75f;

    enum State { Walk, Sit, Sleep, Groom }
    State state = State.Sit;
    float stateTimer = 2f, phase, meowTimer, petCooldown, petGlow;
    NavMeshAgent agent;
    TimeCyc clock;
    Transform body, head, tail, legFL, legFR, legBL, legBR, pivot;
    Vector3 pivotPos, headPos;
    Quaternion pivotRot, headRot, tailRot, flRot, frRot, blRot, brRot;
    float sit, sleep;                                    // smoothed pose blends
    CustomerEmote emote;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = walkSpeed;
        agent.angularSpeed = 300f;
        agent.acceleration = 3f;
        agent.radius = 0.15f;
        agent.height = 0.35f;
        agent.avoidancePriority = 99;                  // people never step aside for a cat; the cat goes round them
        clock = FindFirstObjectByType<TimeCyc>();
        emote = GetComponent<CustomerEmote>();
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            string n = t.name.Split('.')[0];
            if (n == "Cat_Body") body = t;
            else if (n == "Cat_Head") head = t;
            else if (n == "Cat_Tail") tail = t;
            else if (n == "Cat_Leg_FL") legFL = t;
            else if (n == "Cat_Leg_FR") legFR = t;
            else if (n == "Cat_Leg_BL") legBL = t;
            else if (n == "Cat_Leg_BR") legBR = t;
        }
        Rig();
        Tint();
        meowTimer = Random.Range(8f, 25f);
        phase = Random.value * 10f;
    }

    // pivot at the front shoulders > body > head / tail / legs, keeping where everything is now
    void Rig()
    {
        if (body == null) return;
        pivot = new GameObject("CatPivot").transform;
        pivot.SetParent(body.parent, false);
        Vector3 shoulder = legFL != null && legFR != null ? (legFL.position + legFR.position) * 0.5f : body.position;
        pivot.SetPositionAndRotation(shoulder, body.parent.rotation);
        body.SetParent(pivot, true);
        foreach (Transform t in new[] { head, tail, legFL, legFR, legBL, legBR })
            if (t != null) t.SetParent(body, true);
        pivotPos = pivot.localPosition;
        pivotRot = pivot.localRotation;
        if (head != null) { headPos = head.localPosition; headRot = head.localRotation; }
        if (tail != null) tailRot = tail.localRotation;
        if (legFL != null) flRot = legFL.localRotation;
        if (legFR != null) frRot = legFR.localRotation;
        if (legBL != null) blRot = legBL.localRotation;
        if (legBR != null) brRot = legBR.localRotation;
    }

    // the fur material is shared by every cat: tint per cat with a property block
    void Tint()
    {
        var block = new MaterialPropertyBlock();
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null || !mats[i].name.StartsWith("M_cat_fur")) continue;
                r.GetPropertyBlock(block, i);
                block.SetColor("_BaseColor", fur);
                r.SetPropertyBlock(block, i);
            }
        }
    }

    float Hour => clock != null ? clock.TimeOfDay : 12f;
    bool Sleepy => Hour >= 21f || Hour < 6f || (Hour >= 12.5f && Hour < 14.5f);

    void Update()
    {
        if (agent == null || !agent.isOnNavMesh) return;
        petCooldown -= Time.deltaTime;
        stateTimer -= Time.deltaTime;
        if (state == State.Walk)
        {
            if (!agent.pathPending && agent.remainingDistance < 0.25f || stateTimer <= 0f) Settle();
        }
        else if (stateTimer <= 0f) Wander();

        meowTimer -= Time.deltaTime;
        if (meowTimer <= 0f)
        {
            meowTimer = Random.Range(15f, 40f);
            if (state != State.Sleep) GameAudio.PlayAt(Sfx.Meow, transform.position + Vector3.up * 0.3f, 0.7f, Random.Range(0.9f, 1.15f));
        }
        Animate();
    }

    void Wander()
    {
        if (spots == null || spots.Length == 0) { stateTimer = 5f; return; }
        Vector3 target = spots[Random.Range(0, spots.Length)].position + new Vector3(Random.Range(-0.4f, 0.4f), 0f, Random.Range(-0.4f, 0.4f));
        if (NavMesh.SamplePosition(target, out NavMeshHit hit, 1f, NavMesh.AllAreas))
        {
            agent.isStopped = false;
            agent.SetDestination(hit.position);
            state = State.Walk;
            stateTimer = 40f;
        }
        else stateTimer = 3f;
    }

    void Settle()
    {
        agent.isStopped = true;
        float r = Random.value;
        if (Sleepy && r < 0.7f || r < 0.25f) { state = State.Sleep; stateTimer = Random.Range(40f, 120f); }
        else if (r < 0.55f) { state = State.Groom; stateTimer = Random.Range(6f, 12f); }
        else { state = State.Sit; stateTimer = Random.Range(10f, 30f); }
    }

    // procedural poses: trotting legs, a swaying tail, sitting up, curling up to sleep, licking a paw.
    // Rotations are applied in the parent's axes (pre-multiplied), so the parts' own import rotations don't matter.
    void Animate()
    {
        if (pivot == null) return;
        float t = Time.time + phase, dt = Time.deltaTime;
        float speed = agent.velocity.magnitude;
        float gait = speed > 0.05f ? Mathf.Clamp01(speed / walkSpeed) : 0f;
        sit = Mathf.MoveTowards(sit, state == State.Sit || state == State.Groom ? 1f : 0f, dt * 2.5f);
        sleep = Mathf.MoveTowards(sleep, state == State.Sleep ? 1f : 0f, dt * 1.2f);
        float stride = Mathf.Sin(t * 9f) * 28f * gait;

        // sitting: the rear drops around the shoulders (front paws stay planted); sleeping: down on the ground
        pivot.localRotation = Quaternion.Euler(-16f * sit * (1f - sleep), 0f, 7f * sleep) * pivotRot;
        pivot.localPosition = pivotPos + new Vector3(0f, -0.085f * sleep + Mathf.Abs(Mathf.Sin(t * 9f)) * 0.006f * gait, 0f);
        float frontKeep = 16f * sit * (1f - sleep);                                   // front legs stay upright when sitting
        Leg(legFL, flRot, stride + frontKeep + 80f * sleep);
        Leg(legFR, frRot, -stride + frontKeep + 80f * sleep);
        Leg(legBL, blRot, -stride - 45f * sit * (1f - sleep) - 85f * sleep);
        Leg(legBR, brRot, stride - 45f * sit * (1f - sleep) - 85f * sleep);

        if (head != null)
        {
            float look = Mathf.Sin(t * 0.7f) * 25f * (1f - sleep);
            float groom = state == State.Groom ? 22f + Mathf.Sin(t * 6f) * 12f : 0f;
            head.localRotation = Quaternion.Euler(groom + 22f * sleep - 10f * sit * (1f - sleep), look, 12f * sleep) * headRot;
            head.localPosition = headPos + new Vector3(0f, -0.04f * sleep, 0f);
        }
        if (tail != null)
        {
            float sway = Mathf.Sin(t * (petGlow > 0f ? 6f : 1.8f)) * (sleep > 0.5f ? 6f : 22f);
            tail.localRotation = Quaternion.Euler(-55f * sleep, sway, 0f) * tailRot;
        }
        petGlow -= dt;
    }

    static void Leg(Transform leg, Quaternion rest, float angle)
    {
        if (leg != null) leg.localRotation = Quaternion.Euler(angle, 0f, 0f) * rest;
    }

    // ---------------------------------------------------------------- petting
    public string Prompt(RaycastAim player) => Loc.T(state == State.Sleep ? "ลูบแมว (กำลังหลับ)" : "ลูบแมว", state == State.Sleep ? "Pet the cat (napping)" : "Pet the cat");

    public void Interact(RaycastAim player)
    {
        if (petCooldown > 0f) return;
        petCooldown = 2.5f;
        petGlow = 3f;
        if (state == State.Walk) Settle();
        state = State.Sit;
        stateTimer = Mathf.Max(stateTimer, 8f);
        GameAudio.PlayAt(Sfx.Meow, transform.position + Vector3.up * 0.3f, 0.8f, 1.15f);
        if (emote != null) emote.Show(CustomerEmote.Kind.Love, 2f);
        StatsSystem.Add(StatsSystem.Stat.CatPet);
        ShopHUD.ToastNow(Loc.T("เหมียว~ แมวชอบใจ", "Meow~ the cat loves it"));
    }
}
