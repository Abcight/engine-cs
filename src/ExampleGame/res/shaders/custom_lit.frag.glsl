#version 330 core

in vec3 vWorldPosition;
in vec3 vWorldNormal;
in vec2 vTexCoord;
in float vClipDepth;

out vec4 FragColor;

uniform vec3 _engine_camera_world_position;

uniform int _engine_directional_light_count;
uniform vec4 _engine_directional_light_directions[8];
uniform vec4 _engine_directional_light_colors[8];

uniform int _engine_point_light_count;
uniform vec4 _engine_point_light_positions[16];
uniform vec4 _engine_point_light_colors[16];
uniform float _engine_point_light_ranges[16];

uniform vec4 _tint_color;
uniform vec3 _rim_color;
uniform float _rim_power;

vec3 ComputeLight(vec3 normal, vec3 viewDirection, vec3 lightDirection, vec3 lightColor) {
    float ndotl = max(dot(normal, lightDirection), 0.0);
    vec3 diffuse = _tint_color.rgb * lightColor * ndotl;

    vec3 halfDirection = normalize(lightDirection + viewDirection);
    float specular = pow(max(dot(normal, halfDirection), 0.0), 36.0) * ndotl;
    vec3 specularColor = mix(vec3(0.04), _tint_color.rgb, 0.35) * specular * lightColor;

    return diffuse + specularColor;
}

void main() {
    vec3 normal = normalize(vWorldNormal);
    vec3 viewDirection = normalize(_engine_camera_world_position - vWorldPosition);

    vec3 shaded = vec3(0.03) * _tint_color.rgb;

    for (int i = 0; i < _engine_directional_light_count && i < 8; i++) {
        vec3 lightDirection = normalize(-_engine_directional_light_directions[i].xyz);
        vec3 lightColor = _engine_directional_light_colors[i].xyz;
        shaded += ComputeLight(normal, viewDirection, lightDirection, lightColor);
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
            shaded += ComputeLight(normal, viewDirection, lightDirection, lightColor);
        }
    }

    float rim = pow(1.0 - max(dot(normal, viewDirection), 0.0), max(_rim_power, 0.001));
    shaded += _rim_color * rim;

    float depthTint = clamp((vClipDepth + 3.0) / 6.0, 0.0, 1.0);
    shaded *= mix(0.9, 1.1, depthTint);

    shaded = shaded / (shaded + vec3(1.0));
    shaded = pow(shaded, vec3(1.0 / 2.2));

    FragColor = vec4(shaded, _tint_color.a);
}
