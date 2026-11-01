using Godot;
using Simulation;

namespace UnnamedProject2026;

/// <summary>Combines only currently visible outfit coverage; base skin stays intact.</summary>
internal sealed class PlayerBodyMask
{
    private const int MaskSize = 512;
    private readonly List<ShaderMaterial> _materials = [];
    private string? _signature;
    public int CoveredPixels { get; private set; }

    public PlayerBodyMask(Node3D model)
    {
        foreach (var mesh in model.FindChildren("HumanBody", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
        for (var surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
        {
            var original = mesh.GetActiveMaterial(surface) as StandardMaterial3D;
            var material = new ShaderMaterial { ResourceLocalToScene = true,
                Shader = GD.Load<Shader>("res://assets/appearance/body_coverage.gdshader") };
            material.SetShaderParameter("skin_color", original?.AlbedoColor ?? new Color(.8f, .8f, .8f));
            material.SetShaderParameter("skin_roughness", original?.Roughness ?? .7f);
            if (original?.AlbedoTexture is { } texture)
            {
                material.SetShaderParameter("skin_albedo", texture);
                material.SetShaderParameter("use_skin_albedo", true);
            }
            mesh.SetSurfaceOverrideMaterial(surface, material);
            _materials.Add(material);
        }
        if (_materials.Count == 0) throw new InvalidDataException("Player presentation has no maskable body mesh.");
    }

    public void Apply(IEnumerable<string> visibleItems, PlayerSex sex)
    {
        var ids = visibleItems.OrderBy(id => id, StringComparer.Ordinal).ToArray();
        var signature = string.Join("|", ids);
        if (signature == _signature) return;
        _signature = signature;
        var combined = new byte[MaskSize * MaskSize];
        foreach (var id in ids)
        {
            var item = PlayerAppearanceAssets.Default.GetItem(id);
            if (item.SkinMask(sex) is not { } texture) continue;
            using var image = texture.GetImage();
            if (image.IsCompressed() && image.Decompress() != Error.Ok)
                throw new InvalidDataException($"Cannot decompress body coverage for '{id}'.");
            if (image.IsEmpty()) throw new InvalidDataException($"Body coverage for '{id}' is empty.");
            if (image.GetWidth() != MaskSize || image.GetHeight() != MaskSize)
                image.Resize(MaskSize, MaskSize, Image.Interpolation.Nearest);
            image.Convert(Image.Format.Rgba8);
            var data = image.GetData();
            for (var pixel = 0; pixel < combined.Length; pixel++)
                combined[pixel] = Math.Max(combined[pixel],
                    item.InvertSkinMask ? (byte)(255 - data[pixel * 4]) : data[pixel * 4]);
        }
        CoveredPixels = combined.Count(value => value > 127);
        using var union = Image.CreateFromData(MaskSize, MaskSize, false, Image.Format.L8, combined);
        var mask = ImageTexture.CreateFromImage(union);
        foreach (var material in _materials) material.SetShaderParameter("coverage_mask", mask);
    }
}
