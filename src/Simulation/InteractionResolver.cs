using System.Collections.Generic;
using System.Linq;

namespace Simulation;

/// <summary>
/// The central hub for validating and executing interactions between entities.
/// Decouples intent (InteractionRequest) from domain mutation (World/Service calls).
/// </summary>
public sealed class InteractionResolver
{
    private readonly World _world;
    private readonly RelationshipEventService _socialService;

    public InteractionResolver(World world, TradeGoodsCatalog tradeCatalog, RelationshipEventService socialService)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(tradeCatalog);
        ArgumentNullException.ThrowIfNull(socialService);
        if (!socialService.BelongsTo(world)) throw new ArgumentException("Relationship events must use the same World.", nameof(socialService));
        _world = world;
        _socialService = socialService;
    }

    /// <summary>
    /// Resolves an interaction request by validating prerequisites and executing the corresponding domain logic.
    /// </summary>
    public InteractionResult Resolve(InteractionRequest request)
    {
        if (request is null) return new(false, "Null request.");
        if (string.IsNullOrWhiteSpace(request.ActorId)) return new(false, "No actor specified.");

        if (!_world.TryGetEntity(request.ActorId, out var actor) || actor is null)
        {
            return new(false, "Actor not found in world.");
        }

        if (actor.Health.IsIncapacitated) return new(false, "Actor is incapacitated.");
        if (string.IsNullOrWhiteSpace(request.ActionId)) return new(false, "No action specified.");

        return request.Type switch
        {
            InteractionType.Physical => ResolvePhysical(actor, request),
            InteractionType.Social => ResolveSocial(actor, request),
            InteractionType.Command => ResolveCommand(actor, request),
            _ => new(false, $"Unsupported interaction type: {request.Type}")
        };
    }

    private InteractionResult ResolvePhysical(EntityRecord actor, InteractionRequest request)
    {
        if (request.TargetId == null) return new(false, "Physical interactions require a target.");
        
        if (_world.TryGetEntity(request.TargetId, out var target) && target is not null)
        {
            var rejection = ValidateTarget(actor, target);
            if (rejection is not null) return rejection;
            return request.ActionId switch
            {
                "capture" => ResolveCapture(actor, target),
                "feed" => ResolveFeed(actor, target, request.ProvidedItem),
                "pet" => ResolvePet(actor, target),
                "give" => ResolveGive(actor, target, request.ProvidedItem),
                _ => new(false, $"Unknown physical action: {request.ActionId}")
            };
        }

        var structure = _world.Structures.FirstOrDefault(s => s.Id == request.TargetId);
        if (structure != null)
        {
            if (MeleeReachValidator.Validate(actor.Position, structure.Position, CreatureGreetingService.DefaultMaximumReach) != MeleeReachResult.InRange)
                return new(false, "Move closer before interacting (2 m reach).");
            return request.ActionId switch
            {
                "toggle_door" => ResolveToggleDoor(actor, structure),
                _ => new(false, $"Unknown physical action: {request.ActionId}")
            };
        }

        return new(false, "Target not found.");
    }

    private InteractionResult ResolveSocial(EntityRecord actor, InteractionRequest request)
    {
        if (request.TargetId == null) return new(false, "Social interactions require a target.");
        if (!_world.TryGetEntity(request.TargetId, out var target) || target is null)
        {
            return new(false, "Target not found.");
        }

        var rejection = ValidateTarget(actor, target);
        if (rejection is not null) return rejection;

        var eventId = request.ActionId switch
        {
            "greet" => "greeted",
            "threaten" => "threatened",
            "help" => "helped",
            _ => null
        };

        if (eventId == null) return new(false, $"Unknown social action: {request.ActionId}");

        var result = _socialService.Apply(target.Id, actor.Id, eventId);
        return result == RelationshipEventApplyResult.Applied 
            ? new(true, $"Successfully performed {request.ActionId}.") 
            : new(false, $"Social interaction failed: {result}");
    }

    private InteractionResult ResolveCommand(EntityRecord actor, InteractionRequest request)
    {
        if (request.TargetId == null) return new(false, "Command interactions require a target.");
        if (!_world.TryGetEntity(request.TargetId, out var target) || target is null)
        {
            return new(false, "Target not found.");
        }

        if (actor.Health.IsDead || actor.Health.IsIncapacitated)
            return new(false, "Only a conscious actor can issue these orders.");
        if (actor.Id == target.Id || target.Id == _world.PlayerEntityId || target.Health.IsDead || target.Health.IsIncapacitated)
            return new(false, "Target cannot receive this command.");

        var command = request.ActionId.ToLowerInvariant();
        if (command is not ("follow" or "stay"))
            return new(false, $"Unknown command: {request.ActionId}. Choose follow or stay.");

        if (_world.SpeciesCatalog is not { } catalog
            || !catalog.TryGet(target.SpeciesId, out var species)
            || species is null
            || species.MovementSpeed <= 0)
            return new(false, "Target species cannot receive movement orders.");

        var authority = AuthorityMatrix.GetAuthority(actor, target, _world);

        if (!AuthorityMatrix.CanObey(authority, command))
        {
            return new(false, $"Target does not obey your commands. (Authority: {authority})");
        }

        target.OrderIntent = command == "follow"
            ? new EntityOrder(EntityOrderKind.Follow, actor.Id)
            : new EntityOrder(EntityOrderKind.Stay, actor.Id, target.Position);
        return new(true, command == "follow"
            ? $"Follow order issued to {target.Id}."
            : $"Stay order issued to {target.Id} at its current position.");
    }

    private InteractionResult ResolveCapture(EntityRecord actor, EntityRecord target)
    {
        var result = _world.TryCaptureCreature(target.Id);
        return result switch
        {
            CreatureCaptureResult.Captured => new(true, "Target captured."),
            CreatureCaptureResult.AlreadyCaptured => new(false, "Target is already captured."),
            CreatureCaptureResult.AlreadyCaptive => new(false, "Target is already captive."),
            CreatureCaptureResult.TargetNotFound => new(false, "Target not found."),
            CreatureCaptureResult.NotCreature => new(false, "Target is not a capturable creature."),
            CreatureCaptureResult.TargetDead => new(false, "Target is dead."),
            _ => new(false, "Capture failed due to an invalid transition.")
        };
    }

    private InteractionResult ResolvePet(EntityRecord actor, EntityRecord target)
    {
        if (string.Equals(target.SpeciesId, "human", StringComparison.Ordinal))
        {
            return new(false, "Cannot pet a human.");
        }

        if (_world.SpeciesCatalog is not { } catalog || !catalog.TryGet(target.SpeciesId, out _))
            return new(false, "Target species definition is unavailable.");
        if (_world.GetBondLevel(target.Id, actor.Id) is not (RelationshipBondLevel.Trusted or RelationshipBondLevel.Loyal))
            return new(false, "Pet unavailable until this creature is Trusted.");

        var result = _socialService.Apply(target.Id, actor.Id, "helped");
        return result == RelationshipEventApplyResult.Applied
            ? new(true, "You petted the creature. It seems to like you.")
            : new(false, $"Pet interaction failed: {result}.");
    }

    private InteractionResult ResolveGive(EntityRecord actor, EntityRecord target, ResourceContainer? itemContainer)
    {
        if (!TryGetProvidedItem(itemContainer, out var item, out var amount))
            return new(false, "Provide exactly one item type with a positive quantity to give.");
        if (!_socialService.HasEvent("helped")) return new(false, "The helped relationship profile is unavailable.");
        var transfer = ResourceTransferService.TryTransfer(actor, target, item, amount);
        if (transfer != ResourceMutationResult.Success) return new(false, $"Give failed: {transfer}.");
        _socialService.Apply(target.Id, actor.Id, "helped");
        return new(true, $"Gave {amount}x {item} to target.");
    }

    private InteractionResult ResolveToggleDoor(EntityRecord actor, Structure structure)
    {
        structure.IsClosed = !structure.IsClosed;
        return new(true, $"Door is now {(structure.IsClosed ? "closed" : "open")}.");
    }

    private InteractionResult ResolveFeed(EntityRecord actor, EntityRecord target, ResourceContainer? itemContainer)
    {
        if (!TryGetProvidedItem(itemContainer, out var item, out _))
            return new(false, "Provide exactly one item type with a positive quantity to feed.");
        if (_world.SpeciesCatalog is not { } catalog || !catalog.TryGet(target.SpeciesId, out var species) || species is null)
            return new(false, "Target species definition is unavailable.");
        if (!species.Diet.Contains(item))
        {
            return new(false, $"The target cannot eat {item}.");
        }

        if (target.Satiety >= 100) return new(false, "Target is already full.");
        if (!_socialService.HasEvent("helped")) return new(false, "The helped relationship profile is unavailable.");
        if (actor.TryRemoveCarried(item, 1) == ResourceMutationResult.Success)
        {
            target.Needs.AddNutrition(target.Needs.NutritionPerUnit);
            var socialResult = _socialService.Apply(target.Id, actor.Id, "helped");

            return socialResult == RelationshipEventApplyResult.Applied
                ? new(true, $"Fed {item} to target. Satiety increased.")
                : new(true, $"Fed {item} to target, but no social bond was formed.");
        }

        return new(false, "Actor does not have the item.");
    }

    private static InteractionResult? ValidateTarget(EntityRecord actor, EntityRecord target)
    {
        if (actor.Id == target.Id) return new(false, "Cannot interact with yourself.");
        if (target.Health.IsIncapacitated) return new(false, "Target is incapacitated.");
        return MeleeReachValidator.Validate(actor.Position, target.Position, CreatureGreetingService.DefaultMaximumReach) == MeleeReachResult.InRange
            ? null : new(false, "Move closer before interacting (2 m reach).");
    }

    private static bool TryGetProvidedItem(ResourceContainer? container, out string item, out int amount)
    {
        item = ""; amount = 0;
        if (container is null || container.Counts.Count != 1) return false;
        var entry = container.Counts.Single();
        item = entry.Key; amount = entry.Value;
        return ItemIds.IsKnown(item) && amount > 0;
    }
}
