using UnityEngine;
using UnityEngine.AI;

// A soi cat (แมวจร). Wanders between favourite spots (under the table, by the spirit house, in front of the
// chicken-rice stall...), sits, grooms, naps (longer at night and in the midday heat), meows now and then, and can
// be petted when you're close. The SM_Cat model comes in parts; legs, tail and head are animated here.
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
    Transform body, head, tail, legFL, legFR, legBL, legBR;
    Vector3 bodyPos, headPos;
    Quaternion bodyRot, headRot, tailRot, legRot;
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
        if (body != null) { bodyPos = body.localPosition; bodyRot = body.localRotation; }
        if (head != null) { headPos = head.localPosition; headRot = head.localRotation; }
        if (tail != null) tailRot = tail.localRotation;
        if (legFL != null) legRot = legFL.localRotation;
        Tint();
        meowTimer = Random.Range(8f, 25f);
        phase = Random.value * 10f;
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

    // procedural poses: trotting legs, a swaying tail, sitting up, curling up to sleep, licking a paw
    void Animate()
    {
        float t = Time.time + phase;
        float speed = agent.velocity.magnitude;
        float gait = speed > 0.05f ? Mathf.Clamp01(speed / walkSpeed) : 0f;
        float swing = Mathf.Sin(t * 9f) * 28f * gait;
        Quaternion Leg(float a) => legRot * Quaternion.Euler(a, 0f, 0f);
        float sit = state == State.Sit || state == State.Groom ? 1f : 0f;
        float sleep = state == State.Sleep ? 1f : 0f;
        if (legFL != null) legFL.localRotation = Quaternion.Slerp(legFL.localRotation, Leg(swing - 70f * sleep), Time.deltaTime * 10f);
        if (legBR != null) legBR.localRotation = Quaternion.Slerp(legBR.localRotation, Leg(swing + 75f * sleep - 60f * sit), Time.deltaTime * 10f);
        if (legFR != null) legFR.localRotation = Quaternion.Slerp(legFR.localRotation, Leg(-swing - 70f * sleep), Time.deltaTime * 10f);
        if (legBL != null) legBL.localRotation = Quaternion.Slerp(legBL.localRotation, Leg(-swing + 75f * sleep - 60f * sit), Time.deltaTime * 10f);
        if (body != null)
        {
            // sitting: haunches down, chest up; sleeping: flat on the ground
            Quaternion bRot = bodyRot * Quaternion.Euler(-24f * sit, 0f, 4f * sleep);
            Vector3 bPos = bodyPos + new Vector3(0f, -0.1f * sleep - 0.035f * sit + Mathf.Abs(Mathf.Sin(t * 9f)) * 0.008f * gait, 0f);
            body.localRotation = Quaternion.Slerp(body.localRotation, bRot, Time.deltaTime * 4f);
            body.localPosition = Vector3.Lerp(body.localPosition, bPos, Time.deltaTime * 4f);
        }
        if (head != null)
        {
            float look = Mathf.Sin(t * 0.7f) * 25f * (1f - sleep);
            float groom = state == State.Groom ? Mathf.Sin(t * 6f) * 12f + 25f : 0f;
            Quaternion hRot = headRot * Quaternion.Euler(groom + 20f * sleep, look, 15f * sleep);
            Vector3 hPos = headPos + new Vector3(0f, -0.12f * sleep + 0.03f * sit, 0.02f * sit);
            head.localRotation = Quaternion.Slerp(head.localRotation, hRot, Time.deltaTime * 5f);
            head.localPosition = Vector3.Lerp(head.localPosition, hPos, Time.deltaTime * 4f);
        }
        if (tail != null)
        {
            float sway = Mathf.Sin(t * (petGlow > 0f ? 6f : 1.8f)) * (sleep > 0f ? 6f : 22f);
            tail.localRotation = Quaternion.Slerp(tail.localRotation, tailRot * Quaternion.Euler(-60f * sleep, sway, 0f), Time.deltaTime * 6f);
        }
        petGlow -= Time.deltaTime;
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
