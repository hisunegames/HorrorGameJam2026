using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[Serializable]
public class ItemStack
{
    public ItemData item;
    //Uses left (weapons)
    public int charges;

    public ItemStack(ItemData item, int charges)
    {
        this.item = item;
        this.charges = charges;
    }
}

//Stand-in inventory until the real inventory system lands: as many items as you like, one slot each.
//[Left]/[Right] pick one, [Q] uses it (pick up is [F] on a WorldItem).
//Everything carried drops around the old body when you swap, spread out where it can be picked up again.
//Put it on the GameManager
public class PlayerInventory : MonoBehaviour
{
    public BodyManager bodyManager;
    //0 = no limit
    public int slotLimit = 0;
    public KeyCode useKey = KeyCode.Q;
    public KeyCode previousKey = KeyCode.LeftArrow;
    public KeyCode nextKey = KeyCode.RightArrow;
    //A trap set this close to a left-behind body is attached to it; blades reach bodies this close
    public float attachToBodyRange = 1f;
    //Melee hits things within this angle either side of where you're facing
    public float meleeAngle = 70f;

    [Header("Dropping on swap")]
    public Vector2 dropRadius = new Vector2(0.5f, 1.1f);
    //Layers that count as solid when spreading drops (walls, props). Bodies and the player are ignored
    public LayerMask dropBlockers = ~0;

    public event Action OnChanged;

    public IReadOnlyList<ItemStack> Items => items;
    //Wheel slot: 0 = empty hand, 1..N = Items[0..N-1]
    public int Selected { get; private set; }
    public int SlotCount => items.Count + 1;
    public ItemStack SelectedStack => SlotAt(Selected);
    //null = the empty hand slot
    public ItemStack SlotAt(int slot) => slot > 0 && slot <= items.Count ? items[slot - 1] : null;
    public ItemData SelectedItem => SelectedStack != null ? SelectedStack.item : null;
    public bool IsFull => slotLimit > 0 && items.Count >= slotLimit;
    public Vector2 Facing { get; private set; } = Vector2.down;

    private readonly List<ItemStack> items = new List<ItemStack>();
    private SpriteRenderer held;

    [Header("Held item")]
    //Where the selected item shows on the player, from their feet (x flips with facing)
    public Vector2 handOffset = new Vector2(0.22f, 0.3f);

    //The selected weapon in the player's right hand (other items aren't shown). Four directions like the body:
    //facing down it's on screen-left in front of the body, facing up on screen-right behind it, facing right out in
    //front behind the body, facing left mirrored and behind (the right hand is the far one)
    //End of the barrel of the gun in your hand, and the way it points (the four held directions). Falls back to the
    //middle of the body if the weapon isn't showing
    public Vector2 Muzzle(BodyScript body, out Vector2 direction)
    {
        Vector2 f = Facing.sqrMagnitude > 0.001f ? Facing : Vector2.down;
        direction = Mathf.Abs(f.x) >= Mathf.Abs(f.y) ? new Vector2(Mathf.Sign(f.x), 0f) : new Vector2(0f, Mathf.Sign(f.y));
        if(held != null && held.enabled && held.sprite != null)
        {
            //The art points right from its pivot; mirrored when facing left. TransformPoint adds the turn up / down
            Bounds b = held.sprite.bounds;
            float tip = held.flipX ? -b.max.x : b.max.x;
            return held.transform.TransformPoint(new Vector3(tip, b.center.y, 0f));
        }
        Collider2D col = body.GetComponent<Collider2D>();
        return col != null ? (Vector2)col.bounds.center : (Vector2)body.transform.position;
    }

    void UpdateHeld(BodyScript body)
    {
        if(held == null)
        {
            held = new GameObject("HeldItem").AddComponent<SpriteRenderer>();
        }
        if(held.transform.parent != body.transform) held.transform.SetParent(body.transform, false);
        ItemData item = SelectedItem;
        bool show = item != null && item.kind == ItemKind.Weapon && item.worldSprite != null && !body.isHiding && !body.isPlayingDead;
        held.enabled = show;
        if(!show) return;
        held.sprite = item.worldSprite;

        Vector2 f = Facing.sqrMagnitude > 0.001f ? Facing : Vector2.down;
        bool horizontal = Mathf.Abs(f.x) >= Mathf.Abs(f.y);
        bool inFront = false;
        float angle;
        Vector2 pos;
        //Item art points right
        if(horizontal && f.x > 0f)
        {
            held.flipX = false;
            angle = 0f;
            pos = new Vector2(handOffset.x, handOffset.y);
        }
        else if(horizontal)
        {
            held.flipX = true;
            angle = 0f;
            pos = new Vector2(-handOffset.x, handOffset.y);
        }
        else if(f.y < 0f)
        {
            //Facing the camera: their right hand is on the screen's left
            held.flipX = false;
            angle = -90f;
            pos = new Vector2(-handOffset.x, handOffset.y - 0.1f);
            inFront = true;
        }
        else
        {
            held.flipX = false;
            angle = 90f;
            pos = new Vector2(handOffset.x, handOffset.y + 0.05f);
        }
        held.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        held.transform.localPosition = pos;
        SpriteRenderer bodySprite = body.GetComponent<SpriteRenderer>();
        if(bodySprite != null)
        {
            held.sortingLayerID = bodySprite.sortingLayerID;
            held.sortingOrder = bodySprite.sortingOrder + (inFront ? 1 : -1);
        }
    }

