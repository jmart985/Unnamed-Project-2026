using Godot;
using Simulation;
using NumericsVector3 = System.Numerics.Vector3;

namespace UnnamedProject2026;

public partial class EntityView : CharacterBody3D
{
    private const double CrowdRecoveryTimeoutSeconds = 8;
    private GodotNavigationAdapter? _navigationAdapter;
    private float _movementSpeed;
    private bool _isCreature;
    private string _speciesId = string.Empty;
    private string _bodyCategory = string.Empty;
    private InteractionAnchorDefinition _anchorDefinition = InteractionAnchorDefinition.Empty;
    private CreatureBodyPlan _bodyPlan;
    private Node3D? _rig;
    private Node3D? _visualModelRoot;
    private ProceduralGait? _gait;
    private Vector3 _lastPosition;
    private float _smoothedSpeed;
    private Func<Vector3?>? _greeterPosition;
    private ActionState? _currentTask;
    private ActionState? _greetingTask;

    public bool HasGreetingAttention => _greeterPosition is not null;

    // Imported Blender rigs in this prototype use +Z as their source forward;
    // normalize them at the asset boundary so every actor uses the same -Z rule.
    public Vector3 VisualForward => _visualModelRoot is null
        ? (_rig is null ? -GlobalBasis.Z : -_rig.GlobalBasis.Z)
        : _visualModelRoot.GlobalBasis.Z;

    public void AcknowledgeGreeting(Func<Vector3?> greeterPosition)
    {
        _greeterPosition = greeterPosition;
        _greetingTask = _currentTask;
        FaceGreeter();
    }

    private void FaceGreeter()
    {
        if (_rig is null) return;
        if (_greeterPosition?.Invoke() is not { } position) { _greeterPosition = null; return; }
        if (GlobalPosition.DistanceSquaredTo(position) > CreatureGreetingService.DefaultMaximumReach * CreatureGreetingService.DefaultMaximumReach)
        { _greeterPosition = null; return; }
        var direction = position - GlobalPosition;
        direction.Y = 0;
        if (direction.LengthSquared() > 0.0001f)
            _rig.GlobalRotation = new Vector3(0, Mathf.Atan2(-direction.X, -direction.Z), 0);
    }

    public string EntityId { get; private set; } = string.Empty;
    public NavigationOutcome? NavigationOutcome => _navigationAdapter?.LastOutcome;
    public NumericsVector3? NavigationTarget => _navigationAdapter?.LastTarget;
    public INavigationAdapter NavigationAdapter
        => _navigationAdapter ?? throw new InvalidOperationException("The entity view is not ready for navigation.");

    public SemanticAnchorResolution ResolveAnchor(SemanticAnchorKind kind, string anchorId)
    {
        if (_rig is null)
        {
            return new SemanticAnchorResolution(SemanticAnchorResolutionStatus.Missing, null,
                "The entity model is not ready.");
        }

        return SemanticAnchorResolver.Resolve(_rig, _anchorDefinition, kind, anchorId);
    }

    public override void _Ready()
    {
        // Actors use authoritative spacing and navigation for separation. They do not
        // become physical walls for the player or for one another, which prevents a
        // crowd from trapping a controllable character in a narrow space.
        // Actors occupy physical space so groups form a dense mob that resists
        // the player. The safety layer handles prolonged enclosure separately.
        CollisionLayer = 2;
        CollisionMask = 3;
        var navigationAgent = new NavigationAgent3D { PathDesiredDistance = 0.1f, TargetDesiredDistance = 0.1f };
        AddChild(navigationAgent);
        _navigationAdapter = new GodotNavigationAdapter(this, navigationAgent);
        BuildBodyVisuals();
    }

