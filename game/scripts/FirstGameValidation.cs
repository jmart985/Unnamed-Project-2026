using Godot;
using Simulation;

namespace UnnamedProject2026;

public partial class Main
{
    private async Task ValidatePlayerLoop()
    {
        Require(_world.Entities.Count == 14 && FoodTotal() == 400 && WoodTotal() == 100, "new game population/resources");
        var player = _world.Entities.Single(e => e.Id == _world.PlayerEntityId);
        var initialPosition = player.Position;
        var initialSatiety = player.Satiety;
        _playerCharacter.SetPhysicsProcess(true);
        Input.ActionPress(ControllerActions.MoveRight);
        try { for (var frame = 0; frame < 12; frame++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }
        finally { Input.ActionRelease(ControllerActions.MoveRight); _playerCharacter.SetPhysicsProcess(false); }
        SyncPlayerPosition();
        Require(player.Position != initialPosition, "controller moves authoritative player");
        _clock.SetPaused(false);
        _clock.Advance(1, AdvanceSimulationTick);
        _clock.SetPaused(true);
        Require(player.Satiety < initialSatiety && _clock.SimulationTime > 0, "player hunger and clock advance");
        var source = _world.FoodSources.Single(s => s.Id == "food-pile-1");
        _playerCharacter.GlobalPosition = new Vector3(source.Position.X, source.Position.Y, source.Position.Z);
        SyncPlayerPosition();
        var before = player.Satiety;
        Require(TryPlayerEat().StartsWith("Ate one unit", StringComparison.Ordinal), "player eating handler succeeds");
        Require(player.Satiety == Math.Min(100, before + player.Needs.NutritionPerUnit), "nutrition once");
        Require(FoodTotal() == 399 && _foodConsumption!.FoodUnitsConsumed == 1, "single consumption");
        _playerCharacter.GlobalPosition = new Vector3(190, 0, 190);
        SyncPlayerPosition();
        var unchanged = WorldSnapshotSerializer.Serialize(_world, _clock.SimulationTime);
        Require(TryPlayerEat().StartsWith("Move closer", StringComparison.Ordinal), "out-of-range eating rejection");
        Require(unchanged == WorldSnapshotSerializer.Serialize(_world, _clock.SimulationTime), "rejected eating preserves World");
        _world.AddFoodSource("empty-validation", player.Position, new ResourceContainer("empty-validation", 1));
        unchanged = WorldSnapshotSerializer.Serialize(_world, _clock.SimulationTime);
        Require(TryPlayerEat().Contains("empty", StringComparison.Ordinal), "empty food rejection");
        Require(unchanged == WorldSnapshotSerializer.Serialize(_world, _clock.SimulationTime), "empty food preserves World");
        var cow = _world.Entities.Single(e => e.SpeciesId == "cow");
        _playerCharacter.GlobalPosition = new Vector3(cow.Position.X + 1, 0, cow.Position.Z);
        SyncPlayerPosition();
        SelectEntity(cow.Id);
        Require(GreetSelectedEntity().StartsWith("Greeted", StringComparison.Ordinal), "greeting handler succeeds");
        var relation = _world.Relationships.Single();
        Require(relation.ObserverEntityId == player.Id && relation.TargetEntityId == cow.Id && relation.Familiarity == 5
            && relation.Trust == 0 && relation.Fear == 0 && relation.Loyalty == 0, "directed greeting only");
        unchanged = WorldSnapshotSerializer.Serialize(_world, _clock.SimulationTime);
        SelectEntity(_world.Entities.Single(e => e.SpeciesId == "wolf").Id);
        Require(GreetSelectedEntity().Contains("does not accept", StringComparison.Ordinal), "unsupported capability rejection");
        SelectEntity(_world.Entities.Single(e => e.SpeciesId == "ape").Id);
        Require(GreetSelectedEntity().StartsWith("Move closer", StringComparison.Ordinal), "out-of-range greeting rejection");
        _selectedEntityId = "missing";
        Require(GreetSelectedEntity().Contains("no longer exists", StringComparison.Ordinal), "missing greeting rejection");
        Require(unchanged == WorldSnapshotSerializer.Serialize(_world, _clock.SimulationTime), "greeting rejections preserve World");
    }

    private long FoodTotal() => _world.FoodSources.Sum(s => s.Contents.Counts.Where(i => ItemIds.IsEdibleFood(i.Key)).Sum(i => (long)i.Value))
        + _world.Entities.Sum(e => e.Inventory.Where(i => ItemIds.IsEdibleFood(i.Key)).Sum(i => (long)i.Value))
        + _world.Stockpiles.Sum(s => s.Contents.Counts.Where(i => ItemIds.IsEdibleFood(i.Key)).Sum(i => (long)i.Value));
    private long WoodTotal() => _world.ResourcePiles.Sum(s => (long)s.Contents.Counts.GetValueOrDefault(ItemIds.Wood))
        + _world.Entities.Sum(e => (long)e.Inventory.GetValueOrDefault(ItemIds.Wood))
        + _world.Stockpiles.Sum(s => (long)s.Contents.Counts.GetValueOrDefault(ItemIds.Wood));
}
