using Godot;
using Simulation;

namespace UnnamedProject2026;

public partial class Main
{
    private void ValidateStarterNpcWorkbenchClearance()
    {
        var workbenchBody = _workbench.GetChildren().OfType<StaticBody3D>().Single(body => body.Name == "WorkbenchCollision");
        var benchCollider = workbenchBody.GetChildren().OfType<CollisionShape3D>().Single();
        var benchBounds = CollisionBounds(benchCollider);
        var playerCollider = _playerCharacter.GetChildren().OfType<CollisionShape3D>().Single();
        var playerBounds = CollisionBounds(playerCollider);
        var starterIds = _world.Entities
            .Where(entity => entity.SpeciesId == "human" && entity.Id != _world.PlayerEntityId)
            .Select(entity => entity.Id)
            .ToArray();
        Require(starterIds.Length == NewGameSession.HumanNpcCount, "all starter humans are present for clearance validation");
        ValidateTownGroundAlignment(starterIds);

        var npcBounds = new List<(string Id, Aabb Bounds)>();
        foreach (var id in starterIds)
        {
            var collider = _entityViews[id].GetChildren().OfType<CollisionShape3D>().Single();
            var bounds = CollisionBounds(collider);
            Require(!bounds.Intersects(benchBounds), $"starter human {id} capsule clears workbench collision");
            Require(!bounds.Intersects(playerBounds), $"starter human {id} capsule clears player collision");
            npcBounds.Add((id, bounds));
        }

        for (var first = 0; first < npcBounds.Count; first++)
        for (var second = first + 1; second < npcBounds.Count; second++)
            Require(!npcBounds[first].Bounds.Intersects(npcBounds[second].Bounds),
                $"starter humans {npcBounds[first].Id} and {npcBounds[second].Id} do not overlap");
    }

    private void ValidateTownGroundAlignment(IReadOnlyList<string> starterIds)
    {
        const float SurfaceHeightToleranceMeters = 0.001f;
        const float FloatRoundingAllowanceMeters = 0.000001f;
        var scenerySurfaces = new[]
        {
            (Name: "TownGround", ExpectedTop: 0f),
            (Name: "RoadEastWest", ExpectedTop: 0.001f),
            (Name: "RoadNorthSouth", ExpectedTop: 0.001f),
            (Name: "CountryRoadEast", ExpectedTop: 0.001f),
            (Name: "CountryRoadWest", ExpectedTop: 0.001f)
        };
        var surfaceHeights = new List<float>();
        foreach (var (name, expectedTop) in scenerySurfaces)
        {
            var meshInstance = _scenery!.GetNode<MeshInstance3D>(name);
            var box = meshInstance.Mesh as BoxMesh
                ?? throw new InvalidOperationException($"{name} must use a measurable box ground mesh.");
            var top = meshInstance.GlobalPosition.Y + box.Size.Y * 0.5f;
            Require(Mathf.Abs(top - expectedTop) <= SurfaceHeightToleranceMeters,
                $"{name} visible surface aligns with the floor at {expectedTop:0.000} m (actual {top:0.000} m)");
            surfaceHeights.Add(top);
        }

        var floorShape = GetNode<CollisionShape3D>("Floor/CollisionShape3D");
        var floor = floorShape.Shape as BoxShape3D
            ?? throw new InvalidOperationException("World floor must use a measurable box collision shape.");
        var floorTop = floorShape.GlobalPosition.Y + floor.Size.Y * 0.5f;
        Require(Mathf.Abs(floorTop) <= 0.001f
            && surfaceHeights.Max() - surfaceHeights.Min()
                <= SurfaceHeightToleranceMeters + FloatRoundingAllowanceMeters,
            "town and road surfaces meet the shared Y=0 actor floor without a visible height step");

        foreach (var id in starterIds)
        {
            var view = _entityViews[id];
            var capsuleNode = view.GetChildren().OfType<CollisionShape3D>().Single();
            var capsule = capsuleNode.Shape as CapsuleShape3D
                ?? throw new InvalidOperationException($"Starter human {id} must use a grounded capsule.");
            var capsuleBottom = capsuleNode.GlobalPosition.Y - capsule.Height * 0.5f;
            Require(Mathf.Abs(view.GlobalPosition.Y) <= 0.001f && Mathf.Abs(capsuleBottom - floorTop) <= 0.001f,
                $"starter human {id} visual root and collision capsule remain grounded at town-road height");
        }
    }

    private static Aabb CollisionBounds(CollisionShape3D collider)
    {
        var localBounds = collider.Shape switch
        {
            BoxShape3D box => new Aabb(-box.Size * 0.5f, box.Size),
            CapsuleShape3D capsule => new Aabb(
                new Vector3(-capsule.Radius, -capsule.Height * 0.5f, -capsule.Radius),
                new Vector3(capsule.Radius * 2, capsule.Height, capsule.Radius * 2)),
            _ => throw new InvalidOperationException($"Unsupported starter collision shape: {collider.Shape?.GetType().Name ?? "missing"}.")
        };
        return collider.GlobalTransform * localBounds;
    }

