#version 330 core

in vec3 vWorldPosition;
in vec3 vWorldNormal;
in vec2 vTexCoord;
in float vMvpDepth;

out vec4 FragColor;

uniform vec3 _engine_camera_world_position;

uniform int _engine_directional_light_count;
uniform vec4 _engine_directional_light_directions[8];
uniform vec4 _engine_directional_light_colors[8];

uniform int _engine_point_light_count;
uniform vec4 _engine_point_light_positions[16];
uniform vec4 _engine_point_light_colors[16];
uniform float _engine_point_light_ranges[16];

uniform vec4 _base_color_factor;
uniform vec3 _emissive_factor;
uniform float _metallic_factor;
uniform float _roughness_factor;
uniform float _occlusion_strength;

uniform sampler2D _base_color_texture;
uniform sampler2D _normal_texture;
uniform sampler2D _metallic_roughness_texture;
uniform sampler2D _occlusion_texture;
uniform sampler2D _emissive_texture;

uniform bool _has_base_color_texture;
uniform bool _has_normal_texture;
uniform bool _has_metallic_roughness_texture;
uniform bool _has_occlusion_texture;
uniform bool _has_emissive_texture;

vec3 EvaluateLight(
    vec3 lightDirection,
    vec3 lightColor,
    vec3 normal,
    vec3 viewDirection,
    vec3 baseColor,
    float metallic,
    float roughness
) {
    float ndotl = max(dot(normal, lightDirection), 0.0);
    if (ndotl <= 0.0) {
        return vec3(0.0);
    }

    vec3 halfDirection = normalize(lightDirection + viewDirection);
    float ndoth = max(dot(normal, halfDirection), 0.0);
    float specularPower = mix(8.0, 128.0, 1.0 - roughness);
    float specularTerm = pow(ndoth, specularPower) * ndotl;

    vec3 diffuse = (1.0 - metallic) * baseColor * ndotl;
    vec3 f0 = mix(vec3(0.04), baseColor, metallic);
    vec3 specular = f0 * specularTerm;

    return (diffuse + specular) * lightColor;
}

void main() {
    vec4 baseColor = _base_color_factor;
    if (_has_base_color_texture) {
        baseColor *= texture(_base_color_texture, vTexCoord);
    }

    float metallic = clamp(_metallic_factor, 0.0, 1.0);
    float roughness = clamp(_roughness_factor, 0.04, 1.0);
    if (_has_metallic_roughness_texture) {
        vec4 metalRoughSample = texture(_metallic_roughness_texture, vTexCoord);
        metallic *= clamp(metalRoughSample.b, 0.0, 1.0);
        roughness *= clamp(metalRoughSample.g, 0.04, 1.0);
    }

    vec3 normal = normalize(vWorldNormal);
    if (_has_normal_texture) {
        vec3 sampledNormal = texture(_normal_texture, vTexCoord).xyz * 2.0 - 1.0;
        if (length(sampledNormal) > 0.001) {
            normal = normalize(sampledNormal);
        }
    }

    vec3 viewDirection = normalize(_engine_camera_world_position - vWorldPosition);
    vec3 lighting = vec3(0.02) * baseColor.rgb;

    for (int i = 0; i < _engine_directional_light_count && i < 8; i++) {
        vec3 lightDirection = normalize(-_engine_directional_light_directions[i].xyz);
        vec3 lightColor = _engine_directional_light_colors[i].xyz;
        lighting += EvaluateLight(lightDirection, lightColor, normal, viewDirection, baseColor.rgb, metallic, roughness);
    }

    for (int i = 0; i < _engine_point_light_count && i < 16; i++) {
        vec3 toLight = _engine_point_light_positions[i].xyz - vWorldPosition;
        float distanceToLight = length(toLight);
        float range = max(_engine_point_light_ranges[i], 0.0001);
        float attenuation = clamp(1.0 - (distanceToLight / range), 0.0, 1.0);
        attenuation *= attenuation;

        if (attenuation > 0.0 && distanceToLight > 0.0001) {
            vec3 lightDirection = toLight / distanceToLight;
            vec3 lightColor = _engine_point_light_colors[i].xyz * attenuation;
            lighting += EvaluateLight(lightDirection, lightColor, normal, viewDirection, baseColor.rgb, metallic, roughness);
        }
    }

    float occlusion = 1.0;
    if (_has_occlusion_texture) {
        float aoSample = texture(_occlusion_texture, vTexCoord).r;
        occlusion = mix(1.0, aoSample, clamp(_occlusion_strength, 0.0, 1.0));
    }

    vec3 emissive = _emissive_factor;
    if (_has_emissive_texture) {
        emissive *= texture(_emissive_texture, vTexCoord).rgb;
    }

    vec3 color = lighting * occlusion + emissive;
    float depthHint = clamp((vMvpDepth + 3.0) / 6.0, 0.85, 1.15);
    color *= depthHint;
    color = color / (color + vec3(1.0));
    color = pow(color, vec3(1.0 / 2.2));

    FragColor = vec4(color, baseColor.a);
}
