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
    public int Selected { get; private set; }
    public ItemStack SelectedStack => items.Count > 0 ? items[Mathf.Clamp(Selected, 0, items.Count - 1)] : null;
    public ItemData SelectedItem => SelectedStack != null ? SelectedStack.item : null;
    public bool IsFull => slotLimit > 0 && items.Count >= slotLimit;
    public Vector2 Facing { get; private set; } = Vector2.down;

    private readonly List<ItemStack> items = new List<ItemStack>();

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
        if(body == null || Time.timeScale == 0f || ModalUI.IsOpen) return;
        Vector2 v = body.rb.linearVelocity;
        if(v.sqrMagnitude > 0.01f) Facing = v.normalized;
        if(body.isPlayingDead || body.isHiding) return;

        //Arrow keys belong to the flask table while it's in use
        if(items.Count > 1 && ChemicalChargingStation.Active == null)
        {
            int step = (Input.GetKeyDown(nextKey) ? 1 : 0) - (Input.GetKeyDown(previousKey) ? 1 : 0);
            if(step != 0)
            {
                Selected = (Selected + step + items.Count) % items.Count;
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

    //Into the inventory, or at your feet if it's full
    public void Give(ItemData item, Vector2 dropAt, int charges = -1)
    {
        if(!Add(item, charges)) WorldItem.Spawn(item, charges, dropAt);
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
                RemoveAt(Selected);
                return;
            case ItemKind.Weapon:
                if(UseWeapon(body, stack)) SpendCharge(stack);
                return;
        }
        if(item.IsTrap)
        {
            //On a left-behind body if one's close (booby trap), otherwise on the floor at your feet
            BodyScript bait = NearestBait(body);
            Vector2 pos = bait != null ? (Vector2)bait.transform.position : (Vector2)body.transform.position;
            ArmedTrap.Place(item, pos, bait != null ? bait.transform : null);
            RemoveAt(Selected);
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
        if(WeaponTarget(body, weapon, out Component target) == null) return false;
        if(weapon.useNoise > 0f) Noise.Make(body.transform.position, weapon.useNoise);
        if(target is Enemy enemy) enemy.Stun(weapon.weaponStun);
        else if(target is SmallMonster small) small.Kill();
        else if(target is BodyScript victim)
        {
            if(!victim.isConsumed) victim.Consume();
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

    void RemoveAt(int index)
    {
        if(index < 0 || index >= items.Count) return;
        items.RemoveAt(index);
        if(Selected >= items.Count) Selected = Mathf.Max(0, items.Count - 1);
        OnChanged?.Invoke();
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
