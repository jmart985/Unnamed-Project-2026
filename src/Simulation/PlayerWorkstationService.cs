using System.Numerics;

namespace Simulation;

public enum WorkstationActionResult
{
    Success,
    PlayerUnavailable,
    PlayerIncapacitated,
    WorkstationUnavailable,
    OutOfRange,
    InvalidItem,
    MissingCarriedItem,
    InputRequirementSatisfied,
    InputCapacityExceeded,
    RecipeAlreadyActive,
    RecipeNotActive,
    MissingInput,
    OutputCapacityExceeded,
    CarriedItemStackFull,
    NothingToCollect
}

public sealed record WorkstationAction(WorkstationActionResult Result, string Message)
{
    public bool Succeeded => Result == WorkstationActionResult.Success;
}

public sealed class PlayerWorkstationService(World world)
{
    public const float InteractionRange = 2f;

    public WorkstationAction StartBasicMeal(string workstationId)
    {
        if (!TryResolve(workstationId, out _, out var workstation, out var failure)) return failure;
        if (workstation!.CurrentRecipe is not null)
            return Fail(WorkstationActionResult.RecipeAlreadyActive, "A recipe is already active.");
        workstation.SetRecipe(SettlementRecipes.CreateBasicMeal());
        return Ok("Basic meal started: load 1 plant and 1 wood, then apply 5 work steps.");
    }

    public WorkstationAction LoadOne(string workstationId, string itemId)
    {
        if (!TryResolve(workstationId, out var player, out var workstation, out var failure)) return failure;
        if (workstation!.CurrentRecipe is null)
            return Fail(WorkstationActionResult.RecipeNotActive, "Start a recipe before loading materials.");
        if (!workstation.CurrentRecipe.Inputs.ContainsKey(itemId))
            return Fail(WorkstationActionResult.InvalidItem, "That item is not used by the active recipe.");
        workstation.InputStorage.TryGetCount(itemId, out var loaded);
        if (loaded >= workstation.CurrentRecipe.Inputs[itemId])
            return Fail(WorkstationActionResult.InputRequirementSatisfied, $"The recipe already has enough {itemId}.");
        if (!player!.Inventory.TryGetValue(itemId, out var carried) || carried < 1)
            return Fail(WorkstationActionResult.MissingCarriedItem, $"You are not carrying {itemId}.");
        if (workstation.InputStorage.RemainingCapacity < 1)
            return Fail(WorkstationActionResult.InputCapacityExceeded, "The workbench input storage is full.");

        player.TryRemoveCarried(itemId, 1);
        workstation.InputStorage.AddKnown(itemId, 1);
        return Ok($"Loaded 1 {itemId}.");
    }

    public WorkstationAction ApplyWork(string workstationId)
    {
        if (!TryResolve(workstationId, out _, out var workstation, out var failure)) return failure;
        var recipe = workstation!.CurrentRecipe;
        if (recipe is null) return Fail(WorkstationActionResult.RecipeNotActive, "Start a recipe first.");
        if (recipe.Inputs.Any(input => !workstation.InputStorage.HasQuantity(input.Key, input.Value)))
            return Fail(WorkstationActionResult.MissingInput, "Load all required materials before working.");
        if (recipe.Outputs.Values.Sum() > workstation.OutputStorage.RemainingCapacity)
            return Fail(WorkstationActionResult.OutputCapacityExceeded, "Collect finished output before making another meal.");

        recipe.ApplyWork(1);
        if (!recipe.IsComplete) return Ok($"Work applied: {recipe.WorkProgress}/{recipe.WorkRequired}.");
        var result = workstation.TryComplete();
        return result == RecipeExecutionResult.Success
            ? Ok("Meal complete: 2 food are ready to collect.")
            : throw new InvalidOperationException($"Validated recipe completion failed: {result}.");
    }

    public WorkstationAction CollectOutput(string workstationId)
    {
        if (!TryResolve(workstationId, out var player, out var workstation, out var failure)) return failure;
        var output = workstation!.OutputStorage.Counts.Where(item => item.Value > 0).ToArray();
        if (output.Length == 0) return Fail(WorkstationActionResult.NothingToCollect, "There is no finished output to collect.");
        var collected = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var item in output)
        {
            var result = ResourceTransferService.TryPickUp(
                workstation.OutputStorage,
                player!,
                item.Key,
                item.Value,
                out var transferred);
            if (result == ResourceMutationResult.Success)
                collected[item.Key] = transferred;
        }
        if (collected.Count == 0)
            return Fail(WorkstationActionResult.CarriedItemStackFull, "The carried stack for each finished item is full.");
        var message = string.Join(", ", collected.Select(item => $"{item.Value} {item.Key}"));
        var remaining = workstation.OutputStorage.Counts.Values.Sum();
        return Ok(remaining > 0
            ? $"Collected {message}. Some finished output remains at the workstation."
            : $"Collected {message}.");
    }

    private bool TryResolve(string workstationId, out EntityRecord? player, out Workstation? workstation, out WorkstationAction failure)
    {
        player = null;
        workstation = null;
        if (world.PlayerEntityId is null || !world.TryGetEntity(world.PlayerEntityId, out player) || player is null)
        {
            failure = Fail(WorkstationActionResult.PlayerUnavailable, "The player is unavailable.");
            return false;
        }
        if (player.Health.IsIncapacitated)
        {
            failure = Fail(WorkstationActionResult.PlayerIncapacitated, "You cannot use the workbench while incapacitated.");
            return false;
        }
        workstation = world.Workstations.FirstOrDefault(item => item.Id == workstationId);
        if (workstation is null)
        {
            failure = Fail(WorkstationActionResult.WorkstationUnavailable, "The workbench is unavailable.");
            return false;
        }
        if (Vector3.Distance(player.Position, workstation.Position) > InteractionRange)
        {
            failure = Fail(WorkstationActionResult.OutOfRange, "Move within 2 m of the workbench.");
            return false;
        }
        failure = Ok("");
        return true;
    }

    private static WorkstationAction Ok(string message) => new(WorkstationActionResult.Success, message);
    private static WorkstationAction Fail(WorkstationActionResult result, string message) => new(result, message);
}
