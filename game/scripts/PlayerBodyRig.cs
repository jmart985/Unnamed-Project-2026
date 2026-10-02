using Godot;
using Simulation;

namespace UnnamedProject2026;

/// <summary>
/// Presentation-only jointed humanoid rig for the player, built from <see cref="PlayerBodyProportions"/>.
/// Exposes independent pivots (head, torso twist, shoulders, elbows, hips, knees) so locomotion, combat poses
/// and head-look can each drive their own joints.
/// </summary>
public sealed class PlayerBodyRig
{
    public Node3D Root { get; } = new() { Name = "Rig" };
    public Node3D Torso { get; private set; } = null!;
    public Node3D Head { get; private set; } = null!;
    public Node3D LeftShoulder { get; private set; } = null!;
    public Node3D RightShoulder { get; private set; } = null!;
    public Node3D LeftElbow { get; private set; } = null!;
    public Node3D RightElbow { get; private set; } = null!;
    public Node3D LeftHip { get; private set; } = null!;
    public Node3D RightHip { get; private set; } = null!;
    public Node3D LeftKnee { get; private set; } = null!;
    public Node3D RightKnee { get; private set; } = null!;
    public PlayerBodyProportions Proportions { get; }

    public PlayerBodyRig(PlayerBodyProportions proportions)
    {
        Proportions = proportions;
        Build();
    }

    private static Node3D Joint(Node3D parent, string name, Vector3 position)
    {
        var joint = new Node3D { Name = name, Position = position };
        parent.AddChild(joint);
        return joint;
    }

    private static void Part(Node3D parent, Mesh mesh, Vector3 position, Color color)
    {
        parent.AddChild(new MeshInstance3D
        {
            Mesh = mesh,
            Position = position,
            MaterialOverride = new StandardMaterial3D { AlbedoColor = color }
        });
    }

    private void Build()
    {
        var p = Proportions;
        var skin = new Color(0.82f, 0.58f, 0.42f);
        var shirt = new Color(0.18f, 0.42f, 0.72f);
        var trousers = new Color(0.12f, 0.25f, 0.48f);
        var hair = new Color(0.2f, 0.13f, 0.08f);
        var scale = p.Height / 1.8f;
        var limb = 0.11f * scale * p.LimbThickness;
        var torsoWidth = 0.46f * scale * p.TorsoWidth;
        var torsoDepth = 0.26f * scale * p.TorsoDepth;

        Torso = Joint(Root, "Torso", new Vector3(0, p.LegLength, 0));
        // Pelvis stays with the hips, the chest block sits on the twisting torso joint.
        Part(Root, new BoxMesh { Size = new Vector3(0.4f * scale * p.HipSpan, 0.2f * scale, torsoDepth * 0.95f) },
            new Vector3(0, p.LegLength, 0), trousers);
        Part(Torso, new BoxMesh { Size = new Vector3(torsoWidth * p.ShoulderSpan, p.TorsoLength, torsoDepth) },
            new Vector3(0, p.TorsoLength * 0.5f + 0.04f, 0), shirt);
        if (p.ChestSize > 0)
        {
            foreach (var x in new[] { -0.09f, 0.09f })
            {
                Part(Torso, new SphereMesh { Radius = 0.075f * scale, Height = 0.14f * scale },
                    new Vector3(x * scale, p.TorsoLength * 0.72f, -torsoDepth * 0.5f), shirt);
            }
        }

        var neck = Joint(Torso, "Neck", new Vector3(0, p.TorsoLength + 0.04f, 0));
        Part(neck, new CylinderMesh { TopRadius = limb * 0.55f, BottomRadius = limb * 0.65f, Height = p.NeckLength + 0.04f },
            new Vector3(0, p.NeckLength * 0.5f, 0), skin);
        Head = Joint(neck, "Head", new Vector3(0, p.NeckLength, 0));
        Part(Head, new SphereMesh { Radius = p.HeadRadius, Height = p.HeadRadius * 2.1f }, new Vector3(0, p.HeadRadius, 0), skin);
        Part(Head, new SphereMesh { Radius = p.HeadRadius * 1.02f, Height = p.HeadRadius * 1.1f },
            new Vector3(0, p.HeadRadius * 1.45f, p.HeadRadius * 0.12f), hair);
        foreach (var x in new[] { -0.4f, 0.4f })
        {
            Part(Head, new SphereMesh { Radius = p.HeadRadius * 0.1f, Height = p.HeadRadius * 0.2f },
                new Vector3(x * p.HeadRadius, p.HeadRadius * 1.1f, -p.HeadRadius * 0.92f), Colors.Black);
        }

        var shoulderX = torsoWidth * p.ShoulderSpan * 0.5f + limb * 0.55f;
        var shoulderY = p.TorsoLength * 0.92f + 0.04f;
        (LeftShoulder, LeftElbow) = BuildArm(Torso, "Left", new Vector3(-shoulderX, shoulderY, 0), p.ArmLength, limb, shirt, skin);
        (RightShoulder, RightElbow) = BuildArm(Torso, "Right", new Vector3(shoulderX, shoulderY, 0), p.ArmLength, limb, shirt, skin);

        var hipX = 0.1f * scale * p.HipSpan * p.TorsoWidth;
        (LeftHip, LeftKnee) = BuildLeg("Left", new Vector3(-hipX, p.LegLength, 0), p.LegLength, limb * 1.3f, trousers);
        (RightHip, RightKnee) = BuildLeg("Right", new Vector3(hipX, p.LegLength, 0), p.LegLength, limb * 1.3f, trousers);
    }

    private static (Node3D Shoulder, Node3D Elbow) BuildArm(Node3D parent, string side, Vector3 position, float length,
        float thickness, Color sleeve, Color skin)
    {
        var shoulder = Joint(parent, side + "Shoulder", position);
        Part(shoulder, new SphereMesh { Radius = thickness * 0.75f, Height = thickness * 1.5f }, Vector3.Zero, sleeve);
        Part(shoulder, new CapsuleMesh { Radius = thickness * 0.5f, Height = length * 0.5f + thickness * 0.5f },
            new Vector3(0, -length * 0.25f, 0), sleeve);
        var elbow = Joint(shoulder, side + "Elbow", new Vector3(0, -length * 0.5f, 0));
        Part(elbow, new CapsuleMesh { Radius = thickness * 0.42f, Height = length * 0.5f }, new Vector3(0, -length * 0.25f, 0), skin);
        Part(elbow, new SphereMesh { Radius = thickness * 0.5f, Height = thickness }, new Vector3(0, -length * 0.5f, 0), skin);
        return (shoulder, elbow);
    }

    private (Node3D Hip, Node3D Knee) BuildLeg(string side, Vector3 position, float length, float thickness, Color trousers)
    {
        var hip = Joint(Root, side + "Hip", position);
        Part(hip, new CapsuleMesh { Radius = thickness * 0.5f, Height = length * 0.5f + thickness * 0.4f },
            new Vector3(0, -length * 0.25f, 0), trousers);
        var knee = Joint(hip, side + "Knee", new Vector3(0, -length * 0.5f, 0));
        Part(knee, new CapsuleMesh { Radius = thickness * 0.42f, Height = length * 0.5f }, new Vector3(0, -length * 0.25f, 0), trousers);
        Part(knee, new BoxMesh { Size = new Vector3(thickness, 0.07f, thickness * 1.8f) },
            new Vector3(0, -length * 0.5f + 0.035f, -thickness * 0.4f), new Color(0.12f, 0.1f, 0.09f));
        return (hip, knee);
    }
}
