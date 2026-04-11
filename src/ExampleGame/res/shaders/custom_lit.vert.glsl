#version 330 core

layout(location = 0) in vec3 aPosition;
layout(location = 1) in vec3 aNormal;
layout(location = 2) in vec2 aTexCoord;

uniform mat4 _engine_model;
uniform mat4 _engine_view;
uniform mat4 _engine_projection;
uniform mat4 _engine_model_view_projection;

out vec3 vWorldPosition;
out vec3 vWorldNormal;
out vec2 vTexCoord;
out float vClipDepth;

void main() {
    vec4 worldPosition = _engine_model * vec4(aPosition, 1.0);
    vec4 clipFromSeparate = (_engine_projection * _engine_view) * worldPosition;
    vec4 clipFromCombined = _engine_model_view_projection * vec4(aPosition, 1.0);

    mat3 normalMatrix = mat3(transpose(inverse(_engine_model)));
    vWorldPosition = worldPosition.xyz;
    vWorldNormal = normalize(normalMatrix * aNormal);
    vTexCoord = aTexCoord;
    vClipDepth = clipFromCombined.z;

    gl_Position = clipFromSeparate;
}
