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
    private readonly TradeGoodsCatalog _tradeCatalog;
    private readonly RelationshipEventService _socialService;

    public InteractionResolver(World world, TradeGoodsCatalog tradeCatalog, RelationshipEventService socialService)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(tradeCatalog);
        ArgumentNullException.ThrowIfNull(socialService);
        _world = world;
        _tradeCatalog = tradeCatalog;
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
        if (!_world.TryGetEntity(request.TargetId, out var target) || target is null)
        {
            return new(false, "Target not found.");
        }

        return request.ActionId switch
        {
            "capture" => ResolveCapture(actor, target),
            "feed" => ResolveFeed(actor, target, request.ProvidedItem),
            _ => new(false, $"Unknown physical action: {request.ActionId}")
        };
    }

    private InteractionResult ResolveSocial(EntityRecord actor, InteractionRequest request)
    {
        if (request.TargetId == null) return new(false, "Social interactions require a target.");
        if (!_world.TryGetEntity(request.TargetId, out var target) || target is null)
        {
            return new(false, "Target not found.");
        }

        var eventId = request.ActionId switch
        {
            "greet" => "greeted",
            "threaten" => "threatened",
            "help" => "helped",
            _ => null
        };

        if (eventId == null) return new(false, $"Unknown social action: {request.ActionId}");

        var result = _socialService.Apply(actor.Id, target.Id, eventId);
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

        var authority = AuthorityMatrix.GetAuthority(actor, target, _world);
        
        if (!AuthorityMatrix.CanObey(authority, request.ActionId))
        {
            return new(false, $"Target does not obey your commands. (Authority: {authority})");
        }

        return new(true, $"Command '{request.ActionId}' issued successfully to target.");
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

    private InteractionResult ResolveFeed(EntityRecord actor, EntityRecord target, ResourceContainer? itemContainer)
    {
        if (itemContainer == null || itemContainer.Counts.Count == 0)
        {
            return new(false, "No item provided to feed.");
        }

        var item = itemContainer.Counts.First().Key;
        var species = _world.GetSpecies(target.SpeciesId);
        if (!species.Diet.Contains(item))
        {
            return new(false, $"The target cannot eat {item}.");
        }

        target.Satiety = Math.Min(100, target.Satiety + 10);
        var socialResult = _socialService.Apply(target.Id, actor.Id, "helped");
        
        return socialResult == RelationshipEventApplyResult.Applied 
            ? new(true, $"Fed {item} to target. Satiety increased and trust gained.") 
            : new(true, $"Fed {item} to target, but no social bond was formed.");
    }
}
