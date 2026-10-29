using UnityEngine;

//Every note, line and ending in one place (from "Horror jam 2026 - Sorry, Daughter"). Edit the text here
public enum LoreNote { None, Note1_FakeDoor, Note2_ToolRoom, Note3_NukeRoom, Note4_Secret, Note5_Machine, Note6_DaughterCage }

public static class Lore
{
    // ---------- Notes ----------

    public static string NoteTitle(LoreNote note)
    {
        switch(note)
        {
            case LoreNote.Note1_FakeDoor: return "Memo";
            case LoreNote.Note2_ToolRoom: return "To Dr. Sidorov";
            case LoreNote.Note3_NukeRoom: return "What a shame";
            case LoreNote.Note4_Secret: return "СОВЕРШЕННО СЕКРЕТНО";
            case LoreNote.Note5_Machine: return "Dear Santa";
            case LoreNote.Note6_DaughterCage: return "Dear f***er";
        }
        return "Note";
    }

    public static string NoteBody(LoreNote note)
    {
        switch(note)
        {
            case LoreNote.Note1_FakeDoor:
                return "I will soon return from Berlin to inspect project \"I\".\n" +
                       "I hope you have prepared some update to report to the general.";
            case LoreNote.Note2_ToolRoom:
                return "I'm so done with your bulls**t Dr. Sidorov, when will you stop working on side projects and fully focus on what we have to do?\n\n" +
                       "What if the war becomes heated and has a full effect on us?\n\n" +
                       "We will all die eventually but our discoveries must be passed on to different living beings and you know damn well that only project \"I\" can make this work.\n\n" +
                       "If you read this, you better get your a** to work.";
            case LoreNote.Note3_NukeRoom:
                return "What a shame.\n\n" +
                       "When that contract was formed, it's like all funds spent on this radioactive bomb was a waste.\n\n" +
                       "With all other labs unwilling to take care of this thing, it has to stay here temporarily.\n\n" +
                       "But hey, with 1 project less, that guy will surely work on that occultish project to please the general right? Hope so, I don't want to work on shady stuff like that.";
            case LoreNote.Note4_Secret:
                return "The paper contains a receipt from higher officials. Sending funds to the general to support our lab for technology advancement but somehow, most of the money is gone with no proper explanation.";
            case LoreNote.Note5_Machine:
                return "Dear santa\n\n" +
                       "I want a mommy and daddy together.\n" +
                       "Because when mommy leave, my dad do work nonstop.\n" +
                       "One day, he need my help. I go help dad.\n" +
                       "He put needles in my arms, it hurt but i do it because if i dont help dad my mom will not come back.\n" +
                       "my arm become big and I feel dizzy all time. I can't stand it anymore.\n\n" +
                       "Make mommy come back so i dont have to help daddy\n\n" +
                       "I promise I be a good girl.\n" +
                       "Please santa";
            case LoreNote.Note6_DaughterCage:
                return "Dear f***er\n\n" +
                       "Why don't you just go die?\n\n" +
                       "Why do you support the idea of sacrificing those poor people for few who actually gained something?\n\n" +
                       "Why is it this time, money? thrill of experiment? or whatever inside that f***ed up little head of yours?\n\n" +
                       "F*** you. And f*** your immortality (БЕССМЕРТИЕ) project. I don't care if the upper ranks that you lick their boots come looking for me. I'm leaking this project EVERYWHERE.\n\n" +
                       "Me and everyone will come looking for you.\n\n" +
                       "PS. I ask you to return MY daughter and you just turn a blind eye? The court may say that she is yours but I'm taking her back anyway. Don't even think about making her part of your projects.\n\n" +
                       "You are so dead";
        }
        return "";
    }

    // ---------- The scientist talking to himself ----------

    public const string FakeDoor = "I rather not go out that door.";
    public static readonly string[] ScannerLocked = { "I need that b****'s fingerprint.", "It's locked." };
    public const string StartMixing = "I must get this right.";
    public const string MachineNoTools = "I need my equipment, somewhere near the nuke...";
    public const string HalfBody = "I remember having his torso somewhere around here. Weird, where did they go?";
    public static readonly string[] GainItem = { "This can be useful.", "Absolutely necessary.", "Aahaa." };
    public const string SmallMonsterChase = "I have to end your purpose.";
    public static readonly string[] DaughterChase = { "F***ing hell.", "Get away from me!" };
    public const string GeneratorStarts = "I'm getting out of here.";
    public static readonly string[] BodySwap = { "Ahhh.. F***", "Bloody hell" };
    public const string ResearcherKilled = "Loud a** b****.";
    public const string TakeHerBody = "I can't let her out. Not like this.";

    //People running around while you're in her body (DaughterTransfer). Some only scream...
    public static readonly string[] NpcPanic =
    {
        "It's loose! IT'S LOOSE!",
        "Don't look at it, just run!",
        "Where's the doctor?! Somebody find the doctor!",
        "Oh god, oh god, oh god...",
        "Get the doors shut!",
    };
    //...and some point the way to the true ending
    //At the warhead in her body without all six papers
    public const string NukeNoCodes = "Codes, switches, warnings... I never learned how to arm it. It's all in those papers.";

