using UnityEngine;

// Shows one NPC_* model that matches the customer type (or a random one for pedestrians).
public class CustomerAppearance : MonoBehaviour
{
    [Tooltip("Parent holding the NPC_* model instances. Defaults to this transform.")]
    public Transform modelsRoot;

    void Start()
    {
        if (modelsRoot == null) modelsRoot = transform;
        int count = modelsRoot.childCount;
        if (count == 0) return;

        Transform pick = null;
        CustomerController customer = GetComponentInParent<CustomerController>();
        if (customer != null)
        {
            if (customer.regular != null) pick = modelsRoot.Find(customer.regular.model);   // regulars always look the same
            string[] names = ModelNames(customer.customerType);
            for (int tries = 0; tries < 6 && pick == null; tries++)
                pick = modelsRoot.Find(names[Random.Range(0, names.Length)]);
        }
        if (pick == null) pick = modelsRoot.GetChild(Random.Range(0, count));

        for (int i = 0; i < count; i++)
        {
            Transform c = modelsRoot.GetChild(i);
            c.gameObject.SetActive(c == pick);
        }
    }

    // Models that fit each customer type's lines and habits
    public static string[] ModelNames(CustomerType type)
    {
        switch (type)
        {
            case CustomerType.Student: return new[] { "NPC_Student", "NPC_SchoolBoy", "NPC_UniStudent" };
            case CustomerType.OfficeWorker: return new[] { "NPC_Office" };
            case CustomerType.Tourist: return new[] { "NPC_Tourist" };
            case CustomerType.Auntie: return new[] { "NPC_Auntie", "NPC_Vendor" };
            case CustomerType.Rider: return new[] { "NPC_Rider" };
            default: return new[] { "NPC_Influencer" };
        }
    }
}
