using Godot;
using Simulation;
using NumericsVector3 = System.Numerics.Vector3;

namespace UnnamedProject2026;

public partial class Main
{
    private async void RunEmploymentValidation()
    {
        try
        {
            StartSession(PlayerCharacterProfile.Create("Employment validation"), 42, "Employment validation.");
            SetProcess(false);
            _playerCharacter.SetPhysicsProcess(false);
            _playerCharacter.SetProcessUnhandledInput(false);
            _clock.SetPaused(true);
            Require(_world.WorkplacePositions.Count == NewGameSession.HumanNpcCount
                && _world.WorkplacePositions.All(position => position.WorkerId is not null),
                "starter residents automatically fill the declared ongoing positions");
            var residents = _world.SettlementWorkers.Take(2).Select(setting =>
                _world.Entities.Single(entity => entity.Id == setting.EntityId)).ToArray();
            var worker = residents[0];
            var other = residents[1];
            foreach (var entity in _world.Entities.Where(entity => entity.Id != _world.PlayerEntityId
                         && entity.Id != worker.Id && entity.Id != other.Id).ToArray())
                _world.TryRemoveEntity(entity.Id);
            SyncEntityViewMembership();
            foreach (var resident in _world.SettlementWorkers) _world.UnassignWorker(resident.EntityId);
            var job = _world.WorkplacePositions.First();
            foreach (var position in _world.WorkplacePositions.Where(position => position.Id != job.Id))
                _world.RemoveWorkplacePosition(position.Id);
            _world.TrySetEntityPosition(other.Id, new NumericsVector3(80, 0, 80));
            _world.SetWorkSchedule(worker.Id, 8, 18);
            SelectEntity(worker.Id);
            _autoAssignWork.ButtonPressed = true;
            _world.SetWorkerAutoAssignment(other.Id, true);
            AdvanceSimulationTick(.1);
            Require(_world.GetEmployment(worker.Id)?.Id == job.Id && _world.GetEmployment(other.Id) is null,
                "two unemployed residents cannot fill the same vacancy");
            Require(other.Actions.Current?.Kind != ActionKind.Haul, "unemployed resident does not begin a pickup");
            UpdateInspector();
            _releaseEmployment.EmitSignal(BaseButton.SignalName.Pressed);
            AdvanceSimulationTick(.1);
            Require(_world.GetEmployment(worker.Id) is null && _world.GetEmployment(other.Id)?.Id == job.Id,
                "manual release pauses automatic reassignment and opens the position for another resident");
            _world.UnassignWorker(other.Id);
            SelectEntity(worker.Id);
            _assignEmployment.EmitSignal(BaseButton.SignalName.Pressed);
            Require(_world.GetEmployment(worker.Id)?.Id == job.Id, "management assigns the selected resident explicitly");
            var source = _world.ResourcePiles.Single();
            _world.TrySetEntityPosition(worker.Id, source.Position - new NumericsVector3(2, 0, 0));
            SyncEntityViews();
            for (var frame = 0; frame < 20; frame++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            for (var tick = 0; tick < 400 && worker.Inventory.GetValueOrDefault(ItemIds.Wood) == 0; tick++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                AdvanceSimulationTick(.1);
                SyncEntityViews();
            }
            Require(worker.Inventory.GetValueOrDefault(ItemIds.Wood) > 0,
                $"employed worker picks up wood using real navigation: {worker.Activity}, {worker.WaitingReason}");
            var cargo = worker.Inventory[ItemIds.Wood];
            _world.AddSquadMember(worker.Id);
            _squad!.Execute(SquadOrderKind.Stop);
            AdvanceSimulationTick(.1);
            Require(_world.GetEmployment(worker.Id)?.Id == job.Id
                && worker.Inventory.GetValueOrDefault(ItemIds.Wood) == cargo,
                "explicit squad interruption retains employment and carried cargo");
            _squad.Reset();
            _world.RemoveSquadMember(worker.Id);
            var saved = WorldSnapshotSerializer.Serialize(_world, _clock.SimulationTime);
            var workerId = worker.Id;
            var loadedTime = WorldSnapshotSerializer.Load(_world, saved, _speciesCatalog,
                _foodConsumption!, _woodHauling!, _wandering!, _squad!, _orders!);
            _clock.Restore(loadedTime);
            RebuildPresentationFromWorld();
            worker = _world.Entities.Single(entity => entity.Id == workerId);
            Require(_world.GetEmployment(worker.Id)?.Id == job.Id
                && worker.Inventory[ItemIds.Wood] == cargo && worker.Actions.Current is null,
                "save restores ongoing employment and cargo without restoring the old task or path");
            for (var frame = 0; frame < 20; frame++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            for (var tick = 0; tick < 700 && worker.Inventory.GetValueOrDefault(ItemIds.Wood) > 0; tick++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                AdvanceSimulationTick(.1);
                SyncEntityViews();
            }
            Require(_world.Stockpiles.Single().Contents.Counts.GetValueOrDefault(ItemIds.Wood) >= cargo
                && worker.Inventory.GetValueOrDefault(ItemIds.Wood) == 0,
                $"restored employee delivers through real navigation: {worker.Activity}, {worker.WaitingReason}");
            Require(_world.GetEmployment(worker.Id)?.Id == job.Id && WoodTotal() == 100,
                "completed tasks preserve employment and conserve resources");
            _clock.Restore(100);
            AdvanceSimulationTick(.1);
            Require(_world.GetEmployment(worker.Id)?.Id == job.Id && worker.Actions.Current?.Kind != ActionKind.Haul,
                "off-shift worker keeps the job without beginning new work");
            SelectEntity(worker.Id);
            Require(_employmentSummary.Text.Contains("Wood hauler", StringComparison.Ordinal),
                "management distinguishes ongoing employment from current activity");
            var capture = OS.GetCmdlineUserArgs().FirstOrDefault(arg => arg.StartsWith("--employment-screenshot=", StringComparison.Ordinal));
            if (capture is not null && DisplayServer.GetName() != "headless")
            {
                ToggleMenu(PlayerMenu.Settlement);
                _settlementTabs.CurrentTab = 1;
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                Require(GetViewport().GetTexture().GetImage().SavePng(capture["--employment-screenshot=".Length..]) == Error.Ok,
                    "employment management capture");
            }
            GD.Print("EMPLOYMENT VALIDATION PASSED: finite ongoing vacancies, automatic and explicit assignment, real pickup/delivery, interruption, rest and save/load.");
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PushError($"EMPLOYMENT VALIDATION FAILED: {exception}");
            GetTree().Quit(1);
        }
    }
}