    public static readonly string[] NpcHints =
    {
        "The warhead! Someone has to set off the warhead!",
        "Only the head researcher can arm that bomb - and he's gone!",
        "If the nuke went off, it would never get out. None of us would.",
        "The doctor had the arming codes. Only him!",
    };
    public const string ExitAsDaughter = "No. She stays in here. We both do.";

    //Item asset name -> line when picking it up (anything else gets a random GainItem line)
    public static string PickupLine(ItemData item)
    {
        if(item == null) return null;
        switch(item.name)
        {
            case "EmptyBattery": return "I have to get this chunk of junk charged.";
            case "Wire": return "Need this for the generator.";
            case "Toolbox": return "Here goes my equipment, my babies...";
        }
        return GainItem[Random.Range(0, GainItem.Length)];
    }

    //Inspecting a body in a capsule (by body asset name)
    //Short lines, one picked at random each time (never the same twice in a row)
    static readonly string[] InspectMajor =
    {
        "I can access shortcuts with his fingerprints.",
        "Why does he think those squeaky rubber boots are practical?",
    };
    static readonly string[] InspectBlindGuy =
    {
        "I wonder how she can live without seeing.",
        "Her hearing must've been great.",
    };
    static readonly string[] InspectNoLegs =
    {
        "Such a defect.",
        "Why did he have to lose his legs?",
        "I guess his cowardice brought him here.",
    };
    static string lastInspect;

    public static string InspectBody(CustomBodySO body)
    {
        string[] pool = InspectPool(body);
        if(pool == null || pool.Length == 0) return null;
        string line = pool[Random.Range(0, pool.Length)];
        if(pool.Length > 1 && line == lastInspect) line = pool[(System.Array.IndexOf(pool, line) + 1) % pool.Length];
        lastInspect = line;
        return line;
    }

    public static bool CanInspect(CustomBodySO body)
    {
        return InspectPool(body) != null;
    }

    static string[] InspectPool(CustomBodySO body)
    {
        if(body == null) return null;
        switch(body.name)
        {
            case "Major": return InspectMajor;
            case "BlindGuy": return InspectBlindGuy;
            case "NoLegs": return InspectNoLegs;
        }
        return null;
    }

    // ---------- Researcher F ----------

    public static readonly string[] ResearcherNoWeapon =
    {
        "Eek- Doctor, p-please come hide here. The lab subject 1710 broke the cage and came out.",
        "It's dangerous out there doc. Come here and don't make a noise.",
    };
    public static readonly string[] ResearcherWeapon = { "Ahhhh- D don't kill me!!!", "STAY AWAY FROM ME!!" };
    public static readonly string[] ResearcherAboutToDie = { "What did I do wrong doc...", "Promise me it will be painless." };

    // ---------- Endings (pages on the black screen) ----------

    //Run out the exit without all 6 papers
    public static readonly string[] EndingDefyDeath =
    {
        "You throw the door open and run.\n\nThe cold hits first. Then the rifle.",
        "The body you stole drops into the snow, and whatever was left of you goes with it.\n\nNo more bodies. No more knowledge. Nothing.",
        "Behind you, the door swings wide.\n\nSomething huge squeezes out into the night.\n\nWho knows where she goes now.",
    };

    //Took her body instead of leaving, and let it take him (DaughterTransfer)
    public static readonly string[] EndingKarma =
    {
        "Maybe this is what you're owed.\n\nYou couldn't let her out. So you became her.",
        "Your own chemicals burn through every vein, every second.\n\nYour thoughts come apart one at a time, until there's nothing left that knows your name.",
        "The lab seals itself shut. She is locked away from the world forever, and you are locked inside her.\n\nYou did this.",
    };

    //Run out the exit after reading all 6 papers
    public static readonly string[] EndingNeverWorked =
    {
        "You run. You make it to the door.\n\nAnd you wake up.",
        "Straps. Tubes. An experiment table.\n\nThere were never any other bodies. The machine that cheats death never worked at all.",
        "This lab was only ever built for one thing:\n\nto make you suffer, again, and again, with no way out.",
        "You struggle. Years pass on the table.\n\nYou grow old there, until you stop.\n\nFor good.",
    };

    //Arming the warhead while in her body: sealing them both in
    public static readonly string[] EndingTrue =
    {
        "With the last of your mind, you drag her body back to the warhead.\n\nNo one leaves. Not her. Not you.",
        "The facility goes white.",
        "Before the war, a campaign:\n\"Join the experiment. Be frozen. Wake up in 2030, in a better era.\"\n\nThe volunteers came. They either slept like the dead, or became something else.",
        "His wife saw the news. She read his research log: project immortality, putting one soul into another body.\n\nShe left that night.",
        "Their daughter stayed with him.\n\nThe hitting. The needles. Then her arms began to grow.",
        "She forgot how to speak. How to look after herself.\n\nShe became something that only knew how to eat living flesh.",
        "In her last moments, before everything went black, she still pictured a happy family.\n\nFather. Mother.\n\nWhy...",
    };

    public const string Game = "SORRY, DAUGHTER";
}