    private void BuildBodyVisuals()
    {
        _rig = new Node3D { Name = "Rig" };
        AddChild(_rig);
        _gait = new ProceduralGait { Body = _rig, ReferenceSpeed = Mathf.Max(_movementSpeed, 0.5f) };

        // Production creature assets are selected by species while the
        // procedural body remains an intentional fallback. This lets a single
        // missing import or an unsupported species never prevent the simulation
        // from spawning its entities.
        if (TryBuildProductionCreature())
        {
            return;
        }

        // Human NPCs use the project-owned Humanoid Rig v1 prototype when the
        // import is available. PlayerCharacter remains separate so this asset
        // slice cannot change the player controller or creation flow.
        if (TryBuildProductionHumanoid())
        {
            return;
        }

        // Deterministic per-entity variation so individuals are distinguishable without extra state.
        var hash = (uint)StringComparer.Ordinal.GetHashCode(EntityId);
        var variation = (hash % 100) / 100f;
        var (bodyColor, accentColor) = _speciesId switch
        {
            "cow" => (new Color(0.92f, 0.9f, 0.85f), new Color(0.15f, 0.12f, 0.1f)),
            "wolf" => (new Color(0.45f, 0.45f, 0.5f), new Color(0.2f, 0.2f, 0.24f)),
            "ape" => (new Color(0.3f, 0.2f, 0.14f), new Color(0.55f, 0.4f, 0.3f)),
            "pig" or "swine" or "boar" => (new Color(0.64f, 0.36f, 0.32f), new Color(0.35f, 0.16f, 0.14f)),
            _ when _isCreature => (new Color(0.6f, 0.45f + variation * 0.15f, 0.3f), new Color(0.35f, 0.25f, 0.15f)),
            _ => (Color.FromHsv(variation, 0.45f, 0.75f), new Color(0.85f, 0.68f + variation * 0.12f, 0.55f))
        };

        if (_bodyPlan != CreatureBodyPlan.Humanoid)
        {
            BuildQuadruped(bodyColor, accentColor);
        }
        else
        {
            BuildUpright(bodyColor, accentColor, variation);
        }
    }

    private bool TryBuildProductionCreature()
    {
        if (!_isCreature)
        {
            return false;
        }

        var (assetPath, displayName, totalHeight, radius) = _speciesId switch
        {
            "wolf" => ("res://assets/creatures/wolf/wolf_canid_rig_v1.glb", "WolfProductionModel", 1.0f, 0.32f),
            "cow" => ("res://assets/creatures/cow/cow_ungulate_rig_v1.glb", "CowProductionModel", 1.65f, 0.45f),
            "ape" => ("res://assets/creatures/ape/ape_primate_rig_v1.glb", "ApeProductionModel", 1.75f, 0.38f),
            "fox" or "vulpine" => ("res://assets/creatures/fox/fox_vulpine_rig_v1.glb", "FoxProductionModel", 0.95f, 0.24f),
            "cat" or "feline" => ("res://assets/creatures/cat/cat_felid_rig_v1.glb", "CatProductionModel", 0.80f, 0.24f),
            "pig" or "swine" or "boar" => ("res://assets/creatures/pig/pig_suiform_rig_v1.glb", "PigProductionModel", 0.78f, 0.30f),
            _ => (string.Empty, string.Empty, 0f, 0f)
        };
        if (string.IsNullOrEmpty(assetPath))
        {
            return false;
        }

        var packed = GD.Load<PackedScene>(assetPath);
        if (packed is null)
        {
            GD.PushWarning($"{displayName} is unavailable; using the procedural creature fallback.");
            return false;
        }

        var model = AddImportedModel(packed, displayName);
        AddCollision(totalHeight, radius);

        var animationPlayer = model.FindChild("AnimationPlayer", recursive: true, owned: false) as AnimationPlayer;
        if (animationPlayer is not null)
        {
            var idle = animationPlayer.GetAnimationList()
                .FirstOrDefault(name => string.Equals(name, "idle", StringComparison.OrdinalIgnoreCase));
            if (idle is not null)
            {
                animationPlayer.Play(idle);
            }
        }

        return true;
    }

