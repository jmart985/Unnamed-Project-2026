using Godot;

namespace UnnamedProject2026;

internal static class CharacterLightingValidation
{
    public static void Run(WorldEnvironment worldEnvironment, DirectionalLight3D keyLight)
    {
        var environment = worldEnvironment.Environment
            ?? throw new InvalidOperationException("The main WorldEnvironment has no Environment resource.");
        if (environment.AmbientLightSource != Godot.Environment.AmbientSource.Color)
            throw new InvalidOperationException("The main Environment must use its explicit ambient fill color.");
        if (environment.AmbientLightSkyContribution > 0.01f)
            throw new InvalidOperationException("The configured ambient fill must not depend on sky contribution.");
        if (environment.AmbientLightEnergy is < 0.35f or > 0.9f)
            throw new InvalidOperationException("Ambient light energy must stay within the readable, restrained prototype range.");
        var ambient = environment.AmbientLightColor;
        if (ambient.R < 0.25f || ambient.G < 0.25f || ambient.B < 0.25f
            || Mathf.Abs(ambient.R - ambient.G) > 0.12f
            || Mathf.Abs(ambient.G - ambient.B) > 0.12f)
            throw new InvalidOperationException("Ambient fill must be bright enough to reveal shaded materials and remain close to neutral.");
        if (!keyLight.ShadowEnabled || keyLight.LightEnergy <= 0)
            throw new InvalidOperationException("The directional key light and its shadows must remain enabled.");

        GD.Print("CHARACTER LIGHTING VALIDATION PASSED: neutral ambient fill reveals shaded materials while the directional key light and shadows remain active.");
    }
}