    void Awake()
    {
        if(bodyManager == null) bodyManager = GameManager.Get<BodyManager>();
    }

    void OnEnable()
    {
        if(bodyManager != null) bodyManager.OnBodySwapped += HandleSwap;
    }

    void OnDisable()
    {
        if(bodyManager != null) bodyManager.OnBodySwapped -= HandleSwap;
    }

    void Update()
    {
        BodyScript body = bodyManager != null ? bodyManager.currentBody : null;
        //In her body there are no hands for items
        if(body == null || Time.timeScale == 0f || ModalUI.IsOpen || DaughterTransfer.Active) return;
        Vector2 v = body.rb.linearVelocity;
        if(v.sqrMagnitude > 0.01f) Facing = v.normalized;
        UpdateHeld(body);
        if(body.isPlayingDead || body.isHiding) return;

        //Arrow keys belong to the flask table while it's in use
        if(items.Count > 0 && ChemicalChargingStation.Active == null)
        {
            int step = (Input.GetKeyDown(nextKey) ? 1 : 0) - (Input.GetKeyDown(previousKey) ? 1 : 0);
            if(step != 0)
            {
                Selected = (Selected + step + SlotCount) % SlotCount;
                OnChanged?.Invoke();
            }
        }
        if(Input.GetKeyDown(useKey)) UseSelected(body);
    }

    public bool Has(ItemData item)
    {
        return item != null && items.Exists(s => s.item == item);
    }

    public bool Add(ItemData item, int charges = -1)
    {
        if(item == null || IsFull) return false;
        items.Add(new ItemStack(item, charges > 0 ? charges : item.StartCharges));
        OnChanged?.Invoke();
        SFXManager.Play("item");
        return true;
    }

    //Takes one of this item out (used up by an objective)
    public bool Remove(ItemData item)
    {
        int i = items.FindIndex(s => s.item == item);
        if(i < 0) return false;
        RemoveAt(i);
        return true;
    }

    //Objective rewards (battery, parts, wire, finger): dropped on the floor next to the player so they have to pick
    //them up, on a spot they can actually reach (falls back to near dropAt if there's no player)
    public void Give(ItemData item, Vector2 dropAt, int charges = -1)
    {
        if(item == null) return;
        BodyScript body = bodyManager != null ? bodyManager.currentBody : null;
        Vector2 origin = body != null ? (Vector2)body.transform.position : dropAt;
        float angle = UnityEngine.Random.Range(0f, 360f);
        WorldItem.Spawn(item, charges, FindDropSpot(origin, ref angle));
    }

    //Left-behind body a trap would be attached to
    public BodyScript NearestBait(BodyScript body)
    {
        return NearestBody(body, b => b.IsBait);
    }

    public BodyScript NearestBody(BodyScript body, Predicate<BodyScript> match)
    {
        BodyScript best = null;
        float bestDist = attachToBodyRange;
        foreach(var other in FindObjectsByType<BodyScript>())
        {
            if(other == body || !match(other)) continue;
            float d = Vector2.Distance(other.transform.position, body.transform.position);
            if(d <= bestDist)
            {
                bestDist = d;
                best = other;
            }
        }
        return best;
    }

    void UseSelected(BodyScript body)
    {
        ItemStack stack = SelectedStack;
        if(stack == null) return;
        ItemData item = stack.item;
        switch(item.kind)
        {
            case ItemKind.Adrenaline:
                PlayerSprint sprint = GameManager.Get<PlayerSprint>();
                if(sprint == null) return;
                sprint.ApplyAdrenaline(item);
                RemoveSelected();
                return;
            case ItemKind.Weapon:
                if(UseWeapon(body, stack)) SpendCharge(stack);
                if (item.ranged == true) SFXManager.Play("shoot");
                else SFXManager.Play("stab");
                return;
            case ItemKind.Lure:
                RadioLure.Place(item, body.transform.position);
                RemoveSelected();
                return;
            case ItemKind.Throwable:
                Flashbang.Throw(item, body.transform.position, Facing);
                RemoveSelected();
                return;
        }
        if(item.IsTrap)
        {
            //On a left-behind body if one's close (booby trap), otherwise on the floor at your feet
            BodyScript bait = NearestBait(body);
            Vector2 pos = bait != null ? (Vector2)bait.transform.position : (Vector2)body.transform.position;
            ArmedTrap.Place(item, pos, bait != null ? bait.transform : null);
            RemoveSelected();
        }
    }

