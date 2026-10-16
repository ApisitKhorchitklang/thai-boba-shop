using System.Collections.Generic;
using UnityEngine;

public enum CustomerType { Student, OfficeWorker, Tourist, Auntie, Rider, Influencer }

// Customer archetypes of a Thai soi: who they are, how long they wait, how they pay, what they say.
public static class CustomerProfiles
{
    public class Profile
    {
        public CustomerType type;
        public string labelTH;
        public string labelEN;
        public string Label => Loc.T(labelTH, string.IsNullOrEmpty(labelEN) ? labelTH : labelEN);
        public float patience;          // seconds
        public float payMultiplier;
        public int tipBase;             // baht, scaled by remaining patience
        public string[] favorites;      // preferred drink ids (60% of orders)
        public int[] sweetness;         // sweetness levels they pick from
        public float iceChance;
        public float bobaChance;
        public string[] greet, thanks, okay, wrong, angry;
    }

    static readonly Dictionary<CustomerType, Profile> profiles = new Dictionary<CustomerType, Profile>
    {
        [CustomerType.Student] = new Profile
        {
            type = CustomerType.Student, labelTH = "นักเรียน", labelEN = "Student", patience = 45f, payMultiplier = 1f, tipBase = 3,
            favorites = new[] { "PinkMilk", "MilkTea", "Cocoa" }, sweetness = new[] { 100, 120, 100 },
            iceChance = 0.95f, bobaChance = 0.9f,
            greet = new[] { "พี่คะ ขอ{drink}ค่ะ|One {drink} please!", "พี่ครับ {drink}แก้วนึงครับ|Hi! Can I get a {drink}?" },
            thanks = new[] { "ขอบคุณค่า อร่อยมาก!|Thank you, so yummy!", "เย้! ได้แล้ว|Yay, got it!" },
            okay = new[] { "อืม...ก็ได้ค่ะ|Hmm... it's fine", "ไม่เป๊ะแต่โอเคครับ|Not perfect, but okay" },
            wrong = new[] { "พี่ อันนี้ไม่ใช่ที่สั่งนะคะ|Um, this isn't what I ordered" },
            angry = new[] { "ช้าจัง ไปร้านอื่นดีกว่า|Too slow, I'll go somewhere else", "เข้าเรียนสายแล้ว!|Now I'm late for class!" },
        },
        [CustomerType.OfficeWorker] = new Profile
        {
            type = CustomerType.OfficeWorker, labelTH = "พนักงานออฟฟิศ", labelEN = "Office worker", patience = 35f, payMultiplier = 1f, tipBase = 10,
            favorites = new[] { "ThaiTea", "Matcha", "MilkTea" }, sweetness = new[] { 25, 50, 50, 100 },
            iceChance = 0.85f, bobaChance = 0.5f,
            greet = new[] { "ขอ{drink}เร็วหน่อยนะครับ จะเข้าประชุม|A {drink}, quickly please, I have a meeting", "{drink}ค่ะ รีบนิดนึงนะคะ|{drink} please, I'm in a bit of a hurry" },
            thanks = new[] { "ขอบคุณครับ ทันพอดี!|Thanks, just in time!", "เร็วมากค่ะ ไว้มาอีกนะ|So fast! I'll be back" },
            okay = new[] { "เอาเถอะ ไม่มีเวลาแล้ว|Whatever, no time to argue" },
            wrong = new[] { "ผิดออเดอร์ครับ ผมไม่มีเวลานะ|Wrong order, and I don't have time for this" },
            angry = new[] { "ไม่ทันแล้ว ไปละ!|No time, I'm off!", "สายแล้วค่ะ ไม่รอแล้ว|I'm late, not waiting anymore" },
        },
        [CustomerType.Tourist] = new Profile
        {
            type = CustomerType.Tourist, labelTH = "นักท่องเที่ยว", labelEN = "Tourist", patience = 75f, payMultiplier = 1.3f, tipBase = 20,
            favorites = new[] { "ThaiTea", "ThaiTea", "Taro" }, sweetness = new[] { 50, 100 },
            iceChance = 1f, bobaChance = 0.8f,
            greet = new[] { "Sawasdee krub! One {drink}, please", "Hello! {drink} khob khun ka" },
            thanks = new[] { "Aroi mak! Thank you!", "So good! ขอบคุณครับ|So good! Khob khun krub!" },
            okay = new[] { "Hmm... close enough!" },
            wrong = new[] { "Sorry, this is not my order" },
            angry = new[] { "Too long... bye bye" },
        },
        [CustomerType.Auntie] = new Profile
        {
            type = CustomerType.Auntie, labelTH = "ป้าข้างบ้าน", labelEN = "Auntie next door", patience = 90f, payMultiplier = 1f, tipBase = 5,
            favorites = new[] { "ThaiTea", "MilkTea", "Cocoa" }, sweetness = new[] { 0, 25, 25 },
            iceChance = 0.6f, bobaChance = 0.3f,
            greet = new[] { "ลูก ป้าเอา{drink}นะจ๊ะ|Dear, Auntie will have a {drink}", "{drink}จ้ะ หวานน้อยๆ นะลูก เบาหวานถามหา|{drink}, dear, not too sweet, my blood sugar!" },
            thanks = new[] { "เก่งจริงลูก เดี๋ยวป้ามาอีกนะ|Clever girl/boy, Auntie will be back", "ขายดีๆ นะลูก|May your shop do well, dear" },
            okay = new[] { "ไม่เป็นไรจ้ะ คราวหน้าระวังหน่อยนะ|Never mind, be more careful next time" },
            wrong = new[] { "ไม่ใช่จ้ะลูก ป้าสั่งอีกอย่าง|No dear, Auntie ordered something else" },
            angry = new[] { "โอ๊ย รอนานเหลือเกิน ป้ากลับละ|Oh my, so slow, Auntie is going home" },
        },
        [CustomerType.Rider] = new Profile
        {
            type = CustomerType.Rider, labelTH = "ไรเดอร์", labelEN = "Delivery rider", patience = 28f, payMultiplier = 1f, tipBase = 8,
            favorites = new[] { "ThaiTea", "Matcha", "Taro", "RedLime" }, sweetness = new[] { 50, 100 },
            iceChance = 1f, bobaChance = 0.7f,
            greet = new[] { "ออเดอร์แอปครับ {drink} ด่วนเลยพี่!|App order: one {drink}, urgent!", "พี่ {drink}ครับ ลูกค้ารออยู่!|{drink} please, my customer is waiting!" },
            thanks = new[] { "ขอบคุณพี่ ไปละ!|Thanks, gotta go!", "ไวมากพี่ ได้ดาวเต็มแน่|So fast, that's five stars for sure" },
            okay = new[] { "เอาไปก่อนละพี่|I'll take it anyway" },
            wrong = new[] { "พี่ ผิดแก้วครับ|That's the wrong drink" },
            angry = new[] { "ลูกค้ายกเลิกแล้วพี่!|My customer cancelled!" },
        },
        [CustomerType.Influencer] = new Profile
        {
            type = CustomerType.Influencer, labelTH = "รีวิวเวอร์", labelEN = "Reviewer", patience = 60f, payMultiplier = 1f, tipBase = 50,
            favorites = new[] { "Taro", "Matcha", "PinkMilk", "RedLime" }, sweetness = new[] { 25, 50 },
            iceChance = 1f, bobaChance = 1f,
            greet = new[] { "สวัสดีค่ะ วันนี้มารีวิว{drink}ค่ะ ทำให้ปังๆ นะคะ|Hi! I'm reviewing your {drink} today, make it amazing" },
            thanks = new[] { "ปังมาก! ให้ 5 ดาวเลยค่ะ|Amazing! Five stars!" },
            okay = new[] { "ก็...โอเคค่ะ ให้ 3 ดาวนะ|It's... okay. Three stars" },
            wrong = new[] { "อุ๊ย ไม่ตรงกับที่สั่งค่ะ|Oops, that's not what I ordered" },
            angry = new[] { "รอนานมาก เดี๋ยวรีวิวตามจริงนะคะ...|Such a long wait... my review will be honest" },
        },
    };

