using System.Numerics;

namespace Simulation;

public enum HouseDoorAction { Toggle, Open, Close, Lock, Unlock }
public sealed record HouseDoorResult(bool Succeeded, string Message);

public sealed partial class World
{
    // Movement orders keep their intent while first routing the
    // resident to its own handle. No work schedule or second movement system is involved.
    internal Vector3 ResolveHomeDoorTarget(EntityRecord actor, Vector3 destination)
    {
        var house = Structures.FirstOrDefault(home => home.ResidentId == actor.Id && home.IsClosed);
        if (house is null || HouseGeometry.Contains(house, actor.Position) == HouseGeometry.Contains(house, destination)
            || SpeciesCatalog?.TryGet(actor.SpeciesId, out var species) != true || species is null || !species.CanOpenDoors)
            return destination;
        if (Vector3.Distance(actor.Position, house.DoorPosition) <= 2
            && OperateDoor(actor.Id, house.Id, HouseDoorAction.Open).Succeeded) return destination;
        return house.ToWorld(new Vector3(0, 0, HouseGeometry.DoorZ
            + (HouseGeometry.Contains(house, actor.Position) ? -1.25f : 1.25f)));
    }

    public HouseDoorResult OperateDoor(string actorId, string houseId, HouseDoorAction action)
    {
        if (!Enum.IsDefined(action)) return new(false, "Unknown door action.");
        if (!TryGetEntity(actorId, out var actor) || actor is null || actor.Health.IsIncapacitated)
            return new(false, "The door user is unavailable.");
        if (SpeciesCatalog?.TryGet(actor.SpeciesId, out var species) != true || species is null || !species.CanOpenDoors)
            return new(false, "This species cannot operate doors.");
        var house = Structures.FirstOrDefault(house => house.Id == houseId && house.Type == StructureType.Hut);
        if (house is null) return new(false, "The house is unavailable.");
        if (Vector3.Distance(actor.Position, house.DoorPosition) > 2)
            return new(false, "Move closer before interacting (2 m reach).");
        var owner = house.ResidentId == actor.Id;
        if (action is HouseDoorAction.Lock or HouseDoorAction.Unlock && !owner)
            return new(false, "Only this home's resident can lock or unlock its door.");
        if (action == HouseDoorAction.Toggle) action = house.IsClosed ? HouseDoorAction.Open : HouseDoorAction.Close;
        if (action == HouseDoorAction.Open && house.IsLocked && !owner && !HouseGeometry.Contains(house, actor.Position))
            return new(false, "The resident has locked this door.");
        switch (action)
        {
            // An interior latch permits egress; a visitor cannot become trapped inside.
            case HouseDoorAction.Open: house.IsClosed = false; break;
            case HouseDoorAction.Close: house.IsClosed = true; break;
            case HouseDoorAction.Lock: house.IsClosed = true; house.IsLocked = true; break;
            case HouseDoorAction.Unlock: house.IsLocked = false; break;
        }
        return new(true, $"Door is now {(house.IsLocked ? "locked" : house.IsClosed ? "closed" : "open")}.");
    }
}
