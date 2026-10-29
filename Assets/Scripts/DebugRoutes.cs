using System;
using UnityEngine;
using UnityEngine.AI;

//Shortcuts for testing the ending routes. Editor and Development builds only (off in the real build).
//F1 fix generator, F2 read all notes, F3 to the exit, F4 embrace (become her), F5 to the nuke,
//F6-F9 jump straight to an ending screen, F10 shows/hides this list
public class DebugRoutes : MonoBehaviour
{
    public bool showHelp = false;

    private bool active;
    private GUIStyle style;

    void Awake()
    {
        active = Application.isEditor || Debug.isDebugBuild;
    }

    void Update()
    {
        if(!active) return;
        if(Input.GetKeyDown(KeyCode.F10)) showHelp = !showHelp;
        BodyScript body = Player();

        if(Input.GetKeyDown(KeyCode.F1))
        {
            Generator generator = FindAnyObjectByType<Generator>();
            if(generator != null) generator.DebugFix();
            Log("generator fixed");
        }
        if(Input.GetKeyDown(KeyCode.F2))
        {
            foreach(LoreNote note in Enum.GetValues(typeof(LoreNote)))
            {
                if(note != LoreNote.None) NoteObject.ReadNotes.Add(note);
            }
            Log("all notes read");
        }
        if(Input.GetKeyDown(KeyCode.F3)) TeleportTo(body, FindAnyObjectByType<ExitDoor>());
        if(Input.GetKeyDown(KeyCode.F4))
        {
            DaughterTransfer transfer = GameManager.Get<DaughterTransfer>();
            if(transfer != null && body != null) transfer.Embrace(body);
            Log("embraced her");
        }
        if(Input.GetKeyDown(KeyCode.F5)) TeleportTo(body, FindAnyObjectByType<NukeTerminal>());

        GameOverScreen end = GameManager.Get<GameOverScreen>();
        if(end == null) return;
        if(Input.GetKeyDown(KeyCode.F6)) end.ShowEnding("DEFIED DEATH", Lore.EndingDefyDeath);
        if(Input.GetKeyDown(KeyCode.F7)) end.ShowEnding("IT NEVER WORKED", Lore.EndingNeverWorked);
        if(Input.GetKeyDown(KeyCode.F8)) end.ShowEnding("KARMA", Lore.EndingKarma);
        if(Input.GetKeyDown(KeyCode.F9)) end.ShowEnding("TRUE ENDING", Lore.EndingTrue);
    }

    static BodyScript Player()
    {
        BodyManager bodies = GameManager.Get<BodyManager>();
        return bodies != null ? bodies.currentBody : null;
    }

    //Onto the floor next to it
    void TeleportTo(BodyScript body, Component target)
    {
        if(body == null || target == null)
        {
            Log("nothing to teleport to");
            return;
        }
        Vector3 point = target.transform.position + Vector3.down * 0.8f;
        if(NavMesh.SamplePosition(point, out NavMeshHit hit, 3f, NavMesh.AllAreas)) point = hit.position;
        point.z = body.transform.position.z;
        body.rb.position = point;
        body.transform.position = point;
        Log("teleported to " + target.name);
    }

    void Log(string what)
    {
        Debug.Log("[Debug route] " + what, this);
    }

    void OnGUI()
    {
        if(!active || !showHelp) return;
        if(style == null)
        {
            style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 14 };
            style.normal.textColor = Color.white;
        }
        string notes = $"{NoteObject.ReadNotes.Count}/{NoteObject.StoryNoteCount}";
        GUI.Box(new Rect(10, 120, 300, 230),
            "DEBUG ROUTES (F10 hide)\n" +
            $"F1  Fix generator   [{(Generator.IsFixed ? "on" : "off")}]\n" +
            $"F2  Read all notes  [{notes}]\n" +
            "F3  Teleport to exit\n" +
            $"F4  Embrace (her body) [{(DaughterTransfer.Active ? "yes" : "no")}]\n" +
            "F5  Teleport to nuke\n" +
            "F6  Ending: Defied Death\n" +
            "F7  Ending: It Never Worked\n" +
            "F8  Ending: Karma\n" +
            "F9  Ending: True", style);
    }
}