    public static Profile Get(CustomerType type)
    {
        return profiles[type];
    }

    // Who walks in depends on the hour and the day's event
    public static CustomerType Roll(int hour)
    {
        DailyEventSystem ev = DailyEventSystem.Instance;
        if (ev != null && ev.ConsumeInfluencer())
            return CustomerType.Influencer;

        float student = 2f, office = 2f, tourist = 1f, auntie = 1.5f, rider = 1.5f;
        if (hour >= 7 && hour <= 8) { student += 3f; office += 3f; }
        else if (hour >= 11 && hour <= 13) { office += 4f; rider += 3f; }
        else if (hour >= 15 && hour <= 17) { student += 5f; }
        else if (hour >= 18) { tourist += 2f; auntie += 1f; rider += 2f; }

        if (ev != null)
        {
            student *= ev.Current.studentMultiplier;
            office *= ev.Current.officeMultiplier;
            tourist *= ev.Current.touristMultiplier;
        }

        float total = student + office + tourist + auntie + rider;
        float r = Random.value * total;
        if ((r -= student) < 0) return CustomerType.Student;
        if ((r -= office) < 0) return CustomerType.OfficeWorker;
        if ((r -= tourist) < 0) return CustomerType.Tourist;
        if ((r -= auntie) < 0) return CustomerType.Auntie;
        return CustomerType.Rider;
    }

    public static Order CreateOrder(Profile p, int shopLevel, bool forceIce)
    {
        List<DrinkMenu.Drink> menu = DrinkMenu.Unlocked(Mathf.Max(1, shopLevel));
        string flavor = null;
        if (Random.value < 0.6f)
        {
            string fav = p.favorites[Random.Range(0, p.favorites.Length)];
            if (menu.Exists(d => d.id == fav)) flavor = fav;
        }
        if (flavor == null)
            flavor = menu[Random.Range(0, menu.Count)].id;

        WeatherSystem w = WeatherSystem.Instance;
        float iceChance = p.iceChance;
        if (w != null)
        {
            if (w.IsRaining || w.CurrentSeason == WeatherSystem.Season.Cool)
            {
                iceChance *= 0.35f;                                   // rainy / cool days: warm drinks
                if (Random.value < 0.4f)
                {
                    string[] warm = { "Cocoa", "MilkTea", "ThaiTea" };
                    string pick = warm[Random.Range(0, warm.Length)];
                    if (menu.Exists(d => d.id == pick)) flavor = pick;
                }
            }
            else if (w.CurrentSeason == WeatherSystem.Season.Hot) iceChance = Mathf.Max(iceChance, 0.95f);
        }
        int boba = Random.value < p.bobaChance ? Random.Range(1, 4) : 0;
        bool ice = forceIce || Random.value < iceChance;
        int sweet = p.sweetness[Random.Range(0, p.sweetness.Length)];
        return new Order(boba, ice, flavor, sweet);
    }

    public static string Line(string[] lines, Order order)
    {
        if (lines == null || lines.Length == 0) return "";
        string line = lines[Random.Range(0, lines.Length)];
        int bar = line.IndexOf('|');                       // "ไทย|English"; lines without one are the same in both
        if (bar >= 0) line = Loc.EN ? line.Substring(bar + 1) : line.Substring(0, bar);
        return line.Replace("{drink}", order != null ? order.DrinkNameTH : "");
    }
}
