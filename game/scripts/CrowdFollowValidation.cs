using Godot;
using Simulation;
using NumericsVector3 = System.Numerics.Vector3;

namespace UnnamedProject2026;

public partial class Main
{
    private async Task ValidateCrowdedFollowRedirect()
    {
        _playerCharacter.SetPhysicsProcess(false);
        _playerCharacter.GlobalPosition = Vector3.Zero;
        SyncPlayerPosition();
        var residents = _world.Entities.Where(entity => entity.Id != _world.PlayerEntityId && entity.SpeciesId == "human").Take(2).ToArray();
        var follower = residents[0];
        var obstacle = residents[1];
        foreach (var individualOrder in new[] { false, true })
        {
            _squad!.Reset();
            _orders!.Reset();
            foreach (var id in _world.SquadMemberIds.ToArray()) _world.RemoveSquadMember(id);
            var index = 0;
            foreach (var entity in _world.Entities.Where(entity => entity.Id != _world.PlayerEntityId))
            {
                entity.Actions.Cancel();
                _world.TrySetEntityPosition(entity.Id, new NumericsVector3(40 + index++ * 2, 0, 40));
            }
            _world.TrySetEntityPosition(follower.Id, new NumericsVector3(4, 0, 0));
            if (individualOrder)
            {
                _clock.SetPaused(false);
                _world.ApplyRelationshipAdjustment(follower.Id, _world.PlayerEntityId!, new RelationshipAdjustment(0, 0, 0, 80));
                IssueIndividualOrder(follower.Id, "follow");
                _orders.Advance();
            }
            else
            {
                _world.AddSquadMember(follower.Id);
                Require(_squad.Execute(SquadOrderKind.FollowPlayer).Code == SquadResult.Accepted,
                    "crowded boundary fixture starts the normal squad follow intent");
                _squad.Advance();
            }
            // Reproduce a stale path near the holding circle and a neighbour that
            // redirects it sideways. Keep the live order registered while the native
            // view receives that step, as happens between normal replans.
            _world.TrySetEntityPosition(follower.Id, new NumericsVector3(2.02f, 0, 0));
            _world.TrySetEntityPosition(obstacle.Id, new NumericsVector3(2.02f, 0, .86f));
            SyncEntityViews();
            var view = _entityViews[follower.Id];
            view.NavigationAdapter.Request(new NavigationRequest(new NumericsVector3(1.98f, 0, .2f),
                timeoutSeconds: 30, arrivalTolerance: .001));
            for (var frame = 0; frame < 3; frame++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            var before = view.GlobalPosition;
            view.AdvanceNavigation(.1, _world, _squad, _orders, ConstrainActorSpacing);
            Require(HorizontalDistance(view.GlobalPosition, _playerCharacter.GlobalPosition) >= 1.9999f,
                $"{(individualOrder ? "individual" : "squad")} follow remains outside 2 m after crowd redirection: {view.GlobalPosition}");
            Require(view.GlobalPosition.DistanceTo(before) > .005f
                && NumericsVector3.Distance(follower.Position, obstacle.Position) >= .8499f,
                "crowded follow can advance without intersecting its neighbour");
        }
    }
}
