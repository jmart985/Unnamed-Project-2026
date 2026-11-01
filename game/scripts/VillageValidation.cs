using Godot;
using Simulation;
using NumericsVector3 = System.Numerics.Vector3;

namespace UnnamedProject2026;

public partial class Main
{
    private async void RunVillageValidation()
    {
        try
        {
            _playerNameInput.Text = "Village validation";
            StartNewGame();
            SetProcess(false);
            _playerCharacter.SetPhysicsProcess(false);
            Require(_world.UsesVillageLayout && _world.DayLengthSeconds == 120 && _world.Structures.Count == 10,
                "new-game menu creates ten resident houses and a two-minute day");
            Require(!_scenery!.GetChildren().Any(node => node.Name.ToString().StartsWith("Building_")),
                "placeholder town blocks are gone");
            var houses = _world.Structures.ToArray();
            foreach (var house in houses)
            {
                var view = _constructionViews[new(TargetKind.Structure, house.Id)];
                var floor = (BoxShape3D)view.GetNode<CollisionShape3D>("BuildingCollision/Floor").Shape;
                Require(floor.Size.X == 8 && floor.Size.Z == 8, "every resident home uses the doubled solid footprint");
            }
            for (var frame = 0; frame < 20; frame++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            if (OS.GetCmdlineUserArgs().Contains("--capture-village"))
            {
                await CaptureVillageEvidence(houses[0]);
                GetTree().Quit();
                return;
            }

            // Move the real largest player capsule through every cardinal doorway in both directions.
            foreach (var sex in Enum.GetValues<PlayerSex>())
            {
                _playerCharacter.BindPlayer(_world.PlayerEntityId!, PlayerCharacterProfile.Create("Door clearance",
                    PlayerHeight.Tall, PlayerBodyType.Thick, sex, 1, 1), (float)_speciesCatalog.GetRequired("human").MovementSpeed);
                foreach (var house in houses.GroupBy(house => house.FacingQuarterTurns).Select(group => group.First()))
                {
                    var outside = house.ToWorld(new NumericsVector3(0, .025f, 6));
                    var inside = house.ToWorld(new NumericsVector3(0, .025f, 0));
                    _playerCharacter.GlobalPosition = new(outside.X, outside.Y, outside.Z);
                    _playerCharacter.Velocity = Vector3.Zero;
                    await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                    await MoveVillagePlayerTo(inside);
                    await MoveVillagePlayerTo(outside);
                }
            }
            var selectedHouse = houses[0];
            var porch = selectedHouse.ToWorld(new NumericsVector3(0, .025f, 5.5f));
            var center = selectedHouse.ToWorld(new NumericsVector3(0, .025f, 0));
            selectedHouse.IsClosed = true;
            SyncHouseDoorNavigation();
            for (var frame = 0; frame < 4; frame++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            _playerCharacter.GlobalPosition = new(porch.X, porch.Y, porch.Z);
            var toward = new Vector3(center.X - porch.X, 0, center.Z - porch.Z).Normalized();
            for (var step = 0; step < 60; step++) _playerCharacter.MoveAndCollide(toward * .1f);
            Require(!HouseGeometry.Contains(selectedHouse, new(_playerCharacter.GlobalPosition.X, 0, _playerCharacter.GlobalPosition.Z)),
                "closed doorway physically stops the real player capsule");
            SyncPlayerPosition();
            _selectedTarget = new(TargetKind.Structure, selectedHouse.Id);
            PerformFriendlyInteraction();
            Require(!selectedHouse.IsClosed, "player can reopen a door while standing outside it");
            for (var frame = 0; frame < 4; frame++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            await MoveVillagePlayerTo(center);
            _playerCharacter.GlobalPosition = new(0, 0, -10);
            SyncPlayerPosition();

            foreach (var house in houses)
            {
                var resident = _world.Entities.Single(entity => entity.Id == house.ResidentId);
                var outside = house.ToWorld(new NumericsVector3(0, 0, 7));
                _world.TrySetEntityPosition(resident.Id, outside);
                SyncEntityViews();
                var view = _entityViews[resident.Id];
                foreach (var destination in new[] { house.Position, outside })
                {
                    var request = view.NavigationAdapter.Request(new NavigationRequest(destination, timeoutSeconds: 30, arrivalTolerance: .3));
                    for (var tick = 0; tick < 250 && NumericsVector3.Distance(resident.Position, destination) > .4f; tick++)
                    {
                        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                        AdvanceNavigationTick(.1);
                        Require(!IsBlockedCell(resident.Position.X - .02f, resident.Position.X + .02f,
                            resident.Position.Z - .02f, resident.Position.Z + .02f, CurrentWorkbenchPositions(), _world.Structures),
                            $"{resident.Id} follows the doorway rather than crossing a wall");
                    }
                    Require(NumericsVector3.Distance(resident.Position, destination) < .45f,
                        $"{resident.Id} navigates through its doorway in both directions ({view.NavigationOutcome}, {resident.Position})");
                    view.NavigationAdapter.Cancel(request);
                }
            }

            var worker = _world.Entities.Single(entity => entity.Id == selectedHouse.ResidentId);
            _world.TrySetEntityPosition(worker.Id, selectedHouse.ToWorld(new NumericsVector3(0, 0, 2.6f)));
            selectedHouse.IsClosed = true;
            _woodHauling!.ReconcileSchedule(worker, 0);
            Require(!selectedHouse.IsClosed, "capable resident opens its own door when departing for work");
            _world.TrySetEntityPosition(worker.Id, selectedHouse.ToWorld(new NumericsVector3(0, 0, 5.1f)));
            selectedHouse.IsClosed = true;
            _woodHauling.ReconcileSchedule(worker, 60);
            Require(!selectedHouse.IsClosed, "capable resident opens its own door on returning to rest");
            SyncHouseDoorNavigation();
            SyncEntityViews();
            _clock.Restore(0);
            _clock.SetPaused(false);
            var rested = new HashSet<string>(StringComparer.Ordinal);
            var resumed = new HashSet<string>(StringComparer.Ordinal);
            for (var tick = 0; tick < 1300; tick++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                _clock.Advance(.1, AdvanceSimulationTick);
                SyncEntityViews();
                foreach (var house in houses)
                {
                    var npc = _world.Entities.Single(entity => entity.Id == house.ResidentId);
                    var working = _world.EvaluateWorkSchedule(npc.Id, _clock.SimulationTime).IsWorking;
                    if (!working && NumericsVector3.Distance(npc.Position, house.Position) < .85f) rested.Add(npc.Id);
                    if (working && rested.Contains(npc.Id) && npc.Activity is not ("Resting" or "Resting at home" or "Going home")) resumed.Add(npc.Id);
                }
            }
            Require(rested.Count == 10 && resumed.Count == 10,
                $"two-minute day drives every resident through home rest and back to work: rested {rested.Count}, resumed {resumed.Count}");
            var saved = WorldSnapshotSerializer.Serialize(_world, _clock.SimulationTime);
            var loadedTime = WorldSnapshotSerializer.Load(_world, saved, _speciesCatalog,
                _woodHauling!, _foodConsumption!, _wandering!, _squad!, _orders!);
            _clock.Restore(loadedTime);
            RebuildPresentationFromWorld();
            Require(_world.DayLengthSeconds == 120 && _world.Structures.Count == 10 && _world.UsesVillageLayout,
                "saved village restores resident homes, facing and the faster clock");
            ResetScenario();
            Require(_world.UsesVillageLayout && _world.Structures.Count == 10 && _world.DayLengthSeconds == 120,
                "reset preserves the selected village scenario and clock");
            GD.Print("VILLAGE VALIDATION PASSED: ten homes, real capsule clearance, NPC doorway routes, work/rest cycle and save/reset.");
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PushError($"VILLAGE VALIDATION FAILED: {exception}");
            GetTree().Quit(1);
        }
    }

    private async Task MoveVillagePlayerTo(NumericsVector3 destination)
    {
        var target = new Vector3(destination.X, destination.Y, destination.Z);
        for (var step = 0; step < 100 && _playerCharacter.GlobalPosition.DistanceTo(target) > .08f; step++)
        {
            _playerCharacter.MoveAndCollide((target - _playerCharacter.GlobalPosition).LimitLength(.12f));
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
        Require(_playerCharacter.GlobalPosition.DistanceTo(target) < .1f,
            $"actual player enters/exits without a threshold or door jam: {_playerCharacter.GlobalPosition} -> {target}");
    }

    private async Task CaptureVillageEvidence(Structure house)
    {
        if (DisplayServer.GetName() == "headless") throw new InvalidOperationException("Capture requires rendering.");
        var camera = GetNode<Camera3D>("Camera3D");
        camera.Current = true;
        _menuHud.Visible = false;
        var directory = ProjectSettings.GlobalizePath("res://../docs/evidence/village");
        Directory.CreateDirectory(directory);
        camera.Fov = 50;
        camera.GlobalPosition = new(80, 90, 95);
        camera.LookAt(Vector3.Zero);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng(Path.Combine(directory, "village.png"));
        camera.Fov = 55;
        var entrance = house.ToWorld(new NumericsVector3(9, 6.5f, 11));
        camera.GlobalPosition = new(entrance.X, entrance.Y, entrance.Z);
        camera.LookAt(new(house.Position.X, 2, house.Position.Z));
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng(Path.Combine(directory, "house.png"));
    }
}