    private async Task ValidatePlayerLoop()
    {
        Require(_world.Entities.Count == 14 && FoodTotal() == 300 && WoodTotal() == 48
            && _world.ResourcePiles.Count(pile => pile.Id.StartsWith("tree-adult-", StringComparison.Ordinal)) == 4
            && _world.ResourcePiles.Count(pile => pile.Contents.Counts.ContainsKey(ItemIds.Rock)) == 4
            && !_world.ResourcePiles.Any(pile => pile.Id == "wood-pile-1"),
            $"survival new-game population/resources (entities {_world.Entities.Count}/14, food {FoodTotal()}/300, wood {WoodTotal()}/48, trees {_world.ResourcePiles.Count(pile => pile.Id.StartsWith("tree-adult-", StringComparison.Ordinal))}/4, rocks {_world.ResourcePiles.Count(pile => pile.Contents.Counts.ContainsKey(ItemIds.Rock))}/4, legacy wood pile {_world.ResourcePiles.Any(pile => pile.Id == "wood-pile-1")})");
        var player = _world.Entities.Single(e => e.Id == _world.PlayerEntityId);
        var initialPosition = player.Position;
        var initialSatiety = player.Satiety;
        await ValidatePlayerForwardAndBackwardInput();
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
        Require(FoodTotal() == 299 && _foodConsumption!.FoodUnitsConsumed == 1, "single consumption");
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
        Require(relation.ObserverEntityId == cow.Id && relation.TargetEntityId == player.Id && relation.Familiarity == 21
            && relation.Trust == 50 && relation.Fear == 0 && relation.Loyalty == 0, "recipient greeting and domestic baseline only");
        unchanged = WorldSnapshotSerializer.Serialize(_world, _clock.SimulationTime);
        SelectEntity(_world.Entities.Single(e => e.SpeciesId == "wolf").Id);
        Require(GreetSelectedEntity().Contains("does not accept", StringComparison.Ordinal), "unsupported capability rejection");
        SelectEntity(_world.Entities.Single(e => e.SpeciesId == "ape").Id);
        Require(GreetSelectedEntity().StartsWith("Move closer", StringComparison.Ordinal), "out-of-range greeting rejection");
        _selectedEntityId = "missing";
        Require(GreetSelectedEntity().Contains("no longer exists", StringComparison.Ordinal), "missing greeting rejection");
        Require(unchanged == WorldSnapshotSerializer.Serialize(_world, _clock.SimulationTime), "greeting rejections preserve World");
    }

    private async Task ValidatePlayerForwardAndBackwardInput()
    {
        var originalPosition = _playerCharacter.GlobalPosition;
        var originalRotation = _playerCharacter.Rotation;
        var start = new Vector3(150, 0, 150);
        var expectedForward = Vector3.Forward;
        try
        {
            _playerCharacter.GlobalPosition = start;
            _playerCharacter.Rotation = Vector3.Zero;
            SyncPlayerPosition();
            _playerCharacter.SetPhysicsProcess(true);
            Input.ActionPress(ControllerActions.MoveForward);
            try
            {
                for (var frame = 0; frame < 12; frame++)
                    await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            }
            finally
            {
                Input.ActionRelease(ControllerActions.MoveForward);
            }
            var forwardDelta = _playerCharacter.GlobalPosition - start;
            Require(forwardDelta.Dot(expectedForward) > 0.05f, "W moves along the character's visual forward (-Z) direction");

            var backwardStart = _playerCharacter.GlobalPosition;
            Input.ActionPress(ControllerActions.MoveBackward);
            try
            {
                for (var frame = 0; frame < 36; frame++)
                    await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            }
            finally
            {
                Input.ActionRelease(ControllerActions.MoveBackward);
            }
            var backwardDelta = _playerCharacter.GlobalPosition - backwardStart;
            Require(backwardDelta.Dot(expectedForward) < -0.05f, "S moves opposite the character's visual forward direction");
        }
        finally
        {
            Input.ActionRelease(ControllerActions.MoveForward);
            Input.ActionRelease(ControllerActions.MoveBackward);
            _playerCharacter.SetPhysicsProcess(false);
            _playerCharacter.GlobalPosition = originalPosition;
            _playerCharacter.Rotation = originalRotation;
            SyncPlayerPosition();
        }
    }

    private long FoodTotal() => _world.FoodSources.Sum(s => s.Contents.Counts.Where(i => ItemIds.IsEdibleFood(i.Key)).Sum(i => (long)i.Value))
        + _world.Entities.Sum(e => e.Inventory.Where(i => ItemIds.IsEdibleFood(i.Key)).Sum(i => (long)i.Value))
        + _world.Stockpiles.Sum(s => s.Contents.Counts.Where(i => ItemIds.IsEdibleFood(i.Key)).Sum(i => (long)i.Value));
    private long WoodTotal() => _world.ResourcePiles.Sum(s => (long)s.Contents.Counts.GetValueOrDefault(ItemIds.Wood))
        + _world.Entities.Sum(e => (long)e.Inventory.GetValueOrDefault(ItemIds.Wood))
        + _world.Stockpiles.Sum(s => (long)s.Contents.Counts.GetValueOrDefault(ItemIds.Wood));
}