    private bool TryBuildProductionHumanoid()
    {
        if (_isCreature || !_speciesId.Equals("human", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        const string assetPath = "res://assets/humanoids/human/human_humanoid_rig_v1.glb";
        var packed = GD.Load<PackedScene>(assetPath);
        if (packed is null)
        {
            GD.PushWarning("Human Humanoid Rig v1 is unavailable; using the procedural human fallback.");
            return false;
        }

        var model = AddImportedModel(packed, "HumanProductionModel");
        AddCollision(totalHeight: 1.75f, radius: 0.32f);

        var animationPlayer = model.FindChild("AnimationPlayer", recursive: true, owned: false) as AnimationPlayer;
        if (animationPlayer is not null)
        {
            var idle = animationPlayer.GetAnimationList()
                .FirstOrDefault(name => string.Equals(name, "human_idle", StringComparison.OrdinalIgnoreCase))
                ?? animationPlayer.GetAnimationList()
                    .FirstOrDefault(name => string.Equals(name, "idle", StringComparison.OrdinalIgnoreCase));
            if (idle is not null)
            {
                animationPlayer.Play(idle);
            }
        }

        return true;
    }

    private Node3D AddImportedModel(PackedScene packed, string displayName)
    {
        // Keep source-forward correction on a wrapper so imported idle animations
        // cannot overwrite the project's canonical -Z orientation.
        _visualModelRoot = new Node3D
        {
            Name = $"{displayName}ForwardAdapter",
            Rotation = Vector3.Up * Mathf.Pi
        };
        _rig!.AddChild(_visualModelRoot);
        var model = packed.Instantiate<Node3D>();
        model.Name = displayName;
        _visualModelRoot.AddChild(model);
        return model;
    }

    private void AddCollision(float totalHeight, float radius)
    {
        radius = Mathf.Min(radius, totalHeight * 0.5f);
        AddChild(new CollisionShape3D
        {
            Shape = new CapsuleShape3D { Radius = radius, Height = totalHeight },
            Position = new Vector3(0, totalHeight * 0.5f, 0)
        });
    }

    private static Node3D AddJoint(Node3D parent, Vector3 position)
    {
        var joint = new Node3D { Position = position };
        parent.AddChild(joint);
        return joint;
    }

    private void AddLeg(Node3D parent, Vector3 hip, float length, float thickness, Color color, Color footColor,
        float phase, float swing, float knee, int fastGroup = 0)
    {
        var upper = AddJoint(parent, hip);
		AddPart(new CapsuleMesh { Radius = thickness * 0.5f, Height = length * 0.5f + thickness * 0.45f },
			new Vector3(0, -length * 0.25f, 0), color, parent: upper);
		var lower = AddJoint(upper, new Vector3(0, -length * 0.5f, 0));
		AddPart(new CapsuleMesh { Radius = thickness * 0.42f, Height = length * 0.5f },
			new Vector3(0, -length * 0.25f, 0), color, parent: lower);
		AddPart(new SphereMesh { Radius = thickness * 0.55f, Height = thickness * 0.65f },
			new Vector3(0, -length * 0.5f + thickness * 0.12f, -thickness * 0.42f), footColor, parent: lower);
        _gait!.Limbs.Add(new ProceduralGait.Limb { Pivot = upper, Phase = phase, Amplitude = swing, FastGroup = fastGroup });
        _gait.Limbs.Add(new ProceduralGait.Limb { Pivot = lower, Phase = phase + Mathf.Pi * 0.5f, Amplitude = -knee, Rectify = true, FastGroup = fastGroup });
    }

    private void AddArm(Node3D parent, Vector3 shoulder, float length, float thickness, Color sleeve, Color hand,
        float phase, float swing)
    {
        var upper = AddJoint(parent, shoulder);
        AddPart(new SphereMesh { Radius = thickness * 0.75f, Height = thickness * 1.5f }, Vector3.Zero, sleeve, parent: upper);
		AddPart(new CapsuleMesh { Radius = thickness * 0.5f, Height = length * 0.5f + thickness * 0.45f },
			new Vector3(0, -length * 0.25f, 0), sleeve, parent: upper);
		var lower = AddJoint(upper, new Vector3(0, -length * 0.5f, 0));
		AddPart(new CapsuleMesh { Radius = thickness * 0.42f, Height = length * 0.5f },
			new Vector3(0, -length * 0.25f, 0), hand, parent: lower);
        AddPart(new SphereMesh { Radius = thickness * 0.5f, Height = thickness }, new Vector3(0, -length * 0.5f, 0), hand, parent: lower);
        _gait!.Limbs.Add(new ProceduralGait.Limb { Pivot = upper, Phase = phase, Amplitude = swing, IdleAmplitude = 0.03f, IdleFrequency = 1.1f });
        _gait.Limbs.Add(new ProceduralGait.Limb { Pivot = lower, Phase = phase + Mathf.Pi * 0.5f, Amplitude = 0.25f, Bias = 0.3f });
    }

    private void BuildQuadruped(Color bodyColor, Color accentColor)
    {
        var rig = _rig ?? throw new InvalidOperationException("Build the body rig before its limbs.");
        var bodySize = _speciesId switch
        {
            "cow" => new Vector3(0.9f, 0.8f, 1.45f),
            "wolf" => new Vector3(0.6f, 0.5f, 1.2f),
            "ape" => new Vector3(0.82f, 0.62f, 1.15f),
            "pig" or "swine" or "boar" => new Vector3(0.78f, 0.62f, 1.05f),
            _ when _bodyPlan == CreatureBodyPlan.Ungulate => new Vector3(0.88f, 0.78f, 1.4f),
            _ when _bodyPlan == CreatureBodyPlan.Canid => new Vector3(0.6f, 0.5f, 1.2f),
            _ when _bodyPlan == CreatureBodyPlan.Primate => new Vector3(0.82f, 0.62f, 1.15f),
            _ => new Vector3(0.75f, 0.65f, 1.25f)
        };
        var ape = _bodyPlan == CreatureBodyPlan.Primate;
        var legLength = bodySize.Y * (_bodyPlan == CreatureBodyPlan.Canid ? 0.85f
            : ape ? 0.95f : 0.75f);
        // A great-ape silhouette is defined by long weight-bearing forelimbs,
        // shorter hind legs, and no canine-style tail.
        var frontLegLength = ape ? bodySize.Y * 1.15f : legLength;
        var rearLegLength = ape ? bodySize.Y * 0.82f : legLength;
        var bodyY = Mathf.Max(frontLegLength, rearLegLength) + bodySize.Y * 0.5f - 0.05f;
        AddCollision(Mathf.Max(frontLegLength, rearLegLength) + bodySize.Y, Mathf.Max(bodySize.X, bodySize.Z) * 0.4f);
        _gait!.BobHeight = 0.03f;
        _gait.RadiansPerMeter = 3.4f;

        // Barrel chest and slimmer hindquarters read as one animal rather than a box.
		AddPart(new SphereMesh { Radius = 0.5f, Height = 2f }, new Vector3(0, bodyY, 0), bodyColor,
			scale: new Vector3(bodySize.X, bodySize.Y * 0.5f, bodySize.Z));
		AddPart(new SphereMesh { Radius = 0.5f, Height = 2f },
			new Vector3(0, bodyY - 0.02f, bodySize.Z * 0.38f), bodyColor.Darkened(0.08f),
			scale: new Vector3(bodySize.X * 0.9f, bodySize.Y * 0.42f, bodySize.Z * 0.4f));
        if (_bodyPlan == CreatureBodyPlan.Ungulate)
        {
            AddPart(new BoxMesh { Size = new Vector3(0.5f, 0.3f, 0.5f) }, new Vector3(0.2f, bodyY + bodySize.Y * 0.25f, 0.1f), accentColor);
            AddPart(new BoxMesh { Size = new Vector3(0.3f, 0.2f, 0.25f) }, new Vector3(0, legLength - 0.02f, bodySize.Z * 0.3f), new Color(0.95f, 0.7f, 0.7f));
        }

        var head = AddJoint(rig, new Vector3(0, bodyY + bodySize.Y * 0.3f, -bodySize.Z * 0.5f));
        AddPart(new BoxMesh { Size = new Vector3(0.3f, 0.34f, 0.4f) }, new Vector3(0, 0.02f, -0.12f), bodyColor, new Vector3(-25, 0, 0), head);
        AddPart(new BoxMesh { Size = new Vector3(0.38f, 0.38f, 0.42f) }, new Vector3(0, 0.1f, -0.38f), bodyColor, parent: head);
        AddPart(new BoxMesh { Size = new Vector3(0.22f, 0.18f, 0.24f) }, new Vector3(0, 0.02f, -0.66f), accentColor, parent: head);
        if (_bodyPlan == CreatureBodyPlan.Primate)
        {
            // Broad brow, short muzzle and rounded ears distinguish an ape from
            // the pointed-eared canid silhouette.
            AddPart(new BoxMesh { Size = new Vector3(0.3f, 0.12f, 0.12f) }, new Vector3(0, 0.3f, -0.34f), accentColor, parent: head);
            AddPart(new SphereMesh { Radius = 0.16f, Height = 0.2f }, new Vector3(0, 0.02f, -0.68f), accentColor, parent: head);
            foreach (var x in new[] { -0.22f, 0.22f })
                AddPart(new SphereMesh { Radius = 0.1f, Height = 0.16f }, new Vector3(x, 0.14f, -0.25f), bodyColor, parent: head);
        }
        else
        {
            AddPart(new BoxMesh { Size = new Vector3(0.1f, 0.18f, 0.06f) }, new Vector3(-0.17f, 0.37f, -0.3f), accentColor, new Vector3(0, 0, 20), head);
            AddPart(new BoxMesh { Size = new Vector3(0.1f, 0.18f, 0.06f) }, new Vector3(0.17f, 0.37f, -0.3f), accentColor, new Vector3(0, 0, -20), head);
        }
        foreach (var x in new[] { -0.1f, 0.1f })
        {
            AddPart(new SphereMesh { Radius = 0.04f, Height = 0.08f }, new Vector3(x, 0.17f, -0.58f), Colors.Black, parent: head);
        }

        if (_bodyPlan == CreatureBodyPlan.Ungulate)
        {
            foreach (var x in new[] { -0.2f, 0.2f })
            {
                AddPart(new CapsuleMesh { Height = 0.3f, Radius = 0.03f }, new Vector3(x, 0.4f, -0.36f), new Color(0.9f, 0.85f, 0.7f), new Vector3(0, 0, x < 0 ? 35 : -35), head);
            }
        }

        _gait.Limbs.Add(new ProceduralGait.Limb { Pivot = head, Phase = 0.5f, Amplitude = 0.07f, IdleAmplitude = 0.12f, IdleFrequency = 0.7f });

        if (!ape)
        {
            var tail = AddJoint(_rig, new Vector3(0, bodyY + bodySize.Y * 0.2f, bodySize.Z * 0.5f));
            var tailLength = _bodyPlan == CreatureBodyPlan.Canid ? 0.6f : 0.5f;
            AddPart(new BoxMesh { Size = new Vector3(0.1f, 0.1f, tailLength) }, new Vector3(0, -0.07f, tailLength * 0.5f), accentColor, new Vector3(25, 0, 0), tail);
            _gait.Limbs.Add(new ProceduralGait.Limb { Pivot = tail, Axis = Vector3.Up, Phase = 0, Amplitude = 0.35f, IdleAmplitude = 0.3f, IdleFrequency = 1.3f });
        }

        var frontZ = -bodySize.Z * 0.32f;
        var rearZ = bodySize.Z * 0.32f;
        var legX = bodySize.X * 0.32f;
        var thickness = _bodyPlan == CreatureBodyPlan.Canid ? 0.12f : _bodyPlan == CreatureBodyPlan.Primate ? 0.18f : 0.16f;
        var handColor = _bodyPlan == CreatureBodyPlan.Primate ? new Color(0.22f, 0.14f, 0.09f) : accentColor;
        // Apes knuckle-walk: the long front limbs carry weight on the ground.
        // FastGroup pairs both front limbs, then both hind legs for a gallop.
        AddLeg(_rig, new Vector3(-legX, frontLegLength, frontZ), frontLegLength, thickness, bodyColor, handColor, 0, 0.55f, 0.6f, ape ? 1 : 0);
        AddLeg(_rig, new Vector3(legX, rearLegLength, rearZ), rearLegLength, thickness, bodyColor, accentColor, 0, 0.55f, 0.6f, ape ? 2 : 0);
        AddLeg(_rig, new Vector3(legX, frontLegLength, frontZ), frontLegLength, thickness, bodyColor, handColor, Mathf.Pi, 0.55f, 0.6f, ape ? 1 : 0);
        AddLeg(_rig, new Vector3(-legX, rearLegLength, rearZ), rearLegLength, thickness, bodyColor, accentColor, Mathf.Pi, 0.55f, 0.6f, ape ? 2 : 0);
    }

    private void BuildUpright(Color shirt, Color skin, float variation)
    {
        var rig = _rig ?? throw new InvalidOperationException("Build the body rig before its limbs.");
        var ape = _speciesId == "ape";
        var legLength = ape ? 0.55f : 0.9f;
        var shoulderWidth = ape ? 0.85f : 0.5f;
        var chest = ape ? new Vector3(0.75f, 0.6f, 0.45f) : new Vector3(0.5f, 0.55f, 0.28f);
        var armLength = ape ? 0.95f : 0.68f;
        var lean = ape ? -0.5f : 0f;
        var pants = ape ? shirt : new Color(0.22f, 0.25f, 0.36f);
        var armColor = ape ? shirt : shirt.Darkened(0.1f);
        var handColor = ape ? skin : skin;
        var totalHeight = legLength + chest.Y + 0.35f;
        AddCollision(totalHeight, 0.32f);
        _gait!.BobHeight = ape ? 0.05f : 0.035f;

		AddPart(new SphereMesh { Radius = 0.5f, Height = 2f }, new Vector3(0, legLength, 0), pants,
			scale: new Vector3(chest.X * 0.88f, 0.1f, chest.Z * 0.92f));
		var torso = AddJoint(rig, new Vector3(0, legLength, 0));
		torso.Rotation = new Vector3(lean, 0, 0);
		AddPart(new SphereMesh { Radius = 0.5f, Height = 2f }, new Vector3(0, chest.Y * 0.5f + 0.05f, 0), shirt,
			scale: new Vector3(chest.X, chest.Y * 0.5f, chest.Z), parent: torso);
		AddPart(new SphereMesh { Radius = 0.5f, Height = 2f }, new Vector3(0, 0.02f, 0), shirt.Darkened(0.25f),
			scale: new Vector3(chest.X * 0.72f, 0.05f, chest.Z * 0.8f), parent: torso);
        AddPart(new CylinderMesh { TopRadius = 0.07f, BottomRadius = 0.08f, Height = 0.12f }, new Vector3(0, chest.Y + 0.1f, 0), skin, parent: torso);

        var headPivot = AddJoint(torso, new Vector3(0, chest.Y + 0.15f, ape ? -0.08f : 0));
        AddPart(new SphereMesh { Height = 0.46f, Radius = 0.22f }, new Vector3(0, 0.15f, 0), skin, parent: headPivot);
        AddPart(new SphereMesh { Radius = 0.03f, Height = 0.06f }, new Vector3(-0.08f, 0.2f, -0.19f), Colors.Black, parent: headPivot);
        AddPart(new SphereMesh { Radius = 0.03f, Height = 0.06f }, new Vector3(0.08f, 0.2f, -0.19f), Colors.Black, parent: headPivot);
        AddPart(new BoxMesh { Size = new Vector3(0.06f, 0.08f, 0.08f) }, new Vector3(0, 0.12f, -0.22f), skin.Darkened(0.08f), parent: headPivot);
        foreach (var x in new[] { -0.22f, 0.22f })
        {
            AddPart(new SphereMesh { Radius = 0.05f, Height = 0.1f }, new Vector3(x, 0.15f, 0), skin, parent: headPivot);
        }

        if (ape)
        {
            AddPart(new BoxMesh { Size = new Vector3(0.3f, 0.14f, 0.12f) }, new Vector3(0, 0.05f, -0.17f), skin.Lightened(0.1f), parent: headPivot);
            AddPart(new BoxMesh { Size = new Vector3(0.42f, 0.08f, 0.1f) }, new Vector3(0, 0.3f, -0.15f), shirt.Darkened(0.1f), parent: headPivot);
        }
        else
        {
            AddPart(new SphereMesh { Radius = 0.235f, Height = 0.24f }, new Vector3(0, 0.27f, 0.02f),
                new Color(0.12f + variation * 0.4f, 0.1f + variation * 0.15f, 0.08f), parent: headPivot);
        }

        _gait.Limbs.Add(new ProceduralGait.Limb { Pivot = headPivot, Axis = Vector3.Up, Amplitude = 0.05f, IdleAmplitude = 0.2f, IdleFrequency = 0.6f });

        var swing = ape ? 0.55f : 0.6f;
        var shoulderY = chest.Y + 0.02f;
        AddArm(torso, new Vector3(-(shoulderWidth * 0.5f + 0.06f), shoulderY, 0), armLength, ape ? 0.17f : 0.11f, armColor, handColor, Mathf.Pi, swing);
        AddArm(torso, new Vector3(shoulderWidth * 0.5f + 0.06f, shoulderY, 0), armLength, ape ? 0.17f : 0.11f, armColor, handColor, 0, swing);

        var hipX = chest.X * 0.28f;
        var thickness = ape ? 0.17f : 0.15f;
        AddLeg(_rig, new Vector3(-hipX, legLength, 0), legLength, thickness, pants, new Color(0.15f, 0.12f, 0.1f), 0, 0.6f, 0.9f);
        AddLeg(_rig, new Vector3(hipX, legLength, 0), legLength, thickness, pants, new Color(0.15f, 0.12f, 0.1f), Mathf.Pi, 0.6f, 0.9f);
    }

	private void AddPart(Mesh mesh, Vector3 position, Color color, Vector3? rotationDegrees = null,
		Node3D? parent = null, Vector3? scale = null)
    {
        var part = new MeshInstance3D
        {
			Mesh = mesh,
			Position = position,
			Scale = scale ?? Vector3.One,
			MaterialOverride = new StandardMaterial3D { AlbedoColor = color, Roughness = 0.82f }
        };
        if (rotationDegrees is { } rotation)
        {
            part.RotationDegrees = rotation;
        }

        (parent ?? _rig!).AddChild(part);
    }

    public override void _Process(double delta)
    {
        if (_gait is null || _rig is null || delta <= 0) return;
        var position = GlobalPosition;
        var step = new Vector3(position.X - _lastPosition.X, 0, position.Z - _lastPosition.Z);
        _lastPosition = position;
        // Large jumps are loads, resets, or respawns, not walking.
        var speed = step.Length() > 3f ? 0f : step.Length() / (float)delta;
        _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, speed, 1 - Mathf.Exp(-12 * (float)delta));
        if (_smoothedSpeed > 0.15f && step.LengthSquared() > 1e-8f)
        {
            var targetYaw = Mathf.Atan2(-step.X, -step.Z);
            _rig.Rotation = new Vector3(0, Mathf.LerpAngle(_rig.Rotation.Y, targetYaw, 1 - Mathf.Exp(-10 * (float)delta)), 0);
        }

        _gait.Advance((float)delta, _smoothedSpeed);
        if (_greeterPosition is not null)
        {
            // Attention has no timeout. Purposeful movement or a new task can override it.
            if (_smoothedSpeed > 0.15f && step.LengthSquared() > 1e-8f) _greeterPosition = null;
            else FaceGreeter();
        }
    }

    public override void _PhysicsProcess(double delta) => _navigationAdapter?.RefreshPath();

    public void Bind(string entityId, float movementSpeed, bool isCreature = false, string? speciesId = null,
        string? bodyCategory = null, InteractionAnchorDefinition? anchorDefinition = null)
    {
        if (!float.IsFinite(movementSpeed) || movementSpeed <= 0) throw new ArgumentOutOfRangeException(nameof(movementSpeed));
        EntityId = entityId;
        _movementSpeed = movementSpeed;
        _isCreature = isCreature;
        _speciesId = speciesId ?? (isCreature ? "creature" : "human");
        _bodyCategory = bodyCategory ?? (isCreature ? "quadruped" : "humanoid");
        _anchorDefinition = anchorDefinition ?? InteractionAnchorDefinition.Empty;
        _bodyPlan = CreatureBodyPlanResolver.Resolve(_bodyCategory, _isCreature);
    }

    public bool Sync(World world)
    {
        if (!world.TryGetEntity(EntityId, out var entity) || entity is null) return false;
        _currentTask = entity.Actions.Current;
        if (_greeterPosition is not null && (entity.Health.IsIncapacitated
            || (_currentTask is { Kind: not ActionKind.Idle } && !ReferenceEquals(_currentTask, _greetingTask))))
            _greeterPosition = null;
        GlobalPosition = new Vector3(entity.Position.X, entity.Position.Y, entity.Position.Z);
        return true;
    }

    // A crowd blockage should yield/replan quickly. The owning service receives
    // TimedOut and either retries the same task after the actor is free or advances
    // to its next queued task according to that service's normal priority rules.
    public string RequestNavigation(NumericsVector3 target) => NavigationAdapter.Request(
        new NavigationRequest(target, timeoutSeconds: CrowdRecoveryTimeoutSeconds));
    public bool CancelNavigation() => _navigationAdapter?.ActiveRequestId is { } requestId && _navigationAdapter.Cancel(requestId);
    public bool RemoveNavigationTarget() => _navigationAdapter?.ActiveRequestId is { } requestId && _navigationAdapter.RemoveTarget(requestId);

    public void AdvanceNavigation(double simulationSeconds, World world, SquadOrderService? squad = null,
        Func<string, NumericsVector3, NumericsVector3, NumericsVector3>? constrainStep = null)
    {
        if (_navigationAdapter is not { HasActiveRequest: true } adapter) return;
        adapter.AdvanceMovement(simulationSeconds, _movementSpeed, (current, proposed) =>
        {
            var afterSquad = squad is null ? proposed : squad.ConstrainFollowStep(EntityId, current, proposed);
            return constrainStep is null ? afterSquad : constrainStep(EntityId, current, afterSquad);
        });
        var position = GlobalPosition;
        if (!world.TrySetEntityPosition(EntityId, new NumericsVector3(position.X, position.Y, position.Z)) && adapter.ActiveRequestId is { } requestId)
            adapter.RemoveTarget(requestId);
    }
}


