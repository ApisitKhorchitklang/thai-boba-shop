using System.Collections.Generic;

public enum ServeQuality { Wrong, Okay, Perfect }

// Grades a cup against an order. Wrong flavour or unsealed cup = rejected;
// small mistakes (pearls / ice / sweetness) are accepted but earn no tip and break the combo.
public class ServeResult
{
    public ServeQuality quality;
    public readonly List<string> mistakes = new List<string>();

    public string Summary => mistakes.Count == 0 ? "" : string.Join(", ", mistakes);

    public static ServeResult Evaluate(Order order, CupController cup)
    {
        ServeResult r = new ServeResult();

        if (cup.State != CupController.CupState.LidClosed)
        {
            r.mistakes.Add(Loc.T("ยังไม่ได้ซีลฝา", "not sealed"));
            r.quality = ServeQuality.Wrong;
            return r;
        }
        if (cup.watered)
        {
            r.mistakes.Add(Loc.T("แก้วโดนน้ำสงกรานต์", "soaked by Songkran water"));
            r.quality = ServeQuality.Wrong;
            return r;
        }
        if (cup.flavor != order.flavor)
        {
            r.mistakes.Add(Loc.T("ผิดเมนู", "wrong drink"));
            r.quality = ServeQuality.Wrong;
            return r;
        }

        if (cup.bobaCount != order.bobaCount) r.mistakes.Add(Loc.T("ไข่มุกไม่ตรง", "wrong pearls"));
        if (cup.hasIce != order.wantsIce) r.mistakes.Add(order.wantsIce ? Loc.T("ลืมใส่น้ำแข็ง", "forgot the ice") : Loc.T("ไม่เอาน้ำแข็ง", "didn't want ice"));
        if (cup.sweetness != order.sweetness) r.mistakes.Add(Loc.T("ความหวานไม่ตรง", "wrong sweetness"));
        if (cup.stalePearls && cup.bobaCount > 0) r.mistakes.Add(Loc.T("ไข่มุกแข็ง", "hard pearls"));
        if (cup.blended != order.blended) r.mistakes.Add(order.blended ? Loc.T("ลืมปั่น", "forgot to blend") : Loc.T("ไม่ได้สั่งปั่น", "didn't want it blended"));
        if (cup.marshmallow != order.marshmallow) r.mistakes.Add(order.marshmallow ? Loc.T("ลืมมาร์ชเมลโลว์", "forgot the marshmallows") : Loc.T("ไม่ได้สั่งมาร์ชเมลโลว์", "didn't want marshmallows"));
        if (cup.hasFoam != order.wantsFoam) r.mistakes.Add(order.wantsFoam ? Loc.T("ลืมท็อปชีสโฟม", "forgot the cheese foam") : Loc.T("ไม่ได้สั่งชีสโฟม", "didn't order cheese foam"));

        r.quality = r.mistakes.Count == 0 ? ServeQuality.Perfect : ServeQuality.Okay;
        return r;
    }
}
