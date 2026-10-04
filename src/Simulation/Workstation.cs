using System.Numerics;

namespace Simulation;

public sealed class Workstation
{
    public string Id { get; }
    public Vector3 Position { get; }
    public ResourceContainer InputStorage { get; }
    public ResourceContainer OutputStorage { get; }
    public WorkstationRecipe? CurrentRecipe { get; private set; }

    public Workstation(string id, Vector3 position, int inputCapacity, int outputCapacity)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Workstation ID is required.");
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(position.Z)) throw new ArgumentOutOfRangeException(nameof(position));

        Id = id;
        Position = position;
        InputStorage = new ResourceContainer($"{id}-input", inputCapacity);
        OutputStorage = new ResourceContainer($"{id}-output", outputCapacity);
    }

    public bool SetRecipe(WorkstationRecipe? recipe)
    {
        if (CurrentRecipe != null && !CurrentRecipe.IsComplete)
        {
            // In a real system, we might preserve progress or discard.
            // For now, we allow changing recipes but the previous one is lost.
        }
        CurrentRecipe = recipe;
        return true;
    }

    public bool ApplyWork(int amount)
    {
        if (CurrentRecipe == null) return false;
        return CurrentRecipe.ApplyWork(amount);
    }

    public RecipeExecutionResult TryComplete()
    {
        if (CurrentRecipe == null) return RecipeExecutionResult.IncompleteRecipe;
        var result = WorkstationExecutionService.TryExecute(CurrentRecipe, InputStorage, OutputStorage);
        if (result == RecipeExecutionResult.Success)
        {
            CurrentRecipe = null; // Reset after success
        }
        return result;
    }
}
