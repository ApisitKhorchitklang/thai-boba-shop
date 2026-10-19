using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

// Decorations, the helper and the expansion next door: built hidden, switched on by DecorShop when bought.
public static partial class ThaiBobaSetup
{
    static GameObject DecorGroup(StreetRefs r, Transform parent, string id)
    {
        GameObject g = new GameObject("Decor_" + id);
        g.transform.SetParent(parent, false);
        r.decor[id] = g;
        return g;
    }

    // keeps people walking around footpath props that appear after the NavMesh was baked
    static void Obstacle(GameObject go, Vector3 size)
    {
        NavMeshObstacle o = go.AddComponent<NavMeshObstacle>();
        o.carving = true;
        o.shape = NavMeshObstacleShape.Box;
        o.size = size;
        o.center = new Vector3(0, size.y / 2f, 0);
    }

    // tables in the annex (shophouse-local x, y), same as ANNEX_TABLES in thai_boba_assets.py (the lamps hang over them)
    static readonly (float, float)[] AnnexTables = { (-1.25f, 2.0f), (0.35f, 2.8f), (-1.25f, 4.4f), (0.35f, 5.2f) };

    static void BuildDecor(StreetRefs r, Transform shop, Transform env)
    {
        // potted plants either side of the counter opening
        GameObject plants = DecorGroup(r, shop, "plants");
        int pk = 0;
        foreach (float x in new[] { -1.35f, 1.35f })
            Obstacle(r.Movable("plants_" + pk++, Place("SM_PottedPlant", x, -0.35f, 0, 0, plants.transform, Col.None), 0.32f), new Vector3(0.6f, 1.2f, 0.6f));

        // bunting along the front edge of the awning, fairy lights under it
        Place("SM_Bunting", 0, -1.5f, 2.66f, 0, DecorGroup(r, shop, "bunting").transform, Col.None);
        Place("SM_FairyLights", 0, -0.3f, 2.62f, 0, DecorGroup(r, shop, "lights").transform, Col.None);

        // lucky cat at the left end of the counter, next to the till, waving at customers
        Place("SM_LuckyCat", -1.1f, 1.25f, 1.0f, 15, DecorGroup(r, shop, "cat").transform, Col.None);      // beside the till (the right end holds the delivery rack)

        // mural on the right-hand inner wall, between the fan and the ice cooler
        Place("SM_Mural", 2.07f, 4.1f, 0, -90, DecorGroup(r, shop, "mural").transform, Col.None);

        // bench out front, left of the counter
        Obstacle(r.Movable("bench", Place("SM_FrontBench", -2.3f, -0.45f, 0, 0, DecorGroup(r, shop, "bench").transform, Col.None), 0.65f), new Vector3(1.3f, 1f, 0.5f));

        // the helper: stands at the back counter beside the pearl pot (idle animation)
        GameObject helper = DecorGroup(r, shop, "helper");
        GameObject npc = AssetDatabase.LoadAssetAtPath<GameObject>(CharDir + "NPC_Vendor.fbx");
        if (npc != null)
        {
            GameObject h = (GameObject)PrefabUtility.InstantiatePrefab(npc, helper.transform);
            h.name = "Helper_NPC_Vendor";
            SetupLods(h, 0.11f, 0.012f);
            h.transform.localPosition = P(1.2f, 5.85f, 0);
            h.transform.localRotation = Quaternion.Euler(0, 180f, 0);                 // facing the back counter, working
            Animator anim = h.GetComponent<Animator>();
            if (anim == null) anim = h.AddComponent<Animator>();
            // reuse the controller the customer prefab already made (NpcController() recreates the asset and would break it)
            var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AnimDir + "NPC_Vendor.controller");
            anim.runtimeAnimatorController = ctrl != null ? ctrl : NpcController("NPC_Vendor");
            anim.applyRootMotion = false;
        }
        helper.AddComponent<HelperStaff>();

        // expansion: the shophouse next door becomes our seating room ("chill zone"). Our own shell is swapped for
        // one with the party wall knocked through at the front (the original + the pharmacy are hidden, see
        // BuildStreet), and served customers walk in and sit at the tables with their drinks.
        GameObject exp = DecorGroup(r, env, "expand");
        Place("SM_Shophouse_BobaShopOpen", 0, 0, 0, 0, exp.transform, Col.Mesh);
        Place("SM_Shophouse_BobaAnnex", -4.6f, 0, 0, 0, exp.transform, Col.Mesh);
        const float ax = -4.6f, aisle = 1.5f;                     // annex centre, walkway along the open side
        Vector3 W(float lx, float ly) => exp.transform.TransformPoint(P(ax + lx, ly, 0));
        Vector3 door = W(aisle, -0.45f);
        foreach (var (tx, ty) in AnnexTables)
        {
            GameObject table = Place("SM_SteelTable", ax + tx, ty, 0, 0, exp.transform, Col.Box);
            foreach (float dx in new[] { -0.55f, 0.55f })
            {
                GameObject stool = Place("SM_PlasticStool", ax + tx + dx, ty, 0, dx < 0 ? 0 : 25, exp.transform, Col.Box);
                if (stool == null || table == null) continue;
                SeatSpot seat = stool.AddComponent<SeatSpot>();
                seat.table = table.transform;
                seat.hasDoor = true;
                seat.door = door;
                float ay = ty - 0.55f;                            // the row in front of the stools (street side)
                seat.route = new[] { W(aisle, 0.35f), W(aisle, ay), W(tx + dx, ay) };
            }
        }
        Place("SM_FairyLights", -4.6f, -0.3f, 2.62f, 0, exp.transform, Col.None);
        Place("SM_PottedPlant", -6.55f, 6.4f, 0, 0, exp.transform, Col.None);
        Place("SM_PottedPlant", -2.75f, 6.4f, 0, 30, exp.transform, Col.None);
        GameObject lamp = new GameObject("AnnexLight");
        lamp.transform.SetParent(exp.transform, false);
        lamp.transform.localPosition = P(-4.6f, 3.6f, 3.0f);
        Light l = lamp.AddComponent<Light>();
        l.type = LightType.Point;
        l.range = 6.5f;
        l.intensity = 1.8f;
        l.color = new Color(1f, 0.9f, 0.75f);                     // warmer than the counter side: a place to sit

        foreach (GameObject g in r.decor.Values) g.SetActive(false);
    }
}
