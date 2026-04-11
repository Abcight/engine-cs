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
uniform sampler2D _roughness_texture;
uniform sampler2D _occlusion_texture;
uniform sampler2D _emissive_texture;

uniform bool _has_base_color_texture;
uniform bool _has_normal_texture;
uniform bool _has_metallic_roughness_texture;
uniform bool _has_roughness_texture;
uniform bool _has_occlusion_texture;
uniform bool _has_emissive_texture;

const float PI = 3.14159265359;

float DistributionGGX(vec3 N, vec3 H, float roughness) {
    float a = roughness * roughness;
    float a2 = a * a;
    float NdotH = max(dot(N, H), 0.0);
    float NdotH2 = NdotH * NdotH;

    float nom   = a2;
    float denom = (NdotH2 * (a2 - 1.0) + 1.0);
    denom = PI * denom * denom;

    return nom / max(denom, 0.0000001);
}

float GeometrySchlickGGX(float NdotV, float roughness) {
    float r = (roughness + 1.0);
    float k = (r * r) / 8.0;

    float nom   = NdotV;
    float denom = NdotV * (1.0 - k) + k;

    return nom / denom;
}

float GeometrySmith(vec3 N, vec3 V, vec3 L, float roughness) {
    float NdotV = max(dot(N, V), 0.0);
    float NdotL = max(dot(N, L), 0.0);
    float ggx2 = GeometrySchlickGGX(NdotV, roughness);
    float ggx1 = GeometrySchlickGGX(NdotL, roughness);

    return ggx1 * ggx2;
}

vec3 FresnelSchlick(float cosTheta, vec3 F0) {
    return F0 + (1.0 - F0) * pow(clamp(1.0 - cosTheta, 0.0, 1.0), 5.0);
}

vec3 EvaluateLight(
    vec3 lightDirection,
    vec3 lightColor,
    vec3 normal,
    vec3 viewDirection,
    vec3 baseColor,
    float metallic,
    float roughness
) {
    vec3 L = lightDirection;
    vec3 V = viewDirection;
    vec3 N = normal;
    vec3 H = normalize(V + L);

    float NdotL = max(dot(N, L), 0.0);
    if (NdotL <= 0.0) {
        return vec3(0.0);
    }

    vec3 F0 = mix(vec3(0.04), baseColor, metallic);
    vec3 F = FresnelSchlick(max(dot(H, V), 0.0), F0);

    float NDF = DistributionGGX(N, H, roughness);   
    float G   = GeometrySmith(N, V, L, roughness);      
    
    vec3 numerator    = NDF * G * F; 
    float denominator = 4.0 * max(dot(N, V), 0.0) * NdotL + 0.0001;
    vec3 specular     = numerator / denominator;
    
    vec3 kS = F;
    vec3 kD = vec3(1.0) - kS;
    kD *= 1.0 - metallic;	  

    vec3 diffuse = kD * baseColor / PI;

    return (diffuse + specular) * lightColor * NdotL;
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
        if (!_has_roughness_texture) {
            roughness *= clamp(metalRoughSample.g, 0.04, 1.0);
        }
    }

    if (_has_roughness_texture) {
        float roughnessSample = texture(_roughness_texture, vTexCoord).r;
        roughness *= clamp(roughnessSample, 0.04, 1.0);
    }

    vec3 normal = normalize(vWorldNormal);
    if (_has_normal_texture) {
        vec3 sampledNormal = texture(_normal_texture, vTexCoord).xyz * 2.0 - 1.0;
        if (length(sampledNormal) > 0.001) {
            vec3 dp1 = dFdx(vWorldPosition);
            vec3 dp2 = dFdy(vWorldPosition);
            vec2 duv1 = dFdx(vTexCoord);
            vec2 duv2 = dFdy(vTexCoord);

            vec3 tangent = dp1 * duv2.y - dp2 * duv1.y;
            vec3 bitangent = dp2 * duv1.x - dp1 * duv2.x;
            float tangentLen2 = dot(tangent, tangent);
            float bitangentLen2 = dot(bitangent, bitangent);
            if (tangentLen2 > 1e-8 && bitangentLen2 > 1e-8) {
                tangent = normalize(tangent - normal * dot(normal, tangent));
                float handedness = dot(cross(normal, tangent), bitangent) < 0.0 ? -1.0 : 1.0;
                bitangent = normalize(cross(normal, tangent)) * handedness;
                mat3 tbn = mat3(tangent, bitangent, normal);
                normal = normalize(tbn * sampledNormal);
            }
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
    color = color / (color + vec3(1.0));
    color = pow(color, vec3(1.0 / 2.2));

    FragColor = vec4(color, baseColor.a);
}