    //What a weapon would hit right now (for the prompt too). Monsters first, then Researcher F
    public string WeaponTarget(BodyScript body, ItemData weapon, out Component target)
    {
        target = null;
        Vector2 from = body.transform.position;
        float best = float.MaxValue;
        foreach(var enemy in FindObjectsByType<Enemy>())
        {
            if(enemy.IsStunned || !InReach(from, enemy.transform.position, weapon, ref best)) continue;
            target = enemy;
        }
        foreach(var small in SmallMonster.All)
        {
            if(small == null || small.IsDead || !InReach(from, small.transform.position, weapon, ref best)) continue;
            target = small;
        }
        if(target is Enemy) return "Stun the monster";
        if(target is SmallMonster) return "Kill it";
        if(!weapon.canCut) return null;

        //Researcher F: kill her, or cut the finger off her corpse
        BodyScript alive = NearestBody(body, b => !b.isCurrentBody && !b.isVacant && b.HasFingerprint);
        if(alive != null)
        {
            target = alive;
            return "Kill her";
        }
        BodyScript corpse = NearestBody(body, b => b.isConsumed && b.HasFingerprint && !b.fingerTaken && b.customBodySO.fingerItem != null);
        if(corpse != null)
        {
            target = corpse;
            return "Cut off finger";
        }
        return null;
    }

    bool InReach(Vector2 from, Vector2 to, ItemData weapon, ref float best)
    {
        Vector2 delta = to - from;
        float d = delta.magnitude;
        if(d > weapon.range + 0.25f || d >= best) return false;
        if(d > 0.3f && Vector2.Angle(Facing, delta) > (weapon.ranged ? 15f : meleeAngle)) return false;
        best = d;
        return true;
    }

    bool UseWeapon(BodyScript body, ItemStack stack)
    {
        ItemData weapon = stack.item;
        //Guns always fire (and can miss): a projectile from the middle of the body the way you're facing
        if(weapon.ranged)
        {
            if(weapon.useNoise > 0f) Noise.Make(body.transform.position, weapon.useNoise);
            Vector2 from = Muzzle(body, out Vector2 dir);
            Projectile.Fire(weapon, from, dir);
            return true;
        }
        if(WeaponTarget(body, weapon, out Component target) == null) return false;
        if(weapon.useNoise > 0f) Noise.Make(body.transform.position, weapon.useNoise);
        if(target is Enemy enemy) enemy.Stun(weapon.weaponStun);
        else if(target is SmallMonster small) small.Kill();
        else if(target is BodyScript victim)
        {
            if(!victim.isConsumed)
            {
                victim.Consume();
                SpeechBubble.Say(body.transform, Lore.ResearcherKilled, 2.5f);
            }
            else
            {
                victim.fingerTaken = true;
                Give(victim.customBodySO.fingerItem, victim.transform.position);
            }
        }
        return true;
    }

    void SpendCharge(ItemStack stack)
    {
        stack.charges--;
        if(stack.charges <= 0) RemoveAt(items.IndexOf(stack));
        else OnChanged?.Invoke();
    }

    //Index into Items (not a wheel slot)
    void RemoveAt(int index)
    {
        if(index < 0 || index >= items.Count) return;
        items.RemoveAt(index);
        if(Selected > items.Count) Selected = items.Count;
        OnChanged?.Invoke();
    }

    //The item the wheel is on, used up
    void RemoveSelected()
    {
        if(Selected > 0) RemoveAt(Selected - 1);
    }

    void HandleSwap(BodyScript oldBody, BodyScript newBody, int swapCount)
    {
        if(oldBody == null || items.Count == 0) return;
        DropAll(oldBody.transform.position);
    }

    //Spread everything around a point, only where the player can walk to and pick it up again
    public void DropAll(Vector2 origin)
    {
        float angle = UnityEngine.Random.Range(0f, 360f);
        foreach(var stack in items)
        {
            WorldItem.Spawn(stack.item, stack.charges, FindDropSpot(origin, ref angle));
        }
        items.Clear();
        Selected = 0;
        OnChanged?.Invoke();
    }

    Vector2 FindDropSpot(Vector2 origin, ref float angle)
    {
        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = false;
        int ignore = LayerMask.GetMask("Player", "Body", "Ignore Raycast");
        filter.SetLayerMask(dropBlockers & ~ignore);
        RaycastHit2D[] hits = new RaycastHit2D[1];
        Collider2D[] overlaps = new Collider2D[1];

        for(int i = 0; i < 12; i++)
        {
            //Walk round the circle so drops spread out instead of piling up
            angle += 360f / 5f + UnityEngine.Random.Range(-20f, 20f);
            Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            Vector2 spot = origin + dir * UnityEngine.Random.Range(dropRadius.x, dropRadius.y);

            //Not through a wall, not inside anything solid, and on the walkable floor
            if(Physics2D.Linecast(origin, spot, filter, hits) > 0) continue;
            if(Physics2D.OverlapCircle(spot, 0.2f, filter, overlaps) > 0) continue;
            if(!NavMesh.SamplePosition(spot, out NavMeshHit hit, 0.4f, NavMesh.AllAreas)) continue;
            return spot;
        }
        return origin;
    }
}
