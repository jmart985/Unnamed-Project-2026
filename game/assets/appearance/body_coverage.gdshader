shader_type spatial;
render_mode cull_back;
uniform sampler2D coverage_mask : filter_nearest, repeat_disable;
uniform vec4 skin_color : source_color = vec4(0.8, 0.8, 0.8, 1.0);
uniform float skin_roughness = 0.7;
uniform sampler2D skin_albedo : source_color;
uniform bool use_skin_albedo = false;
void fragment() {
    if (texture(coverage_mask, UV).r > 0.5) {
        discard;
    }
    ALBEDO = skin_color.rgb;
    if (use_skin_albedo) {
        ALBEDO *= texture(skin_albedo, UV).rgb;
    }
    ROUGHNESS = skin_roughness;
}
